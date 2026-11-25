using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;

namespace Content.Shared._WG.Planets;

/// <summary>What the shuttle map screen calls a map: its name, with a plain note of what kind of place it is.</summary>
public sealed class WGMapLabelSystem : EntitySystem
{
    /// <summary>The heading for a map; <paramref name="name"/> is its entity name, which may be empty.</summary>
    public string Label(EntityUid map, string name)
    {
        var named = !string.IsNullOrEmpty(name);

        if (HasComp<CEZTransitMapComponent>(map))
            return Loc.GetString("wg-shuttle-map-transit");

        if (HasComp<StarSystemMapComponent>(map) && named)
            return Loc.GetString("wg-shuttle-map-sector", ("name", name));

        if (named && HasComp<WGPlanetLayerComponent>(map) && !HasComp<WGOrbitLayerComponent>(map) && IsGround(map))
            return Loc.GetString("wg-shuttle-map-surface", ("name", name));

        if (named)
            return name;

        return TryComp<CEZMapComponent>(map, out var zMap)
            ? Loc.GetString("wg-shuttle-map-level", ("level", zMap.Depth))
            : Loc.GetString("shuttle-console-unknown");
    }

    private bool IsGround(EntityUid map)
    {
        return HasComp<CEZGroundLayerComponent>(map) || TryComp<CEZMapComponent>(map, out var zMap) && zMap.Depth == 0;
    }
}
