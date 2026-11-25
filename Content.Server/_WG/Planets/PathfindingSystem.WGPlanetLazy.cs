using System.Numerics;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Content.Shared.NPC;
using Robust.Shared.Configuration;

namespace Content.Server.NPC.Pathfinding;

/// <summary>
/// Builds a planet layer's navmesh only where someone is: round players and awake NPCs, and the way of a path asked
/// for, a slice an update, nearest first. A preloaded world dirties every chunk it has, and upstream built them all in one tick and kept them all.
/// </summary>
// What isn't built stays dirty and is built when someone comes near, or a path is asked for across it (WGPlanetPathWake).
public sealed partial class PathfindingSystem
{
    [Dependency] private IConfigurationManager _wgCfg = default!;

    /// <summary>Seconds before an unbuilt planet chunk with nobody near is looked at again.</summary>
    private static readonly TimeSpan WgIdleRecheck = TimeSpan.FromSeconds(1);

    private readonly List<Vector2> _wgInterest = new();
    private readonly List<(Vector2i Chunk, Vector2 Position)> _wgInterestChunks = new();
    private readonly Dictionary<Vector2i, float> _wgNearest = new();

    private bool _wgLazy;
    private float _wgRange;
    private int _wgChunkBudget;
    private bool _wgCVarsBound;
    private readonly List<(float Distance, Vector2i Origin)> _wgNear = new();
    private readonly List<Vector2i> _wgBuild = new();
    private readonly HashSet<Vector2i> _wgPicked = new();

    /// <summary>
    /// The dirty chunks of a grid to build this update: all of them, except on or over a planet layer, where only those
    /// near someone go, at most <see cref="PlanetCVars.PathfindingChunkBudget"/> of them.
    /// </summary>
    private List<Vector2i> WgChunksToBuild(EntityUid grid, GridPathfindingComponent comp)
    {
        _wgBuild.Clear();
        WgBindCVars();

        if (!WgOnPlanet(grid) || !_wgLazy)
        {
            _wgBuild.AddRange(comp.DirtyChunks);
            return _wgBuild;
        }

        WgInterest(grid);
        _wgNear.Clear();
        _wgPicked.Clear();

        if (_wgInterest.Count > 0 && comp.DirtyChunks.Count > 0)
            WgNearDirty(comp);

        WgAddWanted(grid, comp);

        if (_wgNear.Count == 0)
        {
            comp.NextUpdate = _timing.CurTime + WgIdleRecheck;
            return _wgBuild;
        }

        _wgNear.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        var budget = Math.Max(1, _wgChunkBudget);
        for (var i = 0; i < _wgNear.Count && i < budget; i++)
        {
            _wgBuild.Add(_wgNear[i].Origin);
        }

        return _wgBuild;
    }

    /// <summary>
    /// Fills <see cref="_wgNear"/> with the dirty chunks in range of an interest point, by looking only at the chunks
    /// round each one's own chunk instead of every dirty chunk.
    /// </summary>
    private void WgNearDirty(GridPathfindingComponent comp)
    {
        var rangeSq = _wgRange * _wgRange;
        // A chunk centre is at most half a chunk further than the chunk edge nearest the point.
        var reach = (int) MathF.Ceiling((_wgRange + ChunkSize / 2f) / ChunkSize);
        var half = new Vector2(ChunkSize / 2f);

        _wgInterestChunks.Clear();
        foreach (var pos in _wgInterest)
        {
            var chunk = new Vector2i((int) MathF.Floor(pos.X / ChunkSize), (int) MathF.Floor(pos.Y / ChunkSize));
            _wgInterestChunks.Add((chunk, pos));
        }

        _wgInterestChunks.Sort(static (a, b) => a.Chunk.X != b.Chunk.X ? a.Chunk.X.CompareTo(b.Chunk.X) : a.Chunk.Y.CompareTo(b.Chunk.Y));
        _wgNearest.Clear();

        var start = 0;
        while (start < _wgInterestChunks.Count)
        {
            var chunk = _wgInterestChunks[start].Chunk;
            var end = start + 1;
            while (end < _wgInterestChunks.Count && _wgInterestChunks[end].Chunk == chunk)
            {
                end++;
            }

            for (var x = chunk.X - reach; x <= chunk.X + reach; x++)
            for (var y = chunk.Y - reach; y <= chunk.Y + reach; y++)
            {
                var origin = new Vector2i(x, y);
                if (!comp.DirtyChunks.Contains(origin))
                    continue;

                var centre = origin * ChunkSize + half;
                var nearest = float.MaxValue;
                for (var i = start; i < end; i++)
                {
                    nearest = MathF.Min(nearest, Vector2.DistanceSquared(centre, _wgInterestChunks[i].Position));
                }

                if (nearest > rangeSq)
                    continue;

                if (!_wgNearest.TryGetValue(origin, out var known) || nearest < known)
                    _wgNearest[origin] = nearest;
            }

            start = end;
        }

        foreach (var (origin, distance) in _wgNearest)
        {
            _wgNear.Add((distance, origin));
            _wgPicked.Add(origin);
        }
    }

    /// <summary>Reads the lazy navmesh cvars into fields, once.</summary>
    private void WgBindCVars()
    {
        if (_wgCVarsBound)
            return;

        _wgCVarsBound = true;
        Subs.CVar(_wgCfg, PlanetCVars.PathfindingLazy, v => _wgLazy = v, true);
        Subs.CVar(_wgCfg, PlanetCVars.PathfindingRange, v => _wgRange = v, true);
        Subs.CVar(_wgCfg, PlanetCVars.PathfindingChunkBudget, v => _wgChunkBudget = v, true);
    }

    /// <summary>Drops what was built from the dirty set; the rest waits, and is looked at again next update.</summary>
    private void WgClearBuilt(GridPathfindingComponent comp, List<Vector2i> built)
    {
        if (built.Count == comp.DirtyChunks.Count)
        {
            comp.DirtyChunks.Clear();
            return;
        }

        foreach (var origin in built)
        {
            comp.DirtyChunks.Remove(origin);
        }
    }

    /// <summary>Whether a grid is a planet layer's ground or stands on one, such as a city's upper level or a wreck.</summary>
    private bool WgOnPlanet(EntityUid grid)
    {
        return HasComp<WGPlanetLayerComponent>(grid)
            || TryComp(grid, out TransformComponent? xform) && HasComp<WGPlanetLayerComponent>(xform.MapUid);
    }

    /// <summary>Where on a grid's map players and awake NPCs stand, in the grid's own frame.</summary>
    private void WgInterest(EntityUid grid)
    {
        _wgInterest.Clear();
        if (!TryComp(grid, out TransformComponent? gridXform) || gridXform.MapUid is not { } map)
            return;

        var invMatrix = _transform.GetInvWorldMatrix(gridXform);
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is { } body
                && TryComp(body, out TransformComponent? xform)
                && xform.MapUid == map)
                _wgInterest.Add(Vector2.Transform(_transform.GetWorldPosition(xform), invMatrix));
        }

        var npcs = EntityQueryEnumerator<ActiveNPCComponent, TransformComponent>();
        while (npcs.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == map)
                _wgInterest.Add(Vector2.Transform(_transform.GetWorldPosition(xform), invMatrix));
        }
    }
}
