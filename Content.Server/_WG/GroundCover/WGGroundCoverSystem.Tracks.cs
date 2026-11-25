using System.Numerics;
using Content.Shared._WG.GroundCover;

namespace Content.Server._WG.GroundCover;

public sealed partial class WGGroundCoverSystem
{
    /// <summary>Snow depth units a vehicle's tread presses out of the tile it rolls over.</summary>
    public const int TrackCompression = WGSnowCover.UnitsPerLevel;

    // -- LuaM: колеи от техники Wolfgate (WGVehicleTreadsEvent); наша техника их пока не шлёт
    private void InitializeTracks()
    {
    }

    private void Compress(EntityUid grid, Vector2i tile)
    {
        var depth = GetDepth(grid, tile);
        if (depth > 0)
            SetDepth(grid, tile, depth - TrackCompression);
    }

    private static Vector2i TileOf(Vector2 position)
    {
        return new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
    }
}
