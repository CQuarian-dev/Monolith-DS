using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._WG.Caverns;

/// <summary>Raised on a shovel when digging a shaft through a ground tile into the cavern below ends.</summary>
[Serializable, NetSerializable]
public sealed partial class WGCavernShaftDigDoAfterEvent : DoAfterEvent
{
    /// <summary>The ground map being dug.</summary>
    public NetEntity Grid;

    /// <summary>The ground tile being dug.</summary>
    public Vector2i Tile;

    public WGCavernShaftDigDoAfterEvent(NetEntity grid, Vector2i tile)
    {
        Grid = grid;
        Tile = tile;
    }

    /// <inheritdoc/>
    public override DoAfterEvent Clone()
    {
        return this;
    }

    /// <inheritdoc/>
    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is WGCavernShaftDigDoAfterEvent dig && Grid == dig.Grid && Tile == dig.Tile;
    }
}
