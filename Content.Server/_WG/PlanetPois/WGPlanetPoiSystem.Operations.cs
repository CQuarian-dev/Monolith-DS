using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._WG.Planets;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.PlanetPois;
using Content.Shared._WG.Planets;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._WG.PlanetPois;

/// <summary>
/// Operation sites on a world's ground: fresh ground claimed while the round runs, clear of every site, road, cavern mouth
/// and landed grid, so an operation never takes over a point of interest.
/// </summary>
public sealed partial class WGPlanetPoiSystem
{
    /// <summary>A site an operation has left: its ground stays claimed, its marker says what it was.</summary>
    private const string LeftSiteName = "wg-operation-site-left";

    /// <summary>Tries at a spot clear of landed grids and other sites' bounds before the world is given up on.</summary>
    private const int OperationSpotAttempts = 32;

    /// <summary>Built worlds whose ground can take an operation site, skipping the surfaces listed.</summary>
    public List<EntityUid> OperationGrounds(ICollection<string> excluded)
    {
        var grounds = new List<EntityUid>();
        var networks = EntityQueryEnumerator<WGPlanetNetworkComponent>();
        while (networks.MoveNext(out var network))
        {
            if (excluded.Contains(network.Surface.Id)
                || !TryComp<WGPlanetPoiComponent>(network.GroundMap, out var record)
                || !record.NetworkReady
                || !HasComp<BiomeComponent>(network.GroundMap)
                || TableFor(network.GroundMap) == null)
                continue;
            grounds.Add(network.GroundMap);
        }
        return grounds;
    }

    /// <summary>The world's own name, as the weather report gives it; the ground's map name otherwise.</summary>
    public string OperationWorldName(EntityUid ground)
    {
        if (TryComp<WGPlanetLayerComponent>(ground, out var layer)
            && TryGetEntity(layer.Network, out var network)
            && TryComp<WGPlanetWeatherComponent>(network, out var weather)
            && !string.IsNullOrEmpty(weather.PlanetName))
            return weather.PlanetName;
        return MetaData(ground).EntityName;
    }

    /// <summary>
    /// Claims ground for an operation site <paramref name="reach"/> tiles across from its middle: whatever grew there
    /// cleared and pinned, recorded as a site with an identified orbit signal.
    /// </summary>
    public bool TryReserveOperationSite(EntityUid ground, float reach, LocId name, out Vector2 spot, out int index)
    {
        spot = default;
        index = -1;
        if (!TryComp<WGPlanetPoiComponent>(ground, out var record)
            || !TryComp<BiomeComponent>(ground, out var biome)
            || !TryComp<MapGridComponent>(ground, out var grid)
            || TableFor(ground) is not { } table
            || !TryComp<WGPlanetLayerComponent>(ground, out var layer)
            || !TryGetEntity(layer.Network, out var networkUid)
            || !TryComp<WGPlanetNetworkComponent>(networkUid, out var network))
            return false;

        var mapId = Transform(ground).MapID;
        var radius = record.RoamerRadius + table.EdgeMargin;
        var found = false;
        for (var attempt = 0; attempt < OperationSpotAttempts && !found; attempt++)
        {
            // Only the last try sweeps the whole ring, so a crowded world costs one sweep at most, not one per try.
            var last = attempt == OperationSpotAttempts - 1;
            if (!TryWreckSpot((ground, biome), grid, record.RoamerCentre, radius, table, reach, out spot, sweepRing: last))
            {
                if (last)
                    return false;
                continue;
            }
            var footprint = Footprint(spot, reach);
            found = !GridsNear(ground, mapId, spot, reach + WreckClearance)
                    && !record.Sites.Exists(other => other.Bounds.Intersects(footprint));
        }
        if (!found)
            return false;

        var under = ClearCampGround(ground, grid, biome, spot, reach);
        var origin = spot.Floored();
        var site = new WGPlanetPoiSite
        {
            Name = name,
            Origin = origin,
            Bounds = Footprint(spot, reach),
            TileCount = under.Count,
            Connection = WGPlanetPoiConnection.Isolated,
            Expeditions = false,
            Difficulty = table.Difficulty,
        };
        site.Signal = SpawnSignal(ground, network.OrbitMap, origin, new WGPlanetPoiEntry { Name = name }, table);
        if (TryComp<WGPoiSignalComponent>(site.Signal, out var signal))
            Reveal((site.Signal, signal), null);
        record.Sites.Add(site);
        ClearFurnitureInside(record, site.Bounds);
        index = record.Sites.Count - 1;
        return true;
    }

    /// <summary>
    /// Loads an operation's grid with its middle on a claimed spot and settles it into the ground; fails if a grid has
    /// landed within <paramref name="reach"/> of it since it was claimed.
    /// </summary>
    public bool TryLoadOperationSite(EntityUid ground, ResPath path, Vector2 spot, Vector2 centre, float reach,
        [NotNullWhen(true)] out Entity<MapGridComponent>? site)
    {
        site = null;
        var mapId = Transform(ground).MapID;
        if (GridsNear(ground, mapId, spot, reach + WreckClearance)
            || !_loader.TryLoadGrid(mapId, path, out var loaded, offset: spot - centre))
            return false;
        SettleWreck(loaded.Value);
        site = loaded;
        return true;
    }

    /// <summary>Marks an operation's site as left once its grid is gone; the ground stays claimed.</summary>
    public void ReleaseOperationSite(EntityUid ground, int index)
    {
        if (!TryComp<WGPlanetPoiComponent>(ground, out var record) || index < 0 || index >= record.Sites.Count)
            return;
        var site = record.Sites[index];
        site.Name = LeftSiteName;
        site.Cleared = true;
        if (TryComp<WGPoiSignalComponent>(site.Signal, out var signal))
        {
            signal.Name = LeftSiteName;
            Reveal((site.Signal, signal), null);
        }
    }

    /// <summary>The tiles a site claims round its middle, upper edges exclusive, as other sites record theirs.</summary>
    private static Box2i Footprint(Vector2 spot, float reach)
    {
        return new Box2i((int) MathF.Floor(spot.X - reach), (int) MathF.Floor(spot.Y - reach),
            (int) MathF.Ceiling(spot.X + reach) + 1, (int) MathF.Ceiling(spot.Y + reach) + 1);
    }

    /// <summary>Whether a grid other than the ground lies within a circle on its map.</summary>
    private bool GridsNear(EntityUid ground, MapId map, Vector2 spot, float clear)
    {
        var grids = new List<Entity<MapGridComponent>>();
        _maps.FindGridsIntersecting(map, Box2.CenteredAround(spot, new Vector2(clear * 2f)), ref grids, approx: true, includeMap: false);
        return grids.Exists(found => found.Owner != ground);
    }

    /// <summary>The ground's POI table, honouring the poi_table override.</summary>
    private WGPlanetPoiTablePrototype? TableFor(EntityUid ground)
    {
        if (!TryComp<WGPlanetLayerComponent>(ground, out var layer)
            || !TryGetEntity(layer.Network, out var network)
            || !TryComp<WGPlanetNetworkComponent>(network, out var comp)
            || !_proto.TryIndex(comp.Surface, out var surface))
            return null;

        var forced = _cfg.GetCVar(PlanetCVars.PoiTable);
        var tableId = forced.Length > 0 ? new ProtoId<WGPlanetPoiTablePrototype>(forced) : surface.Pois;
        return tableId is { } id && _proto.TryIndex(id, out var table) ? table : null;
    }
}
