using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

/// <summary>
/// Puts away a preloaded ground's untouched biome entities where nobody is near, keeping its tiles and decals, and
/// lays them again from the recipe when someone comes back. The whole world's ground still exists; only the trees,
/// rocks and plants nobody can see do not.
/// </summary>
// A touched entity, a pinned tile and anything built stay, as the unloader keeps them. Stashed chunks stay in
// LoadedChunks, since their tiles are there; their LoadedEntities entry is gone until they are laid again.
public sealed partial class BiomeSystem
{
    [Dependency] private EntityLookupSystem _wgLookup = default!;

    /// <summary>Per stashed chunk to lay again this pass: how far from its nearest loader, and whether it goes at once.</summary>
    private readonly List<(int Distance, Vector2i Chunk, bool Now)> _wgRestores = new();

    private readonly HashSet<Vector2i> _wgStashKeep = new();
    private readonly HashSet<Vector2i> _wgOccupied = new();
    private readonly HashSet<EntityUid> _wgOccupants = new();
    private EntityUid _wgStashKeepMap;
    private GameTick _wgStashKeepTick;
    private BiomeComponent? _wgRestoreFor;

    /// <summary>Tiles round a landed hull's bounds whose stashed chunks are laid at once.</summary>
    public const float WgRestoreNowPad = 2f;

    /// <summary>Stashed chunks laid at once in one pass, past the load budget; the rest wait for the budget.</summary>
    public const int WgRestoreNowCap = 8;

    /// <summary>The bounds of the hulls landed on <see cref="_wgLandedMap"/>, worked out once a tick.</summary>
    private readonly List<Box2> _wgLanded = new();
    private EntityUid _wgLandedMap;
    private GameTick _wgLandedTick;

    /// <summary>Whether stashing is on for preloaded grounds.</summary>
    public bool WgStashEnabled => _configManager.GetCVar(PlanetCVars.TerrainStash);

    /// <summary>How many of a layer's chunks have their biome entities put away.</summary>
    public int WgStashedChunks(EntityUid layer)
    {
        return _wgLayers.TryGetValue(layer, out var state) ? state.Stashed.Count : 0;
    }

    /// <summary>Whether a chunk of a layer has its biome entities put away.</summary>
    public bool WgIsStashed(EntityUid layer, Vector2i chunkOrigin)
    {
        return _wgLayers.TryGetValue(layer, out var state) && state.Stashed.Contains(chunkOrigin);
    }

    /// <summary>
    /// Puts away a loaded chunk's untouched biome entities if nobody is near it and nothing built stands beside it;
    /// false when it stays as it is.
    /// </summary>
    public bool WgStashIfIdle(Entity<BiomeComponent, MapGridComponent> ground, Vector2i chunkOrigin)
    {
        if (!ground.Comp1.LoadedEntities.ContainsKey(chunkOrigin))
            return false;

        WgRefreshStashKeep(ground.Owner);
        if (_wgStashKeep.Contains(chunkOrigin))
            return false;

        return WgStashChunk(ground.Comp1, ground.Owner, ground.Comp2, chunkOrigin, checkTiles: false);
    }

    /// <summary>Pins every tile of a chunk that is no longer the one the biome grows there.</summary>
    private void WgPinChangedTiles(BiomeComponent biome, EntityUid map, MapGridComponent grid, Vector2i chunk, HashSet<Vector2i> modified)
    {
        for (var x = chunk.X; x < chunk.X + ChunkSize; x++)
        for (var y = chunk.Y; y < chunk.Y + ChunkSize; y++)
        {
            var tile = new Vector2i(x, y);
            if (modified.Contains(tile))
                continue;

            if (!TryGetBiomeTile(tile, biome.Layers, biome.Seed, null, out var grown)
                || !_mapSystem.TryGetTileRef(map, grid, tile, out var current)
                || current.Tile != grown.Value)
                modified.Add(tile);
        }
    }

    /// <summary>What near players and under hulls keeps its entities, worked out once a tick per layer.</summary>
    private void WgRefreshStashKeep(EntityUid map)
    {
        if (_wgStashKeepMap == map && _wgStashKeepTick == _wgTiming.CurTick)
            return;

        _wgStashKeepMap = map;
        _wgStashKeepTick = _wgTiming.CurTick;
        _wgStashKeep.Clear();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } body
                || !_xformQuery.TryGetComponent(body, out var xform)
                || xform.MapUid != map)
                continue;

            var area = _loadArea.Enlarged(WgKeepRing * ChunkSize).Translated(_transform.GetWorldPosition(xform));
            var chunks = new ChunkIndicesEnumerator(area, ChunkSize);
            while (chunks.MoveNext(out var index))
            {
                _wgStashKeep.Add(index.Value * ChunkSize);
            }
        }

        // WgKeepUnderHulls fills the unloader's own set; borrowed here and handed back empty.
        _wgKeep.Clear();
        WgKeepUnderHulls(map);
        _wgStashKeep.UnionWith(_wgKeep);
        _wgKeep.Clear();
    }

    /// <summary>Puts away one chunk's untouched biome entities; a room beside it keeps them, as it keeps its walls.</summary>
    private bool WgStashChunk(BiomeComponent biome, EntityUid map, MapGridComponent grid, Vector2i chunk, bool checkTiles = true)
    {
        if (_atmos.WgHasBuiltTile(map, chunk - Vector2i.One, chunk + new Vector2i(ChunkSize, ChunkSize)))
            return false;

        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= new HashSet<Vector2i>();

        try
        {
            WgUnloadEntities(biome, map, grid, chunk, modified);

            // Ground changed since it grew, dug out or floored over, grows nothing when the rest comes back. The
            // preload's own stash skips this: what it laid over the biome's ground is pinned already.
            if (checkTiles)
                WgPinChangedTiles(biome, map, grid, chunk, modified);
        }
        finally
        {
            if (modified.Count == 0)
                biome.ModifiedTiles.Remove(chunk);
            else
                biome.ModifiedTiles[chunk] = modified;
        }

        var state = _wgLayers.GetOrNew(map);
        state.Stashed.Add(chunk);
        state.IdleSince.Remove(chunk);
        state.Queued.Remove(chunk);
        return true;
    }

    /// <summary>Notes a stashed chunk a loader wants back; it is laid in <see cref="WgRestoreStashed"/>.</summary>
    private void WgNoteRestore(BiomeComponent biome, EntityUid map, Vector2i chunk)
    {
        if (!_wgLayers.TryGetValue(map, out var state) || !state.Stashed.Contains(chunk))
            return;

        if (!ReferenceEquals(_wgRestoreFor, biome))
        {
            _wgRestoreFor = biome;
            _wgRestores.Clear();
        }

        if (_wgLoadLoaders == null)
        {
            _wgRestores.Add((0, chunk, true));
            return;
        }

        var block = WgBlockOf(chunk);
        var now = _wgNearLoaderBlocks.Contains(block) || WgUnderLandedHull(map, chunk);
        _wgRestores.Add((WgBlockDistance(block, _wgLoadLoaders), chunk, now));
    }

    /// <summary>Whether a chunk lies under a hull standing on this layer, or within a tile or two of one.</summary>
    private bool WgUnderLandedHull(EntityUid map, Vector2i chunk)
    {
        if (_wgLandedMap != map || _wgLandedTick != _wgTiming.CurTick)
        {
            _wgLandedMap = map;
            _wgLandedTick = _wgTiming.CurTick;
            _wgLanded.Clear();

            var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (grids.MoveNext(out var uid, out var hull, out var xform))
            {
                if (uid == map || xform.MapUid != map || WgSkipHull(uid))
                    continue;

                _wgLanded.Add(_transform.GetWorldMatrix(xform).TransformBox(hull.LocalAABB).Enlarged(WgRestoreNowPad));
            }
        }

        if (_wgLanded.Count == 0)
            return false;

        var box = new Box2(chunk, chunk + new Vector2i(ChunkSize, ChunkSize));
        foreach (var bounds in _wgLanded)
        {
            if (bounds.Intersects(box))
                return true;
        }

        return false;
    }

    /// <summary>Lays what a layer's loaders want back: those beside them at once, the rest nearest first within the pass's budget.</summary>
    private void WgRestoreStashed(BiomeComponent biome, EntityUid map, MapGridComponent grid)
    {
        if (!ReferenceEquals(_wgRestoreFor, biome) || _wgRestores.Count == 0)
            return;

        _wgRestoreFor = null;
        _wgRestores.Sort(static (a, b) =>
        {
            var order = b.Now.CompareTo(a.Now);
            if (order == 0)
                order = a.Distance.CompareTo(b.Distance);
            if (order == 0)
                order = a.Chunk.X.CompareTo(b.Chunk.X);
            return order != 0 ? order : a.Chunk.Y.CompareTo(b.Chunk.Y);
        });

        try
        {
            var laid = 0;
            var laidNow = 0;
            foreach (var (_, chunk, now) in _wgRestores)
            {
                var atOnce = now && (_wgLoadBudgetMs <= 0 || laidNow < WgRestoreNowCap);
                if (!atOnce && laid > 0 && _wgLoadSpentMs >= _wgLoadBudgetMs)
                    break;

                if (atOnce)
                    laidNow++;

                _wgLoadWatch.Restart();
                WgRestoreChunk(biome, map, grid, chunk);
                _wgLoadSpentMs += _wgLoadWatch.Elapsed.TotalMilliseconds;
                laid++;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Laying stashed chunks of {ToPrettyString(map)} again failed. {e}");
        }
        finally
        {
            _wgRestores.Clear();
        }
    }

    /// <summary>Lays a stashed chunk's biome entities again, round whoever stands where one would go.</summary>
    private void WgRestoreChunk(BiomeComponent biome, EntityUid map, MapGridComponent grid, Vector2i chunk)
    {
        if (!_wgLayers.TryGetValue(map, out var state) || !state.Stashed.Remove(chunk) || biome.LoadedEntities.ContainsKey(chunk))
            return;

        biome.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();

        // Someone who walked onto a stashed tile isn't walled in: the tile goes without, this time only.
        WgOccupiedTiles(map, grid, chunk);
        _wgOccupied.ExceptWith(modified);
        modified.UnionWith(_wgOccupied);

        try
        {
            LoadEntities(biome, map, grid, chunk, biome.Seed, modified);
        }
        finally
        {
            modified.ExceptWith(_wgOccupied);
            FinalizeChunk(biome, chunk, modified);
        }
    }

    /// <summary>Fills <see cref="_wgOccupied"/> with the chunk's tiles under something solid that moves.</summary>
    private void WgOccupiedTiles(EntityUid map, MapGridComponent grid, Vector2i chunk)
    {
        _wgOccupied.Clear();
        _wgOccupants.Clear();
        var box = new Box2(chunk, chunk + new Vector2i(ChunkSize, ChunkSize));
        _wgLookup.GetLocalEntitiesIntersecting(map, box, _wgOccupants, LookupFlags.Dynamic);

        foreach (var uid in _wgOccupants)
        {
            if (!TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide || !body.Hard
                || !_xformQuery.TryGetComponent(uid, out var xform) || xform.ParentUid != map)
                continue;

            _wgOccupied.Add(_mapSystem.LocalToTile(map, grid, xform.Coordinates));
        }
    }
}
