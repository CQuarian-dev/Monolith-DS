using Content.Client._WG.BitmaskWalls;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    [Dependency] private WallFrillSystem _frills = default!;

    /// <summary>Each neighbour's junction bit, and the side neighbours a corner neighbour needs to count.</summary>
    private static readonly (Vector2i Offset, int Bit, int Needs)[] BitmaskNeighbours =
    {
        ((0, 1), 1, 0),
        ((0, -1), 2, 0),
        ((1, 0), 4, 0),
        ((-1, 0), 8, 0),
        ((1, 1), 16, 1 | 4),
        ((1, -1), 32, 2 | 4),
        ((-1, -1), 64, 2 | 8),
        ((-1, 1), 128, 1 | 8),
    };

    private void CalculateNewSpriteBitmask(Entity<MapGridComponent>? gridEntity, IconSmoothComponent smooth,
        Entity<SpriteComponent> sprite, TransformComponent xform, EntityQuery<IconSmoothComponent> smoothQuery)
    {
        var junction = 0;
        var tile = Vector2i.Zero;
        if (gridEntity is { } grid)
        {
            tile = _mapSystem.TileIndicesFor(grid, grid.Comp, xform.Coordinates);
            foreach (var (offset, bit, needs) in BitmaskNeighbours)
            {
                var candidates = _mapSystem.GetAnchoredEntities(grid, grid.Comp, tile + offset);
                if ((junction & needs) == needs && MatchingEntity(smooth, candidates, smoothQuery, offset))
                    junction |= bit;
            }
        }

        _sprite.LayerSetRsiState(sprite.AsNullable(), 0, $"{smooth.StateBase}{junction}");
        _frills.UpdateFrill(sprite, junction, gridEntity?.Owner, tile);
    }
}
