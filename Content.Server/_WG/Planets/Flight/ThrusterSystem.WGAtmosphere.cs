using Content.Shared._CE.ZLevels.Core.Components;
using Content.Server._CE.ZLevels.Core;
using Content.Server._WG.Planets.Flight;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Pow3r;
using Content.Server.Shuttles.Components;
using Content.Shared._WG.Planets.Flight;
using Content.Shared._WG.Planets;
using Content.Shared.Shuttles.Components;
using Content.Shared.Examine;
using Robust.Shared.Map.Components;

namespace Content.Server.Shuttles.Systems;

/// <summary>Ordinary engines lose half their thrust below orbit. Conversions retain their ratings.</summary>
public sealed partial class ThrusterSystem
{
    public const float WgAtmosphereThrustMultiplier = 0.5f;
    // Hover power multiplier for ordinary engines; 1 keeps atmosphere power the same as space.
    public const float WgAtmospherePowerMultiplier = 1f;
    public const float WgStandardGravity = 9.81f;
    private TimeSpan _wgNextAtmosphereUpdate;
    private TimeSpan _wgRestingAt;
    private readonly Dictionary<EntityUid, bool> _wgResting = new();

    private bool WgRestingWreck(EntityUid grid)
    {
        if (_wgRestingAt != _timing.CurTime)
        {
            _wgRestingAt = _timing.CurTime;
            _wgResting.Clear();
        }
        if (!_wgResting.TryGetValue(grid, out var resting))
            _wgResting[grid] = resting = _wgFlightLevels.WgWreckResting(grid);
        return resting;
    }
    [Dependency] private PowerNetSystem _wgPowerNet = default!;
    [Dependency] private CEZLevelsSystem _wgFlightLevels = default!;
    [Dependency] private WGThrusterIndexSystem _wgThrusterIndex = default!;

    private void WgExamineAtmosphere(EntityUid uid, ExaminedEvent args)
    {
        if (HasComp<WGLandingThrusterComponent>(uid))
            args.PushText(Loc.GetString("wg-thruster-landing-converted"));
        else if (TryComp<WGAtmosphereThrusterComponent>(uid, out var state) && state.Atmospheric)
            args.PushText(Loc.GetString("wg-thruster-atmosphere-mode"));
    }

    /// <summary>True when a grid is on a planet layer below orbit or in a gap above one.</summary>
    public bool WgInAtmosphere(EntityUid grid)
    {
        var map = Transform(grid).MapUid;
        if (map == null || HasComp<WGOrbitLayerComponent>(map))
            return false;
        if (HasComp<WGPlanetLayerComponent>(map))
            return true;
        return TryComp<CEZTransitMapComponent>(map, out var transit)
               && HasComp<WGPlanetLayerComponent>(transit.LowerMap ?? transit.UpperMap);
    }

    private void WgUpdateAtmosphereThrusters()
    {
        if (_timing.CurTime < _wgNextAtmosphereUpdate)
            return;
        _wgNextAtmosphereUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        WgUpdatePowerPulses();
        var query = EntityQueryEnumerator<ThrusterComponent>();
        while (query.MoveNext(out var uid, out var thruster))
        {
            if (thruster.Type == ThrusterType.Linear)
                WgRefreshAtmosphereThruster(uid, thruster);
        }
    }

    /// <summary>Applies or lifts the atmosphere thrust and power penalty on one linear thruster.</summary>
    public void WgRefreshAtmosphereThruster(EntityUid uid, ThrusterComponent thruster)
    {
        if (thruster.Type != ThrusterType.Linear)
            return;
        var xform = Transform(uid);
        var inAtmosphere = xform.GridUid is { } grid && !HasComp<MapComponent>(grid) && WgInAtmosphere(grid);
        if (!TryComp<WGAtmosphereThrusterComponent>(uid, out var state))
        {
            if (!inAtmosphere)
                return;
            state = AddComp<WGAtmosphereThrusterComponent>(uid);
            state.RatedThrust = thruster.Thrust;
            state.RatedLoad = thruster.OriginalLoad;
            state.WasPowered = thruster.IsOn;
        }
        var penalized = inAtmosphere && !HasComp<WGLandingThrusterComponent>(uid);
        var thrust = state.RatedThrust * (penalized ? WgAtmosphereThrustMultiplier : 1f);
        // Adjust the registered directional bank without toggling power, sound or burn fixtures.
        if (thruster.Thrust != thrust)
        {
            if (thruster.IsOn && TryComp<ShuttleComponent>(xform.GridUid, out var shuttle))
            {
                var direction = (int)xform.LocalRotation.GetCardinalDir() / 2;
                shuttle.LinearThrust[direction] += thrust - thruster.Thrust;
            }
            thruster.Thrust = thrust;
        }
        state.Atmospheric = inAtmosphere;
        thruster.OriginalLoad = state.RatedLoad * (penalized ? WgAtmospherePowerMultiplier : 1f);
        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            receiver.Load = thruster.Enabled && !state.Cooling ? thruster.OriginalLoad : 1f;
        if (state.Cooling)
            DisableThruster(uid, thruster);
        else if (!thruster.IsOn && CanEnable(uid, thruster))
            EnableThruster(uid, thruster);

        state.WasPowered = thruster.IsOn;
    }

    /// <summary>Changes the unscaled rating, including while in atmosphere. Also used by fixture builders.</summary>
    public void WgSetRatedThrust(EntityUid uid, float rating)
    {
        var thruster = Comp<ThrusterComponent>(uid);
        var on = thruster.IsOn;
        if (on)
            DisableThruster(uid, thruster);
        thruster.Thrust = rating;
        WgRefreshAtmosphereRating(uid, thruster);
        if (on && CanEnable(uid, thruster))
            EnableThruster(uid, thruster);
    }

    private void WgRefreshAtmosphereRating(EntityUid uid, ThrusterComponent thruster)
    {
        if (TryComp<WGAtmosphereThrusterComponent>(uid, out var state))
            state.RatedThrust = thruster.Thrust;
        WgRefreshAtmosphereThruster(uid, thruster);
    }

    /// <summary>Prospective atmospheric force, even in orbit; ignores unavailable engines.</summary>
    public float WgAtmosphericForce(EntityUid uid, ThrusterComponent thruster)
    {
        if (thruster.Type != ThrusterType.Linear || !thruster.Enabled || !thruster.IsOn)
            return 0f;
        if (TryComp<WGAtmosphereThrusterComponent>(uid, out var state) && state.Cooling)
            return 0f;
        return WgRatedThrust(uid, thruster) * (HasComp<WGLandingThrusterComponent>(uid) ? 1f : WgAtmosphereThrustMultiplier);
    }

    /// <summary>The engine's rating without the atmosphere penalty.</summary>
    public float WgRatedThrust(EntityUid uid, ThrusterComponent thruster)
    {
        return TryComp<WGAtmosphereThrusterComponent>(uid, out var state) ? state.RatedThrust : thruster.Thrust;
    }

    /// <summary>Continuous engine demand and local power-network headroom, not a battery endurance guarantee.</summary>
    public void WgAtmospherePower(IEnumerable<EntityUid> grids, out float demand, out bool deficit)
    {
        WgAtmospherePower(grids, out demand, out deficit, out _);
    }

    /// <inheritdoc cref="WgAtmospherePower(IEnumerable{EntityUid}, out float, out bool)"/>
    /// <param name="shortfall">Watts the networks are short, summed over networks; unlinked engines count their whole demand.</param>
    public void WgAtmospherePower(IEnumerable<EntityUid> grids, out float demand, out bool deficit, out float shortfall)
    {
        demand = 0f;
        deficit = false;
        shortfall = 0f;
        var extraByNet = new Dictionary<PowerState.Network, float>();
        foreach (var grid in grids)
        {
            if (_wgThrusterIndex.ThrustersOn(grid) is not { } gridThrusters)
                continue;

            foreach (var uid in gridThrusters)
            {
                if (!TryComp<ThrusterComponent>(uid, out var thruster) || thruster.Type != ThrusterType.Linear
                    || !thruster.Enabled || !Transform(uid).Anchored
                    || !TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                    continue;
                var normalLoad = TryComp<WGAtmosphereThrusterComponent>(uid, out var state)
                    ? state.RatedLoad : thruster.OriginalLoad;
                var requested = normalLoad * (HasComp<WGLandingThrusterComponent>(uid) ? 1f : WgAtmospherePowerMultiplier);
                demand += requested;
                if (!receiver.NeedsPower)
                    continue;
                if (receiver.PowerDisabled || receiver.Provider?.Net is not { } net)
                {
                    deficit = true;
                    shortfall += requested;
                    continue;
                }
                extraByNet[net.NetworkNode] = extraByNet.GetValueOrDefault(net.NetworkNode) + requested - receiver.Load;
            }
        }
        foreach (var (net, extra) in extraByNet)
        {
            if (!WgTryGetPowerStatistics(net, out var stats))
            {
                deficit = true;
                shortfall += MathF.Max(extra, 0f);
                continue;
            }
            var missing = stats.Consumption + extra - stats.SupplyCurrent;
            if (missing > 1f)
            {
                deficit = true;
                shortfall += missing;
            }
        }
    }
}
