using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets;
using Robust.Shared.Map.Components;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    private List<Entity<MapGridComponent>> _wgLayerGrids = new();

    /// <summary>
    /// Whether a planet's air layer below the observer has nothing in view, no grid and no loose entity, so its pass
    /// can be skipped: each pass is a whole render, and from orbit most of the layers are empty sky.
    /// </summary>
    private bool WgNothingInView(EntityUid map, float depth, float ownDepth)
    {
        if (!_entityManager.HasComponent<WGPlanetLayerComponent>(map)
            || _entityManager.HasComponent<CEZGroundLayerComponent>(map)
            || _entityManager.HasComponent<CEZCloudLayerComponent>(map)
            || !_entityManager.TryGetComponent(map, out MapComponent? mapComp))
            return false;

        var view = WgLevelView(map, depth, ownDepth);

        _wgLayerGrids.Clear();
        _mapManager.FindGridsIntersecting(mapComp.MapId, view, ref _wgLayerGrids);
        if (_wgLayerGrids.Count > 0)
            return false;

        return !_entityManager.System<EntityLookupSystem>().AnyEntitiesIntersecting(mapComp.MapId, view);
    }
}
