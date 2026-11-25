using Content.Shared._WG.GroundCover;

namespace Content.Server._WG.GroundCover;

/// <summary>Snow cover on a grid, kept only for chunks that have had snow.</summary>
[RegisterComponent]
public sealed partial class WGSnowCoverComponent : Component
{
    [ViewVariables]
    public readonly Dictionary<Vector2i, WGSnowChunk> Chunks = new();
}

/// <summary>One chunk of snow cover: tile depths and the map totals it has caught up to.</summary>
public sealed class WGSnowChunk
{
    public readonly byte[] Depths = new byte[WGSnowCover.ChunkArea];

    /// <summary>The map's <see cref="WGGroundWeatherComponent.Fallen"/> when snow was last laid here.</summary>
    public double FallenAt;

    /// <summary>The map's <see cref="WGGroundWeatherComponent.MeltClock"/> when this chunk last melted.</summary>
    public double MeltAt;

    /// <summary>Goes up on every change, so clients are only sent what changed.</summary>
    public uint Version = 1;

    /// <summary>
    /// The depths as last sent to clients, which walking reads so both sides slow mobs alike. A new array each send,
    /// never changed after.
    /// </summary>
    public byte[]? Sent;

    /// <summary>The <see cref="Version"/> <see cref="Sent"/> was taken at.</summary>
    public uint SentVersion;

    public bool IsEmpty()
    {
        foreach (var depth in Depths)
        {
            if (depth != 0)
                return false;
        }

        return true;
    }
}
