using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using ChunkIndicesEnumerator = Robust.Shared.Map.Enumerators.ChunkIndicesEnumerator;

namespace Content.Server.Parallax;

/// <summary>
/// Spreads a planet layer's chunk loading over a few passes. Upstream loads every chunk of a load area in the pass it
/// comes into range: eighty-one when someone arrives, and a strip of fourteen or more each second under anyone flying.
/// </summary>
// What still loads at once: the chunks round every player, fewer round every eye, and the ground and air under any hull. The rest loads nearest
// first, a few milliseconds' worth a pass, in the 16-tile blocks the grid is sent in, so no block is sent twice.
public sealed partial class BiomeSystem
{
    /// <summary>How many chunks round a player's own body load in the pass they come into range, by default.</summary>
    public const int WgLoadNowRing = 0;

    /// <summary>How many tiles round a player's own body load in the pass they come into range, by default.</summary>
    public const float WgLoadNowPad = 2f;

    /// <summary>
    /// How many chunks round an eye load in the pass they come into range. An eye looks down a hole, or at the ground
    /// from the air, where the far part filling in a moment late is little to see, and an eye's first sight of a layer
    /// is where most whole areas get loaded.
    /// </summary>
    public const int WgLoadNowEyeRing = 1;

    /// <summary>
    /// Half the side, in tiles, of the box an eye on a cavern loads: about what it sees through a hole, not
    /// net.pvs_range (the view's whole side) each way.
    /// </summary>
    public const float WgCavernEyeHalf = 24f;

    /// <summary>The side in tiles of the blocks put-off chunks load in: one grid chunk, which is always sent whole.</summary>
    public const int WgLoadBlock = 16;

    private double _wgLoadBudgetMs;
    private double _wgLoadSpentMs;
    private GameTick _wgLoaderTick;
    private GameTick _wgHullTick;

    /// <summary>Per biome: the chunk each of this pass's loaders is in, and how many chunks round it load at once.</summary>
    private readonly Dictionary<BiomeComponent, List<WgLoader>> _wgLoaders = new();

    /// <summary>Per planet network: the bounds of every hull on its layers and in the gaps between them.</summary>
    private readonly Dictionary<NetEntity, List<Box2>> _wgHullBounds = new();

    private readonly Stack<List<WgLoader>> _wgLoaderPool = new();
    private readonly HashSet<Vector2i> _wgNearBlocks = new();

    /// <summary>The blocks round this pass's loaders only, without those under hulls.</summary>
    private readonly HashSet<Vector2i> _wgNearLoaderBlocks = new();
    private readonly List<(int Distance, Vector2i Block, Vector2i Chunk)> _wgPending = new();
    private readonly Stopwatch _wgLoadWatch = new();
    private BiomeComponent? _wgLoadFor;
    private List<WgLoader>? _wgLoadLoaders;
    private GameTick _wgBodySettingsTick;
    private int _wgBodyRing = WgLoadNowRing;
    private float _wgBodyPad = WgLoadNowPad;

    /// <summary>A loader: its chunk, its position, and how much round it loads at once (whole chunks, and tiles).</summary>
    private readonly record struct WgLoader(Vector2i Chunk, Vector2 Pos, int Ring, float Pad);

    /// <summary>Notes a player's body as a loader; only the chunk it stands in and the tiles round it load at once.</summary>
    private void WgNoteBodyLoader(BiomeComponent biome, Vector2 worldPos)
    {
        if (_wgBodySettingsTick != _wgTiming.CurTick)
        {
            _wgBodySettingsTick = _wgTiming.CurTick;
            _wgBodyRing = _configManager.GetCVar(PlanetCVars.TerrainLoadNowRing);
            _wgBodyPad = _configManager.GetCVar(PlanetCVars.TerrainLoadNowPad);
        }

        WgNoteLoader(biome, worldPos, _wgBodyRing, _wgBodyPad);
    }

    /// <summary>Notes where a loader is, and how much round it must load at once, as its load area is added.</summary>
    private void WgNoteLoader(BiomeComponent biome, Vector2 worldPos, int ring, float pad = 0f)
    {
        var tick = _wgTiming.CurTick;

        if (_wgLoaderTick != tick)
        {
            _wgLoaderTick = tick;

            foreach (var list in _wgLoaders.Values)
            {
                list.Clear();
                _wgLoaderPool.Push(list);
            }

            _wgLoaders.Clear();
        }

        if (!_wgLoaders.TryGetValue(biome, out var loaders))
            _wgLoaders[biome] = loaders = _wgLoaderPool.TryPop(out var pooled) ? pooled : new List<WgLoader>();

        loaders.Add(new WgLoader(SharedMapSystem.GetChunkIndices(worldPos, ChunkSize) * ChunkSize, worldPos, ring, pad));
    }

    /// <summary>Whether a chunk of a planet layer's load area is put off to <see cref="WgLoadDeferred"/>, or never loads at all.</summary>
    private bool WgDeferLoad(BiomeComponent biome, EntityUid map, Vector2i chunk)
    {
        // Outside a bounded world's circle there is no ground.
        if (WgOutOfBounds(map, chunk))
            return true;

        if (!ReferenceEquals(_wgLoadFor, biome))
            WgBeginLoad(biome, map);

        // A stashed chunk's tiles are there; its entities are laid again after the pass's loads.
        if (biome.LoadedChunks.Contains(chunk))
        {
            WgNoteRestore(biome, map, chunk);
            return false;
        }

        if (_wgLoadLoaders == null)
            return false;

        var block = WgBlockOf(chunk);

        if (_wgNearBlocks.Contains(block))
            return false;

        _wgPending.Add((WgBlockDistance(block, _wgLoadLoaders), block, chunk));
        return true;
    }

    /// <summary>The box an eye loads round it, or null for the usual load area: smaller on a planet's cavern.</summary>
    private Box2? WgEyeLoadArea(TransformComponent xform)
    {
        if (xform.MapUid is not { } map
            || !HasComp<WGPlanetLayerComponent>(map)
            || !TryComp<CEZMapComponent>(map, out var level)
            || level.Depth >= 0)
            return null;

        return new Box2(-WgCavernEyeHalf, -WgCavernEyeHalf, WgCavernEyeHalf, WgCavernEyeHalf);
    }

    /// <summary>Works out which blocks of a layer load at once this pass: those round its loaders and under hulls.</summary>
    private void WgBeginLoad(BiomeComponent biome, EntityUid map)
    {
        _wgLoadFor = biome;
        _wgLoadLoaders = null;
        _wgPending.Clear();
        _wgNearBlocks.Clear();
        _wgNearLoaderBlocks.Clear();

        if (_wgLoadBudgetMs <= 0
            || _wgLoaderTick != _wgTiming.CurTick
            || !_wgLoaders.TryGetValue(biome, out var loaders)
            || loaders.Count == 0
            || !TryComp<WGPlanetLayerComponent>(map, out var layer))
            return;

        try
        {
            foreach (var (loader, pos, ring, pad) in loaders)
            {
                for (var x = -ring; x <= ring; x++)
                for (var y = -ring; y <= ring; y++)
                {
                    _wgNearBlocks.Add(WgBlockOf(loader + new Vector2i(x, y) * ChunkSize));
                }

                // The blocks within a few steps of a body: ground it could walk onto before the next pass.
                if (pad > 0f)
                {
                    var blocks = new ChunkIndicesEnumerator(Box2.CenteredAround(pos, new Vector2(pad * 2f)), WgLoadBlock);

                    while (blocks.MoveNext(out var index))
                    {
                        _wgNearBlocks.Add(index.Value * WgLoadBlock);
                    }
                }
            }

            _wgNearLoaderBlocks.UnionWith(_wgNearBlocks);

            // A hull's riders load its ground through eyes that follow it a second late, and it lands on what is there.
            // None goes below the ground, so a cavern has no hull to hurry for.
            if (layer.Network is { } network
                && !(TryComp<CEZMapComponent>(map, out var level) && level.Depth < 0)
                && WgHullBounds().TryGetValue(network, out var hulls))
            {
                foreach (var bounds in hulls)
                {
                    var blocks = new ChunkIndicesEnumerator(bounds, WgLoadBlock);

                    while (blocks.MoveNext(out var index))
                    {
                        _wgNearBlocks.Add(index.Value * WgLoadBlock);
                    }
                }
            }

            _wgLoadLoaders = loaders;
        }
        catch (Exception e)
        {
            // Thrown out of Update, this would stop every biome loading; the layer loads all at once instead.
            Log.Error($"Putting off chunk loads for {ToPrettyString(map)} failed. {e}");
        }
    }

    /// <summary>Loads what a layer put off this pass, nearest block first, until the pass's time is spent; one block always loads.</summary>
    private void WgLoadDeferred(BiomeComponent biome, EntityUid map, MapGridComponent grid, int seed)
    {
        WgRestoreStashed(biome, map, grid);

        if (!ReferenceEquals(_wgLoadFor, biome) || _wgLoadLoaders == null)
            return;

        if (_wgPending.Count == 0)
            return;

        _wgPending.Sort(static (a, b) =>
        {
            var order = a.Distance.CompareTo(b.Distance);
            if (order == 0)
                order = a.Block.X.CompareTo(b.Block.X);
            if (order == 0)
                order = a.Block.Y.CompareTo(b.Block.Y);
            if (order == 0)
                order = a.Chunk.X.CompareTo(b.Chunk.X);
            return order != 0 ? order : a.Chunk.Y.CompareTo(b.Chunk.Y);
        });

        Vector2i? current = null;

        try
        {
            foreach (var (_, block, chunk) in _wgPending)
            {
                if (block != current)
                {
                    // A block goes whole or not at all: the grid sends it again for each part that loads on a later tick.
                    if (current != null && _wgLoadSpentMs >= _wgLoadBudgetMs)
                        break;

                    current = block;
                }

                // Claimed as it is filled, never before: an empty tile on a loaded chunk reads as a hole.
                if (!biome.LoadedChunks.Add(chunk))
                    continue;

                _wgLoadWatch.Restart();
                LoadChunk(biome, map, grid, chunk, seed);
                _wgLoadSpentMs += _wgLoadWatch.Elapsed.TotalMilliseconds;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Loading put-off chunks of {ToPrettyString(map)} failed. {e}");
        }
        finally
        {
            _wgPending.Clear();
        }
    }

    /// <summary>The origin of the block a chunk loads with.</summary>
    private static Vector2i WgBlockOf(Vector2i chunk)
    {
        return SharedMapSystem.GetChunkIndices(chunk, WgLoadBlock) * WgLoadBlock;
    }

    /// <summary>How many tiles lie between a block and the nearest loader.</summary>
    private static int WgBlockDistance(Vector2i block, List<WgLoader> loaders)
    {
        var nearest = int.MaxValue;

        foreach (var loader in loaders)
        {
            var x = Math.Max(Math.Max(block.X - loader.Pos.X, loader.Pos.X - (block.X + WgLoadBlock)), 0f);
            var y = Math.Max(Math.Max(block.Y - loader.Pos.Y, loader.Pos.Y - (block.Y + WgLoadBlock)), 0f);
            nearest = Math.Min(nearest, (int) Math.Max(x, y));
        }

        return nearest;
    }

    /// <summary>The bounds of every hull over each planet, a chunk wider all round, worked out once a pass.</summary>
    private Dictionary<NetEntity, List<Box2>> WgHullBounds()
    {
        var tick = _wgTiming.CurTick;

        if (_wgHullTick == tick)
            return _wgHullBounds;

        _wgHullTick = tick;

        foreach (var list in _wgHullBounds.Values)
        {
            list.Clear();
        }

        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();

        while (grids.MoveNext(out var uid, out var hull, out var xform))
        {
            if (xform.MapUid is not { } map || uid == map || WgSkipHull(uid) || WgNetworkOf(map) is not { } network)
                continue;

            var bounds = _transform.GetWorldMatrix(xform).TransformBox(hull.LocalAABB).Enlarged(ChunkSize);
            _wgHullBounds.GetOrNew(network).Add(bounds);
        }

        return _wgHullBounds;
    }

    /// <summary>The planet network a map belongs to: a layer's own, or that of the layers a gap lies between.</summary>
    private NetEntity? WgNetworkOf(EntityUid map)
    {
        if (TryComp<WGPlanetLayerComponent>(map, out var layer))
            return layer.Network;

        if (!TryComp<CEZTransitMapComponent>(map, out var gap))
            return null;

        return TryComp(gap.LowerMap ?? gap.UpperMap, out layer) ? layer.Network : null;
    }
}
