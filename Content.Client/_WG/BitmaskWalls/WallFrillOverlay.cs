using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._WG.BitmaskWalls;

/// <summary>Draws wall frills over mobs, lit like the wall below them. The frill over the player's tile fades.</summary>
public sealed partial class WallFrillOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ILightManager _lights = default!;

    private static readonly ProtoId<ShaderPrototype> FrillShader = "WallFrill";
    private const float FadedAlpha = 0.5f;

    private readonly WallFrillSystem _frills;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly ShaderInstance _shader;

    /// <summary>One shader per drawn grid, since each grid needs its own wall offset within a frame.</summary>
    private readonly List<ShaderInstance> _gridShaders = new();

    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public WallFrillOverlay(WallFrillSystem frills)
    {
        IoCManager.InjectDependencies(this);
        // Over mobs, under doors and anything hung on the wall face.
        ZIndex = (int) DrawDepth.Doors;
        _frills = frills;
        _transform = _entManager.System<SharedTransformSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _shader = _prototypes.Index(FrillShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldBounds, ref _grids);
        if (_grids.Count == 0)
            return;

        var handle = args.WorldHandle;
        EntityUid? playerGrid = null;
        var playerTile = Vector2i.Zero;
        if (_player.LocalEntity is { } player
            && _entManager.TryGetComponent(player, out TransformComponent? playerXform)
            && playerXform.GridUid is { } pGrid
            && _entManager.TryGetComponent(pGrid, out MapGridComponent? pGridComp))
        {
            playerGrid = pGrid;
            playerTile = _map.TileIndicesFor(pGrid, pGridComp, playerXform.Coordinates);
        }

        var lit = IsLit(args);
        var shaders = 0;
        foreach (var grid in _grids)
        {
            if (!_frills.Frills.TryGetValue(grid.Owner, out var frills))
                continue;

            var (_, _, worldMatrix, invMatrix) = _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
            var local = invMatrix.TransformBox(args.WorldBounds.Enlarged(1f));
            var size = grid.Comp.TileSize;
            var minX = (int) MathF.Floor(local.Left / size);
            var minY = (int) MathF.Floor(local.Bottom / size);
            var maxX = (int) MathF.Ceiling(local.Right / size);
            var maxY = (int) MathF.Ceiling(local.Top / size);

            if (lit)
            {
                if (shaders == _gridShaders.Count)
                    _gridShaders.Add(_shader.Duplicate());

                var shader = _gridShaders[shaders++];
                shader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
                shader.SetParameter("wallOffset", WallOffset(args.Viewport, worldMatrix, size));
                handle.UseShader(shader);
            }

            handle.SetTransform(worldMatrix);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    var tile = new Vector2i(x, y);
                    if (!frills.TryGetValue(tile, out var frill))
                        continue;

                    if (!_entManager.TryGetComponent(frill.Wall, out SpriteComponent? sprite)
                        || !sprite.Visible
                        || !_entManager.TryGetComponent(frill.Wall, out TransformComponent? wallXform)
                        || wallXform.GridUid != grid.Owner)
                    {
                        continue;
                    }

                    var color = sprite.Color;
                    if (grid.Owner == playerGrid && tile == playerTile)
                        color = color.WithAlpha(color.A * FadedAlpha);

                    handle.DrawTexture(frill.Texture, new Vector2(x * size, y * size), color);
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        if (shaders > 0)
            handle.UseShader(null);
    }

    private bool IsLit(in OverlayDrawArgs args)
    {
        return _lights.Enabled
            && args.Viewport.Eye is { DrawLight: true }
            && _entManager.TryGetComponent(args.MapUid, out MapComponent? map)
            && map.LightingEnabled;
    }

    /// <summary>Screen-space step from a frill to the wall below it, where the shader samples light.</summary>
    private static Vector2 WallOffset(IClydeViewport viewport, Matrix3x2 gridToWorld, float tileSize)
    {
        var origin = viewport.Eye?.Position.Position ?? Vector2.Zero;
        var south = Vector2.TransformNormal(new Vector2(0f, -tileSize), gridToWorld);
        var delta = viewport.WorldToLocal(origin + south) - viewport.WorldToLocal(origin);
        return new Vector2(delta.X / viewport.Size.X, -delta.Y / viewport.Size.Y);
    }
}
