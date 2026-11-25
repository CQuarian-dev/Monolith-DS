using Content.Shared._WG.LandingGuide;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [Dependency] private WGLandingSurveySystem _wgLandingSurvey = default!;

    /// <summary>How long a clear or blocked landing spot is trusted before it is looked at again.</summary>
    private static readonly TimeSpan WGLandingCheckInterval = TimeSpan.FromSeconds(0.5);

    private static readonly TimeSpan WGLandingRefusalCooldown = TimeSpan.FromSeconds(4);

    private readonly Dictionary<EntityUid, (TimeSpan Until, bool Blocked)> _wgLandingChecks = new();
    private readonly Dictionary<EntityUid, TimeSpan> _wgNextLandingRefusal = new();

    /// <summary>
    /// True when a hull setting down gently would come to rest on something solid, such as rock or a building's walls:
    /// it holds just above instead, and its pilots are told to find clear ground. A crash still comes down regardless.
    /// </summary>
    /// <param name="fresh">Look again whatever the last look said: the hull may have slid over something since.</param>
    private bool WgHoldsOffObstacles(EntityUid grid, HashSet<EntityUid> transitSet, EntityUid ground, bool fresh = false)
    {
        if (!fresh && _wgLandingChecks.TryGetValue(grid, out var cached) && _timing.CurTime < cached.Until)
            return cached.Blocked;

        var blocked = false;
        foreach (var member in transitSet)
        {
            if (WgObstacleUnder(member, ground))
            {
                blocked = true;
                break;
            }
        }
        _wgLandingChecks[grid] = (_timing.CurTime + WGLandingCheckInterval, blocked);

        if (blocked && _timing.CurTime >= _wgNextLandingRefusal.GetValueOrDefault(grid))
        {
            _wgNextLandingRefusal[grid] = _timing.CurTime + WGLandingRefusalCooldown;
            var pilots = EntityQueryEnumerator<PilotComponent>();
            while (pilots.MoveNext(out var pilotUid, out var pilot))
            {
                if (pilot.Console is { } console && !TerminatingOrDeleted(console) && transitSet.Contains(Transform(console).GridUid ?? EntityUid.Invalid))
                    _popup.PopupEntity(Loc.GetString("wg-flight-landing-blocked"), console, pilotUid);
            }
        }
        return blocked;
    }

    /// <summary>Whether a hull with lift holds its height over this plane until its pilot holds descend: a planet's ground.</summary>
    private bool WgHoldsOverGround(EntityUid grid, EntityUid plane)
    {
        return WgIsGroundLayer(plane) && WgIsPlanetFlight(grid);
    }

    /// <summary>Whether anything hard and anchored on the ground below stands under one of the hull's real tiles.</summary>
    public bool WgObstacleUnder(EntityUid hull, EntityUid ground)
    {
        return TryComp<MapGridComponent>(hull, out var hullGrid) && TryComp<MapGridComponent>(ground, out var groundGrid)
            && _wgLandingSurvey.ObstacleUnder((hull, hullGrid), (ground, groundGrid));
    }
}
