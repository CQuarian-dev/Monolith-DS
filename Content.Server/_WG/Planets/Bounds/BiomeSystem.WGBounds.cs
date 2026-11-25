using Content.Shared._WG.Planets;
using Content.Shared.Parallax.Biomes;

namespace Content.Server.Parallax;

/// <summary>Keeps the loader inside a bounded planet layer's circle.</summary>
public sealed partial class BiomeSystem
{
    /// <summary>Whether a chunk lies wholly outside its layer's circle, so it never loads.</summary>
    public bool WgOutOfBounds(EntityUid map, Vector2i chunkOrigin)
    {
        return TryComp<WGPlanetBoundsComponent>(map, out var bounds)
               && bounds.Radius > 0f
               && !bounds.MeetsSquare(chunkOrigin, ChunkSize);
    }

    /// <summary>
    /// Every chunk origin of a bounded layer's circle, by 16-tile grid chunk nearest the centre first, so each grid
    /// chunk fills in one go and is sent to the lobby once.
    /// </summary>
    public List<Vector2i> WgChunksInBounds(WGPlanetBoundsComponent bounds)
    {
        var chunks = new List<Vector2i>();
        var radius = (int) MathF.Ceiling(bounds.Radius);
        var min = SharedMapSystem.GetChunkIndices(new Vector2i((int) MathF.Floor(bounds.Centre.X) - radius, (int) MathF.Floor(bounds.Centre.Y) - radius), ChunkSize);
        var max = SharedMapSystem.GetChunkIndices(new Vector2i((int) MathF.Floor(bounds.Centre.X) + radius, (int) MathF.Floor(bounds.Centre.Y) + radius), ChunkSize);

        for (var x = min.X; x <= max.X; x++)
        for (var y = min.Y; y <= max.Y; y++)
        {
            var origin = new Vector2i(x, y) * ChunkSize;

            if (bounds.MeetsSquare(origin, ChunkSize))
                chunks.Add(origin);
        }

        var centre = bounds.Centre;
        chunks.Sort((a, b) =>
        {
            var blockA = WgBlockOf(a);
            var blockB = WgBlockOf(b);
            var da = (new System.Numerics.Vector2(blockA.X + WgLoadBlock / 2f, blockA.Y + WgLoadBlock / 2f) - centre).LengthSquared();
            var db = (new System.Numerics.Vector2(blockB.X + WgLoadBlock / 2f, blockB.Y + WgLoadBlock / 2f) - centre).LengthSquared();
            var order = da.CompareTo(db);
            if (order == 0)
                order = blockA.X.CompareTo(blockB.X);
            if (order == 0)
                order = blockA.Y.CompareTo(blockB.Y);
            if (order == 0)
                order = a.X.CompareTo(b.X);
            return order != 0 ? order : a.Y.CompareTo(b.Y);
        });

        return chunks;
    }
}
