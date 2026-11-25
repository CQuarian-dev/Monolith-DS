using System.Linq; // LuaM
using Content.Server._WG.Planets.Flight;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._WG.CCVar;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Layers;
using Content.Shared.Tiles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._WG.Planets.Atmosphere;

/// <summary>Gives every planet layer with ground a terrain atmosphere, so a room built on a planet holds its own air.</summary>
public sealed partial class WGTerrainAtmosphereSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IComponentFactory _factory = default!;

    /// <summary>Prefix of a road's own copy of a floor players can also lay (Resources/Prototypes/_WG/Planets/road_tiles.yml).</summary>
    public const string RoadPrefix = "WGRoad";

    /// <summary>Tiles some floor tile item lays; built lazily, dropped on a prototype reload.</summary>
    private HashSet<string>? _placeable;

    public override void Initialize()
    {
        SubscribeLocalEvent<WGPlanetNetworkBuiltEvent>(OnNetworkBuilt);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _placeable = null;
    }

    /// <summary>The tile a road paints for a surface: its own copy when the surface is a floor players can lay too.</summary>
    public string RoadTile(string tile)
    {
        var road = RoadPrefix + tile;
        return _tileDefs.TryGetDefinition(road, out _) ? road : tile;
    }

    /// <summary>
    /// Lets a road surface on a planet layer's ground share the world's air like bare ground, so roads add no tracked
    /// atmos tiles and don't keep their chunks from being stashed. A floor players can lay is left tracked.
    /// </summary>
    public void AddRoadTile(EntityUid ground, string tile)
    {
        if (!TryComp<WGTerrainAtmosphereComponent>(ground, out var terrain)
            || !_tileDefs.TryGetDefinition(tile, out var definition)
            || Placeable().Contains(tile))
        {
            return;
        }

        terrain.OpenTiles.Add(definition.TileId);
    }

    private HashSet<string> Placeable()
    {
        if (_placeable != null)
            return _placeable;

        _placeable = new HashSet<string>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.TryGetComponent<FloorTileComponent>(out var floor, _factory) && floor.OutputTiles != null)
                _placeable.UnionWith(floor.OutputTiles.Select(tile => tile.Id)); // LuaM: ProtoId list
        }

        return _placeable;
    }

    private void OnNetworkBuilt(ref WGPlanetNetworkBuiltEvent args)
    {
        if (!_cfg.GetCVar(PlanetCVars.TerrainAtmosphere))
            return;

        AddTo(args.Ground, surface: true);

        foreach (var layer in args.Lower)
        {
            AddTo(layer, surface: false);
        }
    }

    private void AddTo(EntityUid map, bool surface)
    {
        if (!TryComp<MapGridComponent>(map, out var grid) || !TryComp<BiomeComponent>(map, out var biome))
            return;

        var ground = new HashSet<ProtoId<ContentTileDefinition>>();
        CollectTiles(biome.Layers, ground, new HashSet<string>());

        // A crashing hull scrapes the surface down to this.
        if (surface)
            ground.Add(WGFlightSystem.ScarTile);

        var ev = new WGTerrainOpenTilesEvent(map, ground);
        RaiseLocalEvent(ref ev);

        _atmos.WgAddTerrainAtmosphere((map, grid), OpenTiles(ground));
    }

    /// <summary>Every tile a biome's layers lay, through the templates they nest.</summary>
    private void CollectTiles(List<IBiomeLayer> layers, HashSet<ProtoId<ContentTileDefinition>> tiles, HashSet<string> seen)
    {
        foreach (var layer in layers)
        {
            switch (layer)
            {
                case BiomeTileLayer tile:
                    tiles.Add(tile.Tile);
                    break;
                case BiomeMetaLayer meta when seen.Add(meta.Template) && _proto.TryIndex<BiomeTemplatePrototype>(meta.Template, out var template):
                    CollectTiles(template.Layers, tiles, seen);
                    break;
            }
        }
    }

    /// <summary>The tile ids of the ground tiles and of the outdoor ground that digging turns them into.</summary>
    private HashSet<int> OpenTiles(HashSet<ProtoId<ContentTileDefinition>> ground)
    {
        var open = new HashSet<int>();

        foreach (var id in ground)
        {
            if (!_tileDefs.TryGetDefinition(id, out var definition))
                continue;

            var tile = (ContentTileDefinition) definition;

            while (open.Add(tile.TileId)
                   && !string.IsNullOrEmpty(tile.BaseTurf)
                   && _tileDefs.TryGetDefinition(tile.BaseTurf, out var under)
                   && under is ContentTileDefinition { Weather: true, MapAtmosphere: false } below)
            {
                tile = below;
            }
        }

        return open;
    }
}
