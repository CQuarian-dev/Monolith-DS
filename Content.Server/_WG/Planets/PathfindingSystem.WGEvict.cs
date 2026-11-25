using System.Numerics;
using Prometheus;
using Robust.Shared.Configuration;

namespace Content.Server.NPC.Pathfinding;

/// <summary>Server cvars for freeing planet navmesh nobody is near.</summary>
[CVarDefs]
public sealed class WGPathfindingEvictCVars
{
    /// <summary>
    /// Seconds a built planet navmesh chunk can go with nobody near before it is freed, to be built again when someone
    /// comes or a path is asked for across it; 0 keeps every chunk. Chunks built wherever anyone went were kept all round.
    /// </summary>
    public static readonly CVarDef<float> IdleSeconds =
        CVarDef.Create("wg.pathfinding.evict_idle_seconds", 300f, CVar.SERVERONLY);
}

/// <summary>
/// Frees built navmesh chunks on planet layers that nobody has been near for <see cref="WGPathfindingEvictCVars.IdleSeconds"/>.
/// One grid is swept at a time, a slice of chunks a tick, against where players and awake NPCs stood when its sweep began.
/// </summary>
public sealed partial class PathfindingSystem
{
    /// <summary>Chunks looked at a tick.</summary>
    private const int WgEvictPerTick = 256;

    private static readonly Gauge WgPlanetChunks = Metrics.CreateGauge(
        "wg_pathfinding_planet_chunks",
        "Built navmesh chunks on planet layers, counted over the last full sweep");

    private readonly Queue<EntityUid> _wgEvictGrids = new();
    private EntityUid _wgEvictGrid;
    private readonly List<Vector2i> _wgEvictOrigins = new();
    private int _wgEvictIndex;
    private readonly List<Vector2> _wgEvictInterest = new();
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, TimeSpan>> _wgChunkSeen = new();
    private int _wgSweepChunks;

    /// <summary>Looks at the next slice of the grid being swept and frees the chunks idle long enough.</summary>
    private void WgEvictIdleChunks()
    {
        WgBindCVars();
        var idle = _wgCfg.GetCVar(WGPathfindingEvictCVars.IdleSeconds);
        // Only lazily built ground rebuilds a freed chunk on demand; elsewhere a dirty chunk is rebuilt at once.
        if (idle <= 0f || !_wgLazy)
            return;

        if (_wgEvictIndex >= _wgEvictOrigins.Count && !WgNextEvictGrid())
            return;

        if (!TryComp<GridPathfindingComponent>(_wgEvictGrid, out var comp))
        {
            _wgEvictOrigins.Clear();
            return;
        }

        var now = _timing.CurTime;
        var idleFor = TimeSpan.FromSeconds(idle);
        var keep = MathF.Max(_wgRange, WgCorridorLength) + ChunkSize;
        var keepSq = keep * keep;
        var half = new Vector2(ChunkSize / 2f);
        if (!_wgChunkSeen.TryGetValue(_wgEvictGrid, out var seen))
            _wgChunkSeen[_wgEvictGrid] = seen = new Dictionary<Vector2i, TimeSpan>();
        _wgWanted.TryGetValue(_wgEvictGrid, out var wanted);

        var end = Math.Min(_wgEvictOrigins.Count, _wgEvictIndex + WgEvictPerTick);
        for (; _wgEvictIndex < end; _wgEvictIndex++)
        {
            var origin = _wgEvictOrigins[_wgEvictIndex];
            if (!comp.Chunks.TryGetValue(origin, out var chunk))
            {
                seen.Remove(origin);
                continue;
            }

            Vector2 centre = origin * ChunkSize + half;
            var isWanted = wanted != null && wanted.TryGetValue(origin, out var want) && want.Until >= now;
            if (chunk.Portals.Count > 0 || chunk.PortalPolys.Count > 0 || isWanted || WgNearEvictInterest(centre, keepSq))
            {
                seen[origin] = now;
                continue;
            }

            if (!seen.TryGetValue(origin, out var last))
            {
                seen[origin] = now;
                continue;
            }

            if (now - last < idleFor)
                continue;

            WgEvictChunk(comp, origin);
            seen.Remove(origin);
        }
    }

    /// <summary>Starts the sweep of the next planet grid, refilling the queue when it runs out. False if there is none.</summary>
    private bool WgNextEvictGrid()
    {
        if (_wgEvictGrids.Count == 0)
        {
            WgPlanetChunks.Set(_wgSweepChunks);
            _wgSweepChunks = 0;

            var query = AllEntityQuery<GridPathfindingComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (WgOnPlanet(uid))
                    _wgEvictGrids.Enqueue(uid);
            }

            foreach (var grid in new List<EntityUid>(_wgChunkSeen.Keys))
            {
                if (TerminatingOrDeleted(grid))
                    _wgChunkSeen.Remove(grid);
            }
        }

        while (_wgEvictGrids.TryDequeue(out var grid))
        {
            if (!TryComp<GridPathfindingComponent>(grid, out var comp))
                continue;

            WgInterest(grid);
            _wgEvictInterest.Clear();
            _wgEvictInterest.AddRange(_wgInterest);

            _wgEvictGrid = grid;
            _wgEvictOrigins.Clear();
            _wgEvictOrigins.AddRange(comp.Chunks.Keys);
            _wgEvictIndex = 0;
            _wgSweepChunks += comp.Chunks.Count;
            return true;
        }

        return false;
    }

    private bool WgNearEvictInterest(Vector2 centre, float rangeSq)
    {
        foreach (var point in _wgEvictInterest)
        {
            if (Vector2.DistanceSquared(centre, point) <= rangeSq)
                return true;
        }

        return false;
    }

    /// <summary>Whether a grid has a built navmesh chunk at an origin.</summary>
    public bool WgHasChunk(EntityUid grid, Vector2i origin) =>
        TryComp<GridPathfindingComponent>(grid, out var comp) && comp.Chunks.ContainsKey(origin);

    /// <summary>Whether a grid's chunk at an origin is waiting to be built.</summary>
    public bool WgIsDirty(EntityUid grid, Vector2i origin) =>
        TryComp<GridPathfindingComponent>(grid, out var comp) && comp.DirtyChunks.Contains(origin);

    /// <summary>Whether any polygon of one built chunk links to a polygon of another.</summary>
    public bool WgLinked(EntityUid grid, Vector2i from, Vector2i to)
    {
        if (!TryComp<GridPathfindingComponent>(grid, out var comp) || !comp.Chunks.TryGetValue(from, out var chunk))
            return false;

        foreach (var polys in chunk.Polygons)
        {
            foreach (var poly in polys)
            {
                if (!poly.HasNeighbors)
                    continue;

                foreach (var neighbor in poly.Neighbors)
                {
                    if (neighbor.ChunkOrigin == to)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>Frees a grid's built chunk at an origin; see <see cref="WgEvictChunk(GridPathfindingComponent, Vector2i)"/>.</summary>
    public void WgEvictChunk(EntityUid grid, Vector2i origin)
    {
        if (TryComp<GridPathfindingComponent>(grid, out var comp))
            WgEvictChunk(comp, origin);
    }

    /// <summary>
    /// Frees a built chunk as a rebuild clears it: every polygon is taken out of its neighbours, including those in the
    /// chunks around, and marked invalid for any path still holding it. The chunk is left dirty, so it is built again
    /// when someone comes near or a path is asked for across it.
    /// </summary>
    public void WgEvictChunk(GridPathfindingComponent comp, Vector2i origin)
    {
        if (!comp.Chunks.Remove(origin, out var chunk))
            return;

        foreach (var (_, poly) in chunk.PortalPolys)
        {
            ClearPoly(poly);
        }

        chunk.PortalPolys.Clear();
        foreach (var polys in chunk.Polygons)
        {
            foreach (var poly in polys)
            {
                ClearPoly(poly);
            }

            polys.Clear();
        }

        comp.DirtyChunks.Add(origin);
    }
}
