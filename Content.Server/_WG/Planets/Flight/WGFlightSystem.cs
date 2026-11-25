using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._WG.ShipPa;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets.Flight;
using Content.Shared._WG.Planets;
using Content.Shared._WG.ShipPa;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WG.Planets.Flight;

/// <summary>Lift-lost state for falling hulls: PA callouts, glide and the skid a hard landing ends in.</summary>
public sealed partial class WGFlightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private WGGridAudienceSystem _audience = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ShipAlertSystem _alert = default!;
    [Dependency] private ShipPaSystem _pa = default!;

    /// <summary>Caution chime the moment the lift goes, before any callout.</summary>
    public const string AlertLiftLost = "WGAlertLiftLost";

    /// <summary>Out of orbit and sinking into the top air layer.</summary>
    public const string AlertDontSink = "WGAlertDontSink";

    /// <summary>Top air layer into the middle of the stack.</summary>
    public const string AlertSinkRate = "WGAlertSinkRate";

    /// <summary>Middle of the stack into the bottom air layer.</summary>
    public const string AlertTerrain = "WGAlertTerrain";

    /// <summary>The bottom gap, first half.</summary>
    public const string AlertTooLowTerrain = "WGAlertTooLowTerrain";

    /// <summary>The bottom gap, last seconds; the one callout that repeats.</summary>
    public const string AlertPullUp = "WGAlertPullUp";

    /// <summary>Smallest lift-ratio change worth networking.</summary>
    private const float LiftRatioStep = 0.02f;

    /// <summary>Fraction of planar speed a gliding hull gains per layer fallen, compounded.</summary>
    public const float GlideGainPerLayer = 0.25f;

    /// <summary>Forward drift (m/s) along its facing given to a hull that loses lift while holding still.</summary>
    public const float GlideMinSpeed = 2f;

    /// <summary>Progress through the last gap (1 top, 0 ground) below which the pull-up callout starts.</summary>
    public const float PullUpProgress = 0.35f;

    /// <summary>How often the pull-up callout repeats.</summary>
    public static readonly TimeSpan PullUpRepeat = TimeSpan.FromSeconds(3);

    /// <summary>How often the lift-lost alarm loop restarts so late boarders hear it.</summary>
    public static readonly TimeSpan AlarmReissue = TimeSpan.FromSeconds(20);

    /// <summary>Lift-lost alarm volume (dB), kept under the callouts.</summary>
    public const float AlarmVolume = -6f;

    /// <summary>Takeoff volume (dB); the clip is mastered at -14 LUFS.</summary>
    public const float TakeoffVolume = 0f;

    /// <summary>Hard-landing thud volume (dB) aboard the hull.</summary>
    public const float HardLandingVolume = 0f;

    /// <summary>Volume (dB) of the rock-contact impacts and the grind loop while a hull skids.</summary>
    public const float SkidImpactVolume = -3f;
    public const float SkidGrindVolume = -4f;

    /// <summary>Caution alarm looped while lift is lost.</summary>
    public static readonly SoundSpecifier LiftLostLoop =
        new SoundPathSpecifier("/Audio/_WG/Planets/Flight/lift_lost.ogg");

    /// <summary>Played once at hard-landing touchdown.</summary>
    public static readonly SoundSpecifier HardLandingSound =
        new SoundCollectionSpecifier("WGGroundCrashImpacts");

    /// <summary>Played once when a grounded hull's liftoff latches. Placeholder audio.</summary>
    public static readonly SoundSpecifier TakeoffSound =
        new SoundPathSpecifier("/Audio/_WG/Planets/Flight/takeoff.ogg");

    /// <summary>Scrape looped while a skidding hull moves.</summary>
    public static readonly SoundSpecifier SkidSound =
        new SoundPathSpecifier("/Audio/_WG/Planets/Flight/ground_grind_loop.ogg");

    private readonly List<EntityUid> _liftLostScan = new();
    private readonly List<EntityUid> _skidScan = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGLiftLostComponent, ComponentShutdown>(OnLiftLostShutdown);
        SubscribeLocalEvent<WGSkidComponent, ComponentShutdown>(OnSkidShutdown);
        SubscribeLocalEvent<GridSplitEvent>(OnSkidGridSplit);
    }

    private void OnSkidGridSplit(ref GridSplitEvent args)
    {
        if (!HasComp<WGSkidComponent>(args.Grid)
            && !(TryComp<WGCrashImpactComponent>(args.Grid, out var impact) && _timing.CurTime < impact.NextImpact))
            return;
        foreach (var fragment in args.NewGrids)
        {
            EnsureComp<WGSkidComponent>(fragment).Debris = true;
            if (TryComp<WGCrashImpactComponent>(args.Grid, out var parentImpact))
                EnsureComp<WGCrashImpactComponent>(fragment).NextImpact = parentImpact.NextImpact;
        }
        RestoreCrashLattice(args.Grid, args.NewGrids);
        SeparateCrashSections(args.Grid, args.NewGrids);
        _crashThrusters.WgDetachCrashThrust(args.Grid, args.NewGrids);
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateLiftLost();
        UpdateSkids(frameTime);
    }

    // PlayGlobal audio lives in nullspace, so the loop must be stopped here or it outlives the hull.
    private void OnLiftLostShutdown(Entity<WGLiftLostComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Alarm = _audio.Stop(ent.Comp.Alarm);
    }

    /// <summary>Starts or restarts the caution alarm loop for everyone aboard.</summary>
    private void StartAlarmLoop(EntityUid grid, WGLiftLostComponent comp)
    {
        comp.Alarm = _audio.Stop(comp.Alarm);
        comp.NextAlarmLoop = _timing.CurTime + AlarmReissue;

        comp.Alarm = _audio.PlayGlobal(
            LiftLostLoop,
            _audience.Aboard(grid),
            true,
            AudioParams.Default.WithLoop(true).WithVolume(AlarmVolume))?.Entity;
    }

    /// <summary>Puts a hull into lift-lost, or refreshes its lift ratio if already there; idempotent.</summary>
    public void EnterLiftLost(EntityUid grid, float ratio)
    {
        if (TerminatingOrDeleted(grid))
            return;

        if (TryComp<WGLiftLostComponent>(grid, out var existing))
        {
            SetLiftRatio(grid, existing, ratio);
            return;
        }

        if (ratio >= CEZLevelsSystem.WGFullLiftRatio)
            return;

        var comp = AddComp<WGLiftLostComponent>(grid);
        comp.Ratio = ratio;
        comp.Stage = WGFlightAlarmStage.LiftLost;
        comp.LastDepth = int.MaxValue;

        if (TryComp<PhysicsComponent>(grid, out var body))
            comp.Glide = SeedGlide(grid, body);

        // Restored when the emergency ends.
        comp.PriorCode = CompOrNull<ShipAlertComponent>(grid)?.Code;

        Dirty(grid, comp);

        StartAlarmLoop(grid, comp);
        Callout(grid, AlertLiftLost);
    }

    /// <summary>Planar velocity a fall starts with; a stationary hull is nudged along its facing.</summary>
    private Vector2 SeedGlide(EntityUid grid, PhysicsComponent body)
    {
        var velocity = body.LinearVelocity;

        if (body.BodyType == BodyType.Static)
            return velocity;

        // Non-finite velocity falls through to the nudge.
        var lengthSquared = velocity.LengthSquared();

        if (float.IsFinite(lengthSquared) && lengthSquared >= GlideMinSpeed * GlideMinSpeed)
            return velocity;

        velocity = _transform.GetWorldRotation(grid).ToWorldVec() * GlideMinSpeed;
        _physics.SetLinearVelocity(grid, velocity, body: body);

        return velocity;
    }

    /// <summary>Ends lift-lost and restores the ship's prior situation code.</summary>
    public void LeaveLiftLost(EntityUid grid)
    {
        if (!TryComp<WGLiftLostComponent>(grid, out var comp))
            return;

        var prior = comp.PriorCode;
        RemComp<WGLiftLostComponent>(grid);

        if (prior is { } code)
            _alert.SetCode(grid, code, announce: false);
    }

    /// <summary>Plays the takeoff sound to the crew and anyone near the hull.</summary>
    public void PlayTakeoff(EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var hull))
            return;

        var centre = _transform.ToMapCoordinates(new EntityCoordinates(grid, hull.LocalAABB.Center));
        var radius = hull.LocalAABB.Size.Length() * 0.5f + 32f;
        _audio.PlayGlobal(TakeoffSound, _audience.Aboard(grid).AddInRange(centre, radius), true,
            AudioParams.Default.WithVolume(TakeoffVolume));
    }

    /// <summary>Starts a hard-landing skid; <paramref name="thud"/> is false after a crash.</summary>
    public void BeginSkid(EntityUid grid, bool thud = true)
    {
        LeaveLiftLost(grid);

        if (HasComp<WGSkidComponent>(grid))
            return;

        var skid = AddComp<WGSkidComponent>(grid);

        if (thud && TryComp<MapGridComponent>(grid, out var hull))
        {
            var centre = _transform.ToMapCoordinates(new EntityCoordinates(grid, hull.LocalAABB.Center));
            var radius = hull.LocalAABB.Size.Length() * 0.5f + 32f;
            _audio.PlayGlobal(HardLandingSound, _audience.Aboard(grid).AddInRange(centre, radius),
                true, AudioParams.Default.WithVolume(HardLandingVolume));
        }

        // The grind loop is started per section by the moving-ground sweep.
    }

    /// <summary>Stores the ratio and networks it only when it moved by a visible step, so a hull falling steadily is not dirtied every tick.</summary>
    private void SetLiftRatio(EntityUid grid, WGLiftLostComponent comp, float ratio)
    {
        if (comp.Ratio.Equals(ratio) || MathF.Abs(comp.Ratio - ratio) < LiftRatioStep)
            return;

        comp.Ratio = ratio;
        Dirty(grid, comp);
    }

    /// <summary>Advances callouts and glide for falling hulls and ends lift-lost on recovery or arrival.</summary>
    private void UpdateLiftLost()
    {
        _liftLostScan.Clear();

        var query = EntityQueryEnumerator<WGLiftLostComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _liftLostScan.Add(uid);
        }

        foreach (var grid in _liftLostScan)
        {
            if (TerminatingOrDeleted(grid) || !TryComp<WGLiftLostComponent>(grid, out var comp))
                continue;

            if (_zLevels.WgTryGetLiftRatio(grid, out var ratio))
            {
                SetLiftRatio(grid, comp, ratio);

                if (ratio >= CEZLevelsSystem.WGFullLiftRatio)
                {
                    LeaveLiftLost(grid);
                    continue;
                }
            }

            if (_timing.CurTime >= comp.NextAlarmLoop)
                StartAlarmLoop(grid, comp);

            var mapUid = Transform(grid).MapUid;

            // Back on a layer means the fall is over.
            if (!TryComp<CEZTransitMapComponent>(mapUid, out var transit))
            {
                LeaveLiftLost(grid);
                continue;
            }

            var depth = GapDepth(transit);

            if (depth < comp.LastDepth)
            {
                if (comp.LastDepth != int.MaxValue)
                    Glide(grid, comp);

                comp.LastDepth = depth;
                Advance(grid, comp, transit);
            }

            UpdateFinalApproach(grid, comp, transit);
        }
    }

    /// <summary>Depth of the layer below a transit gap, used to identify the gap.</summary>
    private int GapDepth(CEZTransitMapComponent transit)
    {
        if (transit.LowerMap is { } lower && TryComp<CEZMapComponent>(lower, out var zMap))
            return zMap.Depth;

        return int.MaxValue;
    }

    /// <summary>Picks the callout for a newly entered gap; middle gaps step it along one.</summary>
    private void Advance(EntityUid grid, WGLiftLostComponent comp, CEZTransitMapComponent transit)
    {
        WGFlightAlarmStage stage;

        if (transit.UpperMap is { } upper && HasComp<WGOrbitLayerComponent>(upper))
        {
            stage = WGFlightAlarmStage.DontSink;
        }
        else if (transit.LowerMap is { } lower && IsGroundLayer(lower))
        {
            stage = WGFlightAlarmStage.TooLowTerrain;
        }
        else
        {
            stage = (WGFlightAlarmStage) Math.Clamp((int) comp.Stage + 1,
                (int) WGFlightAlarmStage.SinkRate,
                (int) WGFlightAlarmStage.Terrain);
        }

        SetStage(grid, comp, stage);
    }

    /// <summary>Starts and repeats the pull-up callout near the bottom of the last gap.</summary>
    private void UpdateFinalApproach(EntityUid grid, WGLiftLostComponent comp, CEZTransitMapComponent transit)
    {
        if (transit.LowerMap is not { } lower || !IsGroundLayer(lower))
            return;

        if (!TryComp<CEZPhysicsComponent>(grid, out var zPhys) || zPhys.LocalPosition > PullUpProgress)
            return;

        if (comp.Stage < WGFlightAlarmStage.PullUp)
        {
            SetStage(grid, comp, WGFlightAlarmStage.PullUp);
            comp.NextPullUp = _timing.CurTime + PullUpRepeat;
            return;
        }

        if (_timing.CurTime < comp.NextPullUp)
            return;

        comp.NextPullUp = _timing.CurTime + PullUpRepeat;
        Callout(grid, AlertPullUp);
    }

    /// <summary>Advances the callout stage, never backwards, and announces it.</summary>
    private void SetStage(EntityUid grid, WGLiftLostComponent comp, WGFlightAlarmStage stage)
    {
        if (stage <= comp.Stage)
            return;

        comp.Stage = stage;
        Dirty(grid, comp);

        Callout(grid, CodeFor(stage));
    }

    private static string CodeFor(WGFlightAlarmStage stage)
    {
        return stage switch
        {
            WGFlightAlarmStage.DontSink => AlertDontSink,
            WGFlightAlarmStage.SinkRate => AlertSinkRate,
            WGFlightAlarmStage.Terrain => AlertTerrain,
            WGFlightAlarmStage.TooLowTerrain => AlertTooLowTerrain,
            WGFlightAlarmStage.PullUp => AlertPullUp,
            _ => AlertLiftLost,
        };
    }

    /// <summary>Sets the situation code and announces it, even if the code is unchanged.</summary>
    private void Callout(EntityUid grid, ProtoId<ShipAlertCodePrototype> code)
    {
        if (!_proto.TryIndex(code, out var proto))
            return;

        _alert.SetCode(grid, code, announce: false);
        var broadcast = _pa.Announce(grid,
            Loc.GetString(proto.Announcement, ("ship", _pa.GetShipName(grid))),
            proto.Sound,
            color: proto.Color);

        // Voice fallback when the PA is down; the lift-lost alarm has its own loop.
        if (!broadcast && code.Id != AlertLiftLost && proto.Sound != null)
            _audio.PlayGlobal(proto.Sound, _audience.Aboard(grid), true);

    }

    /// <summary>Applies one layer of glide: the hull keeps its heading and gains speed.</summary>
    private void Glide(EntityUid grid, WGLiftLostComponent comp)
    {
        if (!TryComp<PhysicsComponent>(grid, out var body))
            return;

        var velocity = body.LinearVelocity;

        // Fall back on the remembered glide if the transit hop lost momentum.
        if (!IsGlide(velocity))
            velocity = comp.Glide;

        if (!IsGlide(velocity))
            return;

        velocity *= 1f + GlideGainPerLayer;

        _physics.SetLinearVelocity(grid, velocity, body: body);

        comp.Glide = velocity;
    }

    /// <summary>True for a finite, non-negligible velocity.</summary>
    private static bool IsGlide(Vector2 velocity)
    {
        var lengthSquared = velocity.LengthSquared();

        return float.IsFinite(lengthSquared) && lengthSquared >= 0.0001f;
    }

    private bool IsGroundLayer(EntityUid map)
    {
        return HasComp<CEZGroundLayerComponent>(map)
               || (TryComp<CEZMapComponent>(map, out var zMap) && zMap.Depth == 0);
    }
}
