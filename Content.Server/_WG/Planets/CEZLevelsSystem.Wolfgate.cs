using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>True when this map is a planet orbit layer, where parked grids never fall.</summary>
    private bool WgIsOrbitLayer(EntityUid mapUid) => HasComp<WGOrbitLayerComponent>(mapUid);

    /// <summary>Disables z-gravity on an orbit layer and restores it when the grid leaves.</summary>
    // Orbit has no terrain, so CE z-physics would otherwise walk a parked grid down every layer to the ground.
    private void WgRefreshOrbitParking(EntityUid grid, EntityUid? mapUid)
    {
        if (!ZPhysicsQuery.TryComp(grid, out var zPhys))
            return;

        var parked = mapUid is { } map && WgIsOrbitLayer(map);

        zPhys.VelocityGravity = !parked;

        if (!parked)
            return;

        // A hull in orbit rests at its layer's height.
        if (zPhys.Velocity != 0f)
            SetZVelocity((grid, zPhys), 0f);

        if (zPhys.LocalPosition != 0f)
            SetZPosition((grid, zPhys), 0f);
    }

    /// <summary>True for a planet layer below its ground; hulls never enter one.</summary>
    private bool WgClosedToHulls(Entity<CEZMapComponent> map) => map.Comp.Depth < 0 && HasComp<WGPlanetLayerComponent>(map);

    /// <summary>True on an orbit layer (left through transit) or when the hop would take the hull below ground.</summary>
    private bool WgRefusesLevelHop(EntityUid grid, Entity<CEZMapComponent> target)
        => WgIsOrbitLayer(Transform(grid).MapUid ?? EntityUid.Invalid) || WgClosedToHulls(target);


    /// <summary>Test seam: whether this grid is currently parked by the orbit hold.</summary>
    public bool WgIsParkedInOrbit(Entity<MapGridComponent> grid)
    {
        return WgIsOrbitLayer(Transform(grid).MapUid ?? EntityUid.Invalid)
               && ZPhysicsQuery.TryComp(grid, out var zPhys)
               && !zPhys.VelocityGravity;
    }

    /// <summary>Destroys contacts held by the grid's children before a map change.</summary>
    // Children keep old-map contacts across a grid move, and the broadphase then asserts "already in contact".
    public void WgDestroyRiderContacts(EntityUid grid)
    {
        var children = Transform(grid).ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (TryComp<PhysicsComponent>(child, out var body))
                _physics.DestroyContacts(body);
        }
    }
}
