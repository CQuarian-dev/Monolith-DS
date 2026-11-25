using System.Numerics;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._WG.Planets;
using Content.Server._WG.Planets.Flight;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets.Flight;
using Content.Shared._WG.Planets;
using Content.Shared.Destructible;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>Atmospheric flight hooks: thruster lift over a planet, sinking without it, and touchdown costs.</summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>A crashed hull resting on the ground with no ascent command; its engines skip the overload pulse.</summary>
    public bool WgWreckResting(EntityUid grid) =>
        HasComp<WGCrashImpactComponent>(grid) && WgHasSkidGround(grid) &&
        !HasComp<WGLiftoffComponent>(grid) && _pilotVerticalInput.GetValueOrDefault(grid) <= 0f;

    [Dependency] private SharedDestructibleSystem _wgDestructible = default!;
    [Dependency] private WGFlightSystem _wgFlight = default!;
    [Dependency] private WGOrbitEntrySystem _wgOrbitEntry = default!;
    [Dependency] private ThrusterSystem _wgThrusters = default!;
    [Dependency] private WGThrusterIndexSystem _wgThrusterIndex = default!;

    /// <summary>Lift ratio at or above which a hull flies normally; below it the hull is lift-lost.</summary>
    public const float WGFullLiftRatio = 1f;

    /// <summary>Lift ratio below which partial lift stops scaling the fall by (1 - ratio).</summary>
    public const float WGPartialLiftRatio = 0.5f;

    /// <summary>Fraction of the free-fall landing speed at or above which a touchdown is a crash.</summary>
    // Above the partial-lift band's landing speed (about 0.74 at 0.5 lift), so that band lands hard instead.
    public const float WGHardLandingFraction = 0.8f;

    // Matches the server tick CE's own fall runs on.
    private const float WGFreeFallStep = 1f / 60f;

    // Seconds; stops an unreachable gap from hanging the integration loop.
    private const float WGFreeFallCeiling = 600f;

    private static readonly TimeSpan WGOrbitRefusalCooldown = TimeSpan.FromSeconds(4);

    // m/s lost per anchored obstacle ploughed through.
    private const float WGPloughSpeedCost = 1.5f;

    private readonly Dictionary<EntityUid, TimeSpan> _wgNextOrbitRefusal = new();

    private readonly HashSet<EntityUid> _wgPloughed = new();

    private readonly Dictionary<(float Gravity, float Terminal), float> _wgFreeFallSpeeds = new();

    // Lift ratios computed this tick, so one hull walk serves every caller; dropped when the tick or thruster set changes.
    private readonly Dictionary<EntityUid, (bool Ok, float Ratio)> _wgLiftRatioMemo = new();
    private GameTick _wgLiftRatioTick;
    private uint _wgLiftRatioVersion;

    /// <summary>Surface gravity of the planet a grid is over; a transit gap uses the layer below it.</summary>
    public bool WgTryGetPlanetGravity(EntityUid grid, out float gravity)
    {
        gravity = 1f;

        if (Transform(grid).MapUid is not { } mapUid)
            return false;

        if (TryComp<WGPlanetLayerComponent>(mapUid, out var layer))
        {
            gravity = layer.Gravity;
            return true;
        }

        if (!TryComp<CEZTransitMapComponent>(mapUid, out var transit))
            return false;

        var anchor = transit.LowerMap ?? transit.UpperMap;

        if (anchor is not { } anchorMap || !TryComp<WGPlanetLayerComponent>(anchorMap, out var anchorLayer))
            return false;

        gravity = anchorLayer.Gravity;
        return true;
    }

    /// <summary>True when this grid is flying over a planet, where thrusters replace the gravgen as lift.</summary>
    public bool WgIsPlanetFlight(EntityUid grid) => WgTryGetPlanetGravity(grid, out _);

    /// <summary>Current lift from one grid's landing thrusters, before dividing by planet gravity.</summary>
    public float WgGetLandingThrust(EntityUid grid)
    {
        var lift = 0f;
        if (_wgThrusterIndex.ThrustersOn(grid) is not { } thrusters)
            return lift;

        foreach (var uid in thrusters)
        {
            if (TryComp<ThrusterComponent>(uid, out var thruster))
                lift += _wgThrusters.WgAtmosphericForce(uid, thruster) / ThrusterSystem.WgStandardGravity;
        }
        return lift;
    }

    /// <summary>Fraction of thrust left for manoeuvres after supporting the ship's weight.</summary>
    public float WgManeuveringFactor(EntityUid grid)
    {
        if (!_wgThrusters.WgInAtmosphere(grid) || !WgTryGetLiftRatio(grid, out var ratio))
            return 1f;
        return ratio <= 1f ? 0f : Math.Clamp(1f - 1f / ratio, 0f, 1f);
    }

    /// <summary>Hover power demand for the grid's rigid set, and whether it is short of power.</summary>
    public void WgGetAtmospherePower(EntityUid grid, out float demand, out bool deficit)
    {
        _wgThrusters.WgAtmospherePower(CollectRigidSet(grid), out demand, out deficit);
    }

    /// <summary>Hover power demand for the grid's rigid set, whether it is short, and by how many watts.</summary>
    public void WgGetAtmospherePower(EntityUid grid, out float demand, out bool deficit, out float shortfall)
    {
        _wgThrusters.WgAtmospherePower(CollectRigidSet(grid), out demand, out deficit, out shortfall);
    }

    /// <summary>Pooled lift over pooled weight for a grid's whole rigid set; one is level flight.</summary>
    /// <param name="grid">Any member of the rigid body.</param>
    /// <param name="ratio">Lift over weight, or positive infinity for a weightless or force-anchored set.</param>
    public bool WgTryGetLiftRatio(EntityUid grid, out float ratio)
    {
        if (_wgLiftRatioTick != _timing.CurTick || _wgLiftRatioVersion != _wgThrusterIndex.Version)
        {
            _wgLiftRatioMemo.Clear();
            _wgLiftRatioTick = _timing.CurTick;
            _wgLiftRatioVersion = _wgThrusterIndex.Version;
        }

        if (_wgLiftRatioMemo.TryGetValue(grid, out var memo))
        {
            ratio = memo.Ratio;
            return memo.Ok;
        }

        var ok = WgComputeLiftRatio(grid, out ratio);
        _wgLiftRatioMemo[grid] = (ok, ratio);
        return ok;
    }

    private bool WgComputeLiftRatio(EntityUid grid, out float ratio)
    {
        ratio = 0f;

        if (!WgTryGetPlanetGravity(grid, out var gravity) || gravity <= 0f)
            return false;

        var lift = 0f;
        var mass = 0f;

        foreach (var member in CollectRigidSet(grid))
        {
            if (HasComp<CEZMappingAnchorGridComponent>(member))
            {
                ratio = float.PositiveInfinity;
                return true;
            }

            lift += WgGetLandingThrust(member);

            if (_physQuery.TryComp(member, out var body))
                mass += body.FixturesMass;

            mass += GetWGVirtualMass(member);
        }

        ratio = mass <= 0f ? float.PositiveInfinity : lift / (mass * gravity);
        return true;
    }

    /// <summary>True when a gravgen's hull is over a planet, where the gravgen gives no lift.</summary>
    private bool WgGravgenIsOnPlanet(EntityUid gridUid)
    {
        return _mapGridQuery.HasComp(gridUid) && WgIsPlanetFlight(gridUid);
    }

    /// <summary>Adds landing-thruster lift, divided by planet gravity, to CE's pooled capacity.</summary>
    private void WgAddLandingThrusterCapacity(Dictionary<EntityUid, float> capacity)
    {
        var query = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thruster, out var xform))
        {
            var grid = xform.ParentUid;
            if (!_mapGridQuery.HasComp(grid) || !WgTryGetPlanetGravity(grid, out var gravity) || gravity <= 0f)
                continue;
            capacity[grid] = capacity.GetValueOrDefault(grid)
                + _wgThrusters.WgAtmosphericForce(uid, thruster) / (ThrusterSystem.WgStandardGravity * gravity);
        }
    }

    /// <summary>Sink acceleration over a planet, scaled by partial lift; marks the set lift-lost.</summary>
    private float WgSinkGravity(EntityUid grid, HashSet<EntityUid> set, float gravity)
    {
        if (!WgTryGetLiftRatio(grid, out var ratio))
            return gravity;

        // A set falls as one, so every member takes the lead grid's state.
        foreach (var member in set)
        {
            _wgFlight.EnterLiftLost(member, ratio);
        }

        if (ratio >= WGFullLiftRatio)
            return gravity;

        return ratio >= WGPartialLiftRatio ? gravity * (1f - ratio) : gravity;
    }

    /// <summary>
    /// True when vertical input on an orbit layer is handled here: descend drops into the atmosphere when lift and
    /// power need no confirm, and is otherwise refused with the reason.
    /// </summary>
    // Climb is refused too: CE's climb falls back to the gap below when there is none above.
    private bool WgRefusesOrbitInput(EntityUid pilot, EntityUid console, EntityUid grid, float input)
    {
        if (input == 0f || Transform(grid).MapUid is not { } mapUid || !WgIsOrbitLayer(mapUid))
            return false;

        if (input > 0f || _timing.CurTime < _wgNextOrbitRefusal.GetValueOrDefault(grid))
            return true;

        _wgNextOrbitRefusal[grid] = _timing.CurTime + WGOrbitRefusalCooldown;

        if (!_wgOrbitEntry.TryEnterAtmosphere(console, false, out var reason))
            _popup.PopupEntity(reason, console, pilot);

        return true;
    }

    /// <summary>True when landing thrusters give full lift; CE's gravity check only knows gravgens.</summary>
    private bool WgHasVerticalLift(EntityUid grid)
    {
        return WgTryGetLiftRatio(grid, out var ratio) && ratio >= WGFullLiftRatio;
    }

    /// <summary>A skid only damages a hull while its footprint touches planetary terrain.</summary>
    public bool WgHasSkidGround(EntityUid grid)
    {
        return _mapGridQuery.TryComp(grid, out var gridComp)
               && Transform(grid).MapUid is { } map
               && HasComp<WGPlanetLayerComponent>(map)
               && _mapGridQuery.HasComp(map)
               && HasGroundUnderFootprint((grid, gridComp), map);
    }

    /// <summary>Touchdown speed (levels/s) after falling one full gap; CE resets fall speed at each layer.</summary>
    public float WgGetFreeFallSpeed(CEZGridFallerComponent faller)
    {
        return WgGetFreeFallSpeed(faller.GridGravity, faller.GridTerminalVelocity);
    }

    /// <inheritdoc cref="WgGetFreeFallSpeed(CEZGridFallerComponent)"/>
    public float WgGetFreeFallSpeed(float gravity, float terminal)
    {
        if (gravity <= 0f || terminal <= 0f)
            return 0f;

        var key = (gravity, terminal);

        if (_wgFreeFallSpeeds.TryGetValue(key, out var cached))
            return cached;

        // Same integrator as the fall itself, over one level.
        var velocity = 0f;
        var fallen = 0f;

        for (var elapsed = 0f; fallen < 1f && elapsed < WGFreeFallCeiling; elapsed += WGFreeFallStep)
        {
            velocity = ApproachTerminal(velocity, gravity, terminal, WGFreeFallStep);
            fallen += velocity * WGFreeFallStep;
        }

        _wgFreeFallSpeeds[key] = velocity;
        return velocity;
    }

    /// <summary>Hard-lands a lift-lost hull arriving under the crash threshold instead of crashing it.</summary>
    public bool WgTryHardLanding(Entity<MapGridComponent, CEZGridFallerComponent> ent, float impact)
    {
        if (!HasComp<WGLiftLostComponent>(ent.Owner))
            return false;

        var reference = WgGetFreeFallSpeed(ent.Comp2);

        if (reference <= 0f || impact >= reference * WGHardLandingFraction)
            return false;

        _wgFlight.HardLanding((ent.Owner, ent.Comp1), impact / reference);
        WgRefreshOrbitParking(ent.Owner, Transform(ent.Owner).MapUid);
        return true;
    }

    /// <summary>Starts a skid after a lift-lost hull crashes with planar speed.</summary>
    public void WgSkidAfterCrash(EntityUid grid)
    {
        if (TerminatingOrDeleted(grid) || !_mapGridQuery.HasComp(grid) || !HasComp<WGLiftLostComponent>(grid))
            return;

        if (!_physQuery.TryComp(grid, out var body))
            return;

        var crashSpeed = body.LinearVelocity.Length();

        // NaN fails every comparison, so it is rejected explicitly.
        if (!float.IsFinite(crashSpeed) || crashSpeed <= WGFlightSystem.SkidStopSpeed)
            return;

        // No thud; the crash already made its sound.
        _wgFlight.BeginSkid(grid, thud: false);
    }

    /// <summary>Planetary ship crashes use structural breakup; detached terrain keeps the plain crash.</summary>
    public bool WgTryStructuralCrash(Entity<MapGridComponent, CEZGridFallerComponent> ent, float impact)
    {
        if (!WgHasSkidGround(ent.Owner)
            || HasComp<WGDetachedTerrainComponent>(ent.Owner))
            return false;
        var reference = WgGetFreeFallSpeed(ent.Comp2);
        _wgFlight.StructuralCrash((ent.Owner, ent.Comp1), reference > 0f ? impact / reference : 1f);
        return true;
    }

    /// <summary>Sliding wrecks shed momentum over distance; stopped ships retain the normal static ground grip.</summary>
    public float WgCrashSkidFriction(EntityUid grid)
    {
        return HasComp<WGSkidComponent>(grid) ? 0.0125f : 1f;
    }

    /// <summary>Re-arms a grid's ordinary z-gravity after it has been taken off an orbit layer by hand.</summary>
    public void WgRearmZGravity(EntityUid grid)
    {
        WgRefreshOrbitParking(grid, Transform(grid).MapUid);
    }

    /// <summary>A skidding hull breaks anchored obstacles it hits, losing speed, instead of bouncing.</summary>
    private bool WgPloughThroughWalls(EntityUid grid, PhysicsComponent body)
    {
        if (!TryComp<WGSkidComponent>(grid, out var skid) || !WgHasSkidGround(grid))
            return false;

        // Rate-limited to avoid a destruction sound per obstacle per tick; between bites the hull passes through.
        if (_timing.CurTime < skid.NextPlough)
            return true;

        skid.NextPlough = _timing.CurTime + WGFlightSystem.SkidBiteInterval;

        _wgPloughed.Clear();

        foreach (var contact in _wallContacts)
        {
            // Only obstacles CE found touching hull tiles, once each.
            var ent = contact.Wall;
            if (!TerminatingOrDeleted(ent) && _physQuery.TryComp(ent, out var obstacle) && obstacle.CanCollide && obstacle.Hard)
                _wgPloughed.Add(ent);
        }

        if (_wgPloughed.Count == 0)
            return true;

        foreach (var ent in _wgPloughed)
        {
            _wgFlight.GroundObstacleImpact(grid, _transform.GetWorldPosition(ent));
            _wgDestructible.BreakEntity(ent);

            if (!TerminatingOrDeleted(ent))
                QueueDel(ent);
        }

        var velocity = body.LinearVelocity;
        var speed = velocity.Length();
        var cost = MathF.Min(WGPloughSpeedCost * _wgPloughed.Count, speed * 0.03125f);

        // Guard non-finite speed so no NaN velocity reaches the hull.
        _physics.SetLinearVelocity(grid,
            !float.IsFinite(speed) || speed <= cost ? Vector2.Zero : velocity / speed * (speed - cost),
            body: body);

        return true;
    }
}
