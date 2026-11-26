using Content.Server.Gravity;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.PlanetCities;
using Content.Shared._WG.Planets;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._WG.PlanetPois;

/// <summary>
/// Gives a settled wreck or camp hull back to space once it leaves its world's layers and the gaps between them.
/// Settling makes a hull part of the ground (city grid, z-level anchor, static body, inherent gravity, a roof over
/// every tile), and none of that should follow it off the planet, however it got out.
/// </summary>
public sealed class WGPlanetWreckReleaseSystem : EntitySystem
{
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ThrusterSystem _thrusters = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WGPlanetCityGridComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnParentChanged(Entity<WGPlanetCityGridComponent> ent, ref EntParentChangedMessage args)
    {
        // A fixed city's levels never move; only a settled hull has no city.
        if (ent.Comp.City.Id != null || TerminatingOrDeleted(ent) || !HasComp<MapGridComponent>(ent))
            return;

        if (Transform(ent).MapUid is not { } map || TerminatingOrDeleted(map) || OnItsWorld(map))
            return;

        Release(ent);
    }

    /// <summary>Whether a map is a planet layer, or the transit gap between two layers of the same planet.</summary>
    private bool OnItsWorld(EntityUid map)
    {
        if (HasComp<WGPlanetLayerComponent>(map))
            return true;

        return TryComp<CEZTransitMapComponent>(map, out var transit)
            && TryComp<WGPlanetLayerComponent>(transit.LowerMap, out var lower)
            && TryComp<WGPlanetLayerComponent>(transit.UpperMap, out var upper)
            && lower.Network != null
            && lower.Network == upper.Network;
    }

    /// <summary>Undoes a hull's settling: it is a vessel again, flies, and takes gravity from its own generators.</summary>
    public void Release(EntityUid grid)
    {
        RemComp<WGPlanetCityGridComponent>(grid);
        RemComp<RoofComponent>(grid);

        if (!HostsMappingAnchor(grid))
            RemComp<CEZMappingAnchorGridComponent>(grid);

        if (TryComp<GravityComponent>(grid, out var gravity) && gravity.Inherent)
        {
            gravity.Inherent = false;
            _gravity.RefreshGravity(grid, gravity);
        }

        _physics.SetBodyType(grid, BodyType.Dynamic);

        if (!HasComp<ShuttleComponent>(grid))
        {
            EnsureComp<ShuttleComponent>(grid);
            _thrusters.WgRebankThrusters(grid);
        }

        Log.Info($"Released settled hull {ToPrettyString(grid)} from planet ground on leaving its world.");
    }

    private bool HostsMappingAnchor(EntityUid grid)
    {
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<CEZMappingAnchorComponent>(child))
                return true;
        }

        return false;
    }
}
