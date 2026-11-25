using System.Diagnostics.CodeAnalysis;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._NF.Shuttles.Components;
using Content.Server._WG.Planets.Flight;
using Content.Server.Shuttles.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets.Flight;
using Content.Shared._WG.Planets;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>Planet liftoff intent layered over CE's vertical input, takeoff spool and transit.</summary>
public sealed partial class CEZLevelsSystem
{
    private static readonly TimeSpan WGLiftoffInputPopupCooldown = TimeSpan.FromSeconds(4);

    private readonly Dictionary<EntityUid, TimeSpan> _wgNextLiftoffInputPopup = new();
    private readonly List<(EntityUid Grid, WGLiftoffComponent Liftoff)> _wgLiftoffQueue = new();

    /// <summary>Whether the console offers liftoff; only ground contact, so a blocked press can say why.</summary>
    public bool WgCanOfferLiftoff(EntityUid grid)
    {
        return TryComp<MapGridComponent>(grid, out var gridComp)
               && Transform(grid).MapUid is { } map
               && HasComp<WGPlanetLayerComponent>(map)
               && HasGroundUnderFootprint((grid, gridComp), map);
    }

    /// <summary>Shared ascent gate: <see cref="WgLiftoffHold"/>, then power, then lift.</summary>
    public bool WgCanLiftoff(EntityUid grid, [NotNullWhen(false)] out string? reason)
    {
        if (WgLiftoffHold(grid) is { } hold)
        {
            reason = hold;
            return false;
        }

        WgGetAtmospherePower(grid, out _, out var powerDeficit);
        if (powerDeficit)
        {
            reason = Loc.GetString("wg-liftoff-power-deficit");
            return false;
        }

        if (!WgTryGetLiftRatio(grid, out var ratio) || ratio < WGFullLiftRatio)
        {
            reason = Loc.GetString("wg-liftoff-insufficient-lift", ("percent", WgLiftPercent(ratio)));
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Lift ratio as a whole percentage for player text.</summary>
    public static int WgLiftPercent(float ratio)
    {
        return float.IsFinite(ratio) ? (int) MathF.Floor(ratio * 100f) : 999;
    }

    /// <summary>
    /// Why the hull can't leave the ground regardless of lift and power, or null when nothing holds it.
    /// Raises <see cref="WGLiftoffAttemptEvent"/> on each transit-set member.
    /// </summary>
    public string? WgLiftoffHold(EntityUid grid)
    {

        if (TerminatingOrDeleted(grid) || !TryComp<MapGridComponent>(grid, out _))
            return Loc.GetString("wg-liftoff-no-hull");

        if (!WgIsFlyableGrid(grid))
            return Loc.GetString("wg-liftoff-refused-ground");

        var transitSet = CollectTransitSet(grid);
        string? eventReason = null;

        foreach (var member in transitSet)
        {
            var attempt = new WGLiftoffAttemptEvent();
            RaiseLocalEvent(member, ref attempt);

            if (attempt.Cancelled && eventReason is null)
                eventReason = attempt.Reason ?? Loc.GetString("wg-liftoff-blocked");
        }

        if (eventReason is not null)
            return eventReason;

        // A camp's or wreck's hull is part of the world now: its helm still opens, but it never leaves.
        foreach (var member in transitSet)
        {
            if (HasComp<Content.Shared._WG.PlanetCities.WGPlanetCityGridComponent>(member))
                return Loc.GetString("wg-liftoff-settled");
        }

        if (!WgIsPlanetFlight(grid))
            return Loc.GetString("wg-liftoff-not-planet");

        if (HasComp<FTLComponent>(grid) || HasComp<Content.Server._WG.Planets.Hop.WGPlanetHopComponent>(grid))
            return Loc.GetString("wg-flight-busy");

        foreach (var member in transitSet)
        {
            if (HasComp<ForceAnchorComponent>(member))
                return Loc.GetString("wg-liftoff-force-anchored");
        }

        return null;
    }

    /// <summary>Validates and latches a grounded button request.</summary>
    public bool WgTryBeginLiftoff(
        EntityUid grid,
        EntityUid console,
        EntityUid pilot,
        [NotNullWhen(false)] out string? reason)
    {
        if (TerminatingOrDeleted(console)
            || Transform(console).GridUid != grid
            || !TryComp<PilotComponent>(pilot, out var pilotComp)
            || pilotComp.Console != console)
        {
            reason = Loc.GetString("wg-liftoff-pilot-lost");
            return false;
        }

        if (!WgCanOfferLiftoff(grid))
        {
            reason = Loc.GetString("wg-liftoff-not-grounded");
            return false;
        }

        if (!WgCanLiftoff(grid, out reason))
            return false;

        var liftoff = EnsureComp<WGLiftoffComponent>(grid);
        liftoff.Console = console;
        liftoff.Pilot = pilot;
        return true;
    }

    /// <summary>Cancels an active ascent intent. Airborne CE settling then resumes normally.</summary>
    public void WgCancelLiftoff(EntityUid grid)
    {
        RemComp<WGLiftoffComponent>(grid);
    }

    /// <summary>Descend cancels liftoff; grounded ascend input engages liftoff like the console button.</summary>
    private bool WgHandleLiftoffPilotInput(EntityUid pilot, EntityUid console, EntityUid grid, float vertical)
    {
        if (vertical < 0f && HasComp<WGLiftoffComponent>(grid))
        {
            WgCancelLiftoff(grid);
            return false;
        }

        if (vertical <= 0f || !WgIsPlanetFlight(grid))
            return false;

        if (WgCanOfferLiftoff(grid))
        {
            if (HasComp<WGLiftoffComponent>(grid))
                return true;

            if (WgTryBeginLiftoff(grid, console, pilot, out var refusal))
            {
                _wgFlight.PlayTakeoff(grid);
                return true;
            }

            if (_timing.CurTime >= _wgNextLiftoffInputPopup.GetValueOrDefault(grid))
            {
                _wgNextLiftoffInputPopup[grid] = _timing.CurTime + WGLiftoffInputPopupCooldown;
                _popup.PopupEntity(refusal, console, pilot);
            }

            return true;
        }

        if (WgCanLiftoff(grid, out var reason))
            return false;

        if (_timing.CurTime >= _wgNextLiftoffInputPopup.GetValueOrDefault(grid))
        {
            _wgNextLiftoffInputPopup[grid] = _timing.CurTime + WGLiftoffInputPopupCooldown;
            _popup.PopupEntity(reason, console, pilot);
        }

        return true;
    }

    // Progress up the ground gap that counts as taken off; past the settle zone so the hull drifts on up.
    private const float WGTakeoffProgress = 0.8f;

    /// <summary>True once the hull is off the ground for good: high in the first gap, or anywhere above it.</summary>
    private bool WgHasTakenOff(EntityUid grid, EntityUid map)
    {
        if (HasComp<WGOrbitLayerComponent>(map))
            return true;

        if (TryComp<CEZTransitMapComponent>(map, out var transit))
        {
            if (transit.LowerMap is not { } lower || !WgIsGroundLayer(lower))
                return true;

            return TryComp<CEZPhysicsComponent>(grid, out var zPhysics) && zPhysics.LocalPosition >= WGTakeoffProgress;
        }

        return HasComp<WGPlanetLayerComponent>(map) && !WgIsGroundLayer(map);
    }

    private bool WgIsGroundLayer(EntityUid map)
    {
        return HasComp<CEZGroundLayerComponent>(map) || TryComp<CEZMapComponent>(map, out var zMap) && zMap.Depth == 0;
    }

    /// <summary>Adds every valid latch to CE's ordinary per-grid upward input.</summary>
    private void WgCollectLiftoffInputs()
    {
        _wgLiftoffQueue.Clear();

        var query = EntityQueryEnumerator<WGLiftoffComponent>();
        while (query.MoveNext(out var grid, out var liftoff))
            _wgLiftoffQueue.Add((grid, liftoff));

        foreach (var (grid, liftoff) in _wgLiftoffQueue)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            // Liftoff stops at the first air layer; settling carries the hull the rest of the way.
            if (Transform(grid).MapUid is { } map && WgHasTakenOff(grid, map))
            {
                WgCancelLiftoff(grid);

                // Drop the climb's momentum so it doesn't carry the hull on into orbit.
                if (TryComp<CEZGridFallerComponent>(grid, out var faller))
                    faller.Velocity = 0f;

                continue;
            }

            if (TerminatingOrDeleted(liftoff.Console)
                || Transform(liftoff.Console).GridUid != grid
                || !TryComp<PilotComponent>(liftoff.Pilot, out var pilot)
                || pilot.Console != liftoff.Console)
            {
                WgCancelLiftoff(grid);
                continue;
            }

            if (!WgCanLiftoff(grid, out var reason))
            {
                WgCancelLiftoff(grid);
                _popup.PopupEntity(reason, liftoff.Console, liftoff.Pilot);
                continue;
            }

            _pilotVerticalInput[grid] = Math.Clamp(_pilotVerticalInput.GetValueOrDefault(grid) + 1f, -1f, 1f);
        }
    }
}
