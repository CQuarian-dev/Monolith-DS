using System.Linq;
using System.Numerics;

namespace Content.Server.NPC.Pathfinding;

/// <summary>
/// A path asked for across planet ground nobody has been near: the unbuilt chunks along the way are built first, nearest
/// the start first, and the request waits for them instead of finding no way.
/// </summary>
public sealed partial class PathfindingSystem
{
    /// <summary>How long a request waits for its ground before it runs on whatever is built.</summary>
    private static readonly TimeSpan WgGroundWait = TimeSpan.FromSeconds(10);

    /// <summary>How long a chunk stays wanted after the request that wanted it was answered.</summary>
    private static readonly TimeSpan WgWantedFor = TimeSpan.FromSeconds(15);

    /// <summary>Chunks either side of the straight line to the target that are built for a request.</summary>
    private const int WgCorridor = 2;

    /// <summary>How far along the line, in metres, a request has the ground built.</summary>
    private const float WgCorridorLength = 640f;

    /// <summary>Chunks wanted by path requests, per grid, with the squared distance from the request's start.</summary>
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, (float Distance, TimeSpan Until)>> _wgWanted = new();

    /// <summary>Requests waiting on unbuilt ground: the grid, the chunks along the way, and when they give up waiting.</summary>
    private readonly Dictionary<PathRequest, (EntityUid Grid, List<Vector2i> Chunks, TimeSpan Until)> _wgWaiting = new();

    /// <summary>Requests held back this update; read from the parallel path loop.</summary>
    private readonly HashSet<PathRequest> _wgHeld = new();

    private readonly HashSet<Vector2i> _wgCorridor = new();

    /// <summary>
    /// Holds back the requests about to run whose way crosses unbuilt planet ground, and has that ground built. Called
    /// on the main thread, before the parallel loop.
    /// </summary>
    private void WgHoldForGround(int amount)
    {
        _wgHeld.Clear();
        WgBindCVars();
        if (!_wgLazy)
        {
            _wgWaiting.Clear();
            return;
        }

        var now = _timing.CurTime;
        for (var i = 0; i < amount; i++)
        {
            if (_pathRequests[i] is not AStarPathRequest request || request.Started || request.Task.IsCompleted)
                continue;

            if (!_wgWaiting.TryGetValue(request, out var waiting))
            {
                if (!WgWantGround(request, now, out waiting))
                    continue;
                _wgWaiting[request] = waiting;
            }

            if (now < waiting.Until && TryComp<GridPathfindingComponent>(waiting.Grid, out var comp) && WgAnyDirty(comp, waiting.Chunks))
            {
                _wgHeld.Add(request);
                continue;
            }

            _wgWaiting.Remove(request);
        }

        // Answered or cancelled elsewhere.
        if (_wgWaiting.Count > _wgHeld.Count)
        {
            foreach (var request in _wgWaiting.Keys.ToList())
            {
                if (request.Task.IsCompleted)
                    _wgWaiting.Remove(request);
            }
        }
    }

    /// <summary>Whether the parallel loop leaves this request for a later update.</summary>
    private bool WgHeld(PathRequest request)
    {
        return _wgHeld.Count > 0 && _wgHeld.Contains(request);
    }

    private static bool WgAnyDirty(GridPathfindingComponent comp, List<Vector2i> chunks)
    {
        foreach (var origin in chunks)
        {
            if (comp.DirtyChunks.Contains(origin))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Marks the unbuilt chunks along a request's straight line as wanted; false when it is not on a lazily built
    /// planet layer or nothing on its way is unbuilt.
    /// </summary>
    private bool WgWantGround(AStarPathRequest request, TimeSpan now, out (EntityUid Grid, List<Vector2i> Chunks, TimeSpan Until) waiting)
    {
        waiting = default;
        if (_transform.GetGrid(request.Start) is not { } grid
            || _transform.GetGrid(request.End) != grid
            || !WgOnPlanet(grid)
            || !TryComp<GridPathfindingComponent>(grid, out var comp)
            || comp.DirtyChunks.Count == 0)
            return false;

        var invMatrix = _transform.GetInvWorldMatrix(grid);
        var start = Vector2.Transform(_transform.ToMapCoordinates(request.Start).Position, invMatrix);
        var end = Vector2.Transform(_transform.ToMapCoordinates(request.End).Position, invMatrix);
        var line = end - start;
        var length = MathF.Min(line.Length(), WgCorridorLength);
        var direction = line.LengthSquared() > 0.0001f ? Vector2.Normalize(line) : Vector2.Zero;

        if (!_wgWanted.TryGetValue(grid, out var wanted))
            _wgWanted[grid] = wanted = new Dictionary<Vector2i, (float, TimeSpan)>();

        _wgCorridor.Clear();
        var chunks = new List<Vector2i>();
        var until = now + WgGroundWait + WgWantedFor;
        for (var along = 0f; ; along += ChunkSize / 2f)
        {
            along = MathF.Min(along, length);
            var point = start + direction * along;
            var centre = new Vector2i((int) MathF.Floor(point.X / ChunkSize), (int) MathF.Floor(point.Y / ChunkSize));
            for (var x = -WgCorridor; x <= WgCorridor; x++)
            for (var y = -WgCorridor; y <= WgCorridor; y++)
            {
                var origin = centre + new Vector2i(x, y);
                if (!_wgCorridor.Add(origin) || !comp.DirtyChunks.Contains(origin))
                    continue;

                chunks.Add(origin);
                var distance = along * along;
                if (!wanted.TryGetValue(origin, out var known) || known.Distance > distance || known.Until < until)
                    wanted[origin] = (MathF.Min(distance, known.Until > now ? known.Distance : distance), until);
            }

            if (along >= length)
                break;
        }

        if (chunks.Count == 0)
            return false;

        comp.NextUpdate = now;
        waiting = (grid, chunks, now + WgGroundWait);
        return true;
    }

    /// <summary>Adds the still-unbuilt chunks path requests want to the candidates; forgets the stale ones.</summary>
    private void WgAddWanted(EntityUid grid, GridPathfindingComponent comp)
    {
        if (!_wgWanted.TryGetValue(grid, out var wanted))
            return;

        var now = _timing.CurTime;
        foreach (var (origin, (distance, until)) in wanted)
        {
            if (until < now || !comp.DirtyChunks.Contains(origin))
            {
                wanted.Remove(origin);
                continue;
            }

            if (_wgPicked.Add(origin))
                _wgNear.Add((distance, origin));
        }

        if (wanted.Count == 0)
            _wgWanted.Remove(grid);
    }
}
