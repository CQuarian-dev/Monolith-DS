using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Transit collision checks work out each falling grid's docked set once per tick, instead of once for every pair
/// it is checked against.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    private readonly Dictionary<EntityUid, (bool Found, HashSet<EntityUid> Set, Box2 Aabb)> _wgGridSetCache = new();
    private GameTick _wgGridSetCacheTick;

    /// <summary><see cref="TryGetGridSetAabb"/>, kept for the rest of the tick.</summary>
    private bool WGTryGetCachedGridSetAabb(EntityUid primary, out HashSet<EntityUid> set, out Box2 aabb)
    {
        if (_wgGridSetCacheTick != _timing.CurTick)
        {
            _wgGridSetCache.Clear();
            _wgGridSetCacheTick = _timing.CurTick;
        }

        if (!_wgGridSetCache.TryGetValue(primary, out var cached))
        {
            var found = TryGetGridSetAabb(primary, out var fresh, out var freshAabb);
            cached = (found, fresh, freshAabb);
            _wgGridSetCache[primary] = cached;
        }

        set = cached.Set;
        aabb = cached.Aabb;
        // A set crushed earlier this tick is gone.
        return cached.Found && !TerminatingOrDeleted(primary) && !EntityManager.IsQueuedForDeletion(primary);
    }
}
