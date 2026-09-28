// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Radio;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lua.SpaceHazards;

// LuaM: gun fire-rate and weather lookups moved to SharedNebulaEnvironmentSystem, iterators replaced with plain loops
public sealed partial class NebulaEnvironmentSystem : SharedNebulaEnvironmentSystem
{
    private const int MaxParentChecks = 8;
    private static readonly TimeSpan VeilCacheTtl = TimeSpan.FromSeconds(1); // LuaM

    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!; // LuaM
    [Dependency] private SpaceHazardActivitySystem _activity = default!; // LuaM
    private readonly Dictionary<EntityUid, float> _thrustResistance = new();
    private readonly Dictionary<MapId, (TimeSpan BuiltAt, List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> Fields)> _veilCache = new(); // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnFtlAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentStartup>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentShutdown>(OnThrustResistanceChanged);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, EntParentChangedMessage>(OnThrustResistanceMoved);
    }

    public float GetThrustMultiplier(EntityUid gridUid)
    {
        var multiplier = GetThrustMultiplierRaw(gridUid);
        if (multiplier >= 1f)
            return multiplier;

        var resistance = GetGridThrustResistance(gridUid);
        return float.Lerp(multiplier, 1f, resistance);
    }

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentStartup args)
        => _thrustResistance.Clear();

    private void OnThrustResistanceChanged(Entity<NebulaThrustResistanceComponent> ent, ref ComponentShutdown args)
        => _thrustResistance.Clear();

    private void OnThrustResistanceMoved(Entity<NebulaThrustResistanceComponent> ent, ref EntParentChangedMessage args)
        => _thrustResistance.Clear();

    private void OnFtlAttempt(ref ConsoleFTLAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var blockedAtOrigin = BlocksFtl(args.Uid);
        var blockedAtDestination = !blockedAtOrigin && args.Destination is { } destination && IsFtlBlockedAt(destination);
        if (!blockedAtOrigin && !blockedAtDestination)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("nebula-ftl-blocked");
    }

    public bool IsFtlBlockedAt(EntityCoordinates destination)
    {
        var mapCoordinates = _transform.ToMapCoordinates(destination);
        return IsFtlBlockedAt(mapCoordinates.MapId, mapCoordinates.Position);
    }

    public bool IsFtlBlockedAt(MapId mapId, Vector2 worldPosition)
    {
        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || !FieldHasWeather(field, static w => w.BlocksFtl))
                continue;

            var fieldPosition = _transform.GetWorldPosition(xform);
            if (NebulaVeilHelpers.IsInMidZone(field, fieldPosition, worldPosition))
                return true;
        }

        return false;
    }

    private bool FieldHasWeather(AmbientSpaceFieldComponent field, Func<NebulaWeatherPrototype, bool> predicate)
    {
        if (field.Weathers.Count > 0)
        {
            foreach (var weatherId in field.Weathers)
            {
                if (Prototypes.TryIndex(weatherId, out var weather) && predicate(weather))
                    return true;
            }

            return false;
        }

        return field.Weather is { } fallbackId &&
               Prototypes.TryIndex(fallbackId, out var fallback) &&
               predicate(fallback);
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (!args.Cancelled && IsRadioBlocked(args.RadioSource))
            args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (!args.Cancelled && (IsRadioBlocked(args.RadioSource) || IsRadioBlocked(args.RadioReceiver)))
            args.Cancelled = true;
    }

    // LuaM start: presence only ever sits on grids, so check the grid first and walk parents only for protection
    private bool IsRadioBlocked(EntityUid uid)
    {
        if (Count<NebulaPresenceComponent>() == 0 ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid is not { } grid ||
            !HasRadioBlackout(grid))
            return false;

        var current = uid;
        for (var i = 0; i < MaxParentChecks && current.Valid; i++)
        {
            if (HasComp<NebulaRadioProtectedComponent>(current))
                return false;

            if (!TryComp(current, out xform) || !xform.ParentUid.Valid || xform.ParentUid == current)
                break;

            current = xform.ParentUid;
        }

        return true;
    }

    public bool IsHiddenByVeil(EntityUid uid, TransformComponent xform)
    {
        if (HasComp<AmbientSpaceFieldComponent>(uid) || HasComp<SectorCelestialBodyComponent>(uid))
            return false;

        if (HasComp<NebulaVeilTrackedComponent>(uid) || xform.GridUid is { } grid && HasComp<NebulaVeilTrackedComponent>(grid))
            return true;

        return IsInVeil(xform.MapID, _transform.GetWorldPosition(xform));
    }

    public bool IsInVeil(MapId mapId, Vector2 worldPos)
    {
        foreach (var (field, fieldPos) in GetVeilFields(mapId))
        {
            if (NebulaVeilHelpers.IsInMidZone(field, fieldPos, worldPos))
                return true;
        }

        return false;
    }

    private List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> GetVeilFields(MapId mapId)
    {
        var now = _timing.CurTime;
        if (_veilCache.TryGetValue(mapId, out var cached) && now - cached.BuiltAt < VeilCacheTtl)
            return cached.Fields;

        var fields = cached.Fields ?? new List<(AmbientSpaceFieldComponent, Vector2)>();
        fields.Clear();

        foreach (var uid in _activity.ActiveHazards)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field) ||
                !TryComp(uid, out TransformComponent? xform) ||
                xform.MapID != mapId ||
                !FieldHasWeather(field, static w => w.Kind == NebulaWeatherKind.Veil))
                continue;

            fields.Add((field, _transform.GetWorldPosition(xform)));
        }

        _veilCache[mapId] = (now, fields);
        return fields;
    }
    // LuaM end

    private float GetGridThrustResistance(EntityUid gridUid)
    {
        if (_thrustResistance.TryGetValue(gridUid, out var cached))
            return cached;

        var resistance = 0f;
        var query = EntityQueryEnumerator<NebulaThrustResistanceComponent, TransformComponent>();
        while (query.MoveNext(out _, out var component, out var xform))
        {
            if (xform.GridUid == gridUid)
                resistance = MathF.Max(resistance, component.Resistance);
        }

        resistance = Math.Clamp(resistance, 0f, 1f);
        _thrustResistance[gridUid] = resistance;
        return resistance;
    }
}
