using System.Numerics;
using Content.Client._WG.Roofs; // WOLFGATE(Roofs)
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Physics;

namespace Content.Client.Light;

public sealed partial class RoofOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    [Dependency] private IEntityManager _entManagerForMaps = default!;
    private SharedMapSystem _mapManager => _entManagerForMaps.System<SharedMapSystem>();
    [Dependency] private ITileDefinitionManager _tileDefs = default!; // WOLFGATE(Planets): the open-grating check below reads tile definitions.
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private Robust.Shared.Configuration.IConfigurationManager _luamCfg = default!; // LuaM

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedRoofSystem _roof = default!;
    private readonly SharedTransformSystem _xformSystem;

    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public const int ContentZIndex = BeforeLightTargetOverlay.ContentZIndex + 1;

    public RoofOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        IoCManager.InjectDependencies(this);

        _lookup = _entManager.System<EntityLookupSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _roof = _entManager.System<SharedRoofSystem>();
        _xformSystem = _entManager.System<SharedTransformSystem>();

        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || !_entManager.HasComponent<MapLightComponent>(args.MapUid))
            return;

        // WOLFGATE(Roofs) START: a ghost sees no roof shade over buildings and ships, only underground.
        var wgRoofs = _entManager.System<WGRoofSystem>();
        var wgGhost = !wgRoofs.Enabled;
        // WOLFGATE END

        var viewport = args.Viewport;
        var eye = args.Viewport.Eye;
        var luamMap = args.MapUid; // LuaM

        var worldHandle = args.WorldHandle;
        var lightoverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightoverlay.EnlargedBounds;
        var target = lightoverlay.GetCachedForViewport(viewport).EnlargedLightTarget;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, bounds, ref _grids, approx: true, includeMap: true);
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                for (var i = 0; i < _grids.Count; i++)
                {
                    var grid = _grids[i];

                    if (!_entManager.TryGetComponent(grid.Owner, out ImplicitRoofComponent? roof))
                        continue;

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetTilesIntersecting(grid.Owner, grid, bounds);
                    var color = roof.Color;

                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        // WOLFGATE(Planets) START: open grating is no roof.
                        // Lattice is a space tile laid on hulls that keep their implicit roof; painted over, it
                        // blacked out the world below whenever the eye crossed it.
                        if (_tileDefs[tileRef.Tile.TypeId] is ContentTileDefinition { MapAtmosphere: true })
                            continue;
                        // WOLFGATE END
                        if (wgGhost) // WOLFGATE(Roofs): a ghost sees ships unshaded
                            continue; // WOLFGATE(Roofs)

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color);
                    }

                    // Don't need it for the next stage.
                    _grids.RemoveAt(i);
                    i--;
                }
            }, null);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                foreach (var grid in _grids)
                {
                    if (!_entManager.TryGetComponent(grid.Owner, out RoofComponent? roof))
                        continue;
                    if (wgGhost && !wgRoofs.Underground(grid.Owner)) // WOLFGATE(Roofs): a ghost sees buildings unshaded
                        continue; // WOLFGATE(Roofs)

                    var gridMatrix = _xformSystem.GetWorldMatrix(grid.Owner);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetTilesIntersecting(grid.Owner, grid, bounds);
                    var roofEnt = (grid.Owner, grid.Comp, roof);
                    var luamIndoor = LuaMIndoorLight(luamMap, grid.Owner, wgRoofs); // LuaM

                    // Due to stencilling we essentially draw on unrooved tiles
                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        var color = _roof.GetColor(roofEnt, tileRef.GridIndices);

                        if (color == null)
                        {
                            continue;
                        }

                        // LuaM-start: under a roof on the surface some of the day still comes in through windows and doors
                        if (luamIndoor is { } indoor)
                        {
                            color = new Color(MathF.Max(color.Value.R, indoor.R), MathF.Max(color.Value.G, indoor.G),
                                MathF.Max(color.Value.B, indoor.B), color.Value.A);
                        }
                        // LuaM-end

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color.Value);
                    }
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    // LuaM-start: a share of the outdoor light for roofed tiles above ground; caverns stay as dark as their roof
    private Color? LuaMIndoorLight(EntityUid map, EntityUid grid, WGRoofSystem roofs)
    {
        var share = _luamCfg.GetCVar(Content.Shared._LuaM.CCVar.LuaMCVars.RoofIndoorLight);
        if (share <= 0f || roofs.Underground(grid) || !_entManager.TryGetComponent(map, out MapLightComponent? light))
            return null;

        var ambient = light.AmbientLightColor;
        return new Color(ambient.R * share, ambient.G * share, ambient.B * share);
    }
    // LuaM-end
}
