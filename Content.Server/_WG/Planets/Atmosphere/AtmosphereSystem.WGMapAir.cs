using Content.Server._WG.Planets.Atmosphere;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    /// <summary>
    /// Sets a map's air like <see cref="SetMapGasMixture"/> with updateTiles, but swaps it into the map tiles in place.
    /// Tiles are woken only once the air has drifted far enough from what they last saw to exchange, or its gases
    /// changed; the overlay is refreshed only when it looks different.
    /// </summary>
    public void WgSwapMapAir(EntityUid uid, GasMixture mixture, MapAtmosphereComponent component)
    {
        if (!mixture.Immutable)
        {
            mixture = mixture.Clone();
            mixture.MarkImmutable();
        }

        var overlay = _gasTileOverlaySystem.GetOverlayData(mixture);
        var visualsChanged = !overlay.Equals(component.Overlay);
        component.Mixture = mixture;
        component.Overlay = overlay;

        if (visualsChanged)
            Dirty(uid, component);

        var swap = EnsureComp<WGMapAirSwapComponent>(uid);
        var activate = swap.Activated is not { } last || WgAirDiffers(last, mixture);

        if (activate)
            swap.Activated = mixture;

        var grids = AllEntityQuery<GridAtmosphereComponent, TransformComponent>();
        while (grids.MoveNext(out var grid, out var atmos, out var xform))
        {
            if (xform.MapUid != uid || atmos.MapTiles.Count == 0)
                continue;

            TryComp<GasTileOverlayComponent>(grid, out var visuals);

            foreach (var tile in atmos.MapTiles)
            {
                tile.Air = mixture;
                tile.AirArchived?.CopyFrom(mixture);
                tile.ArchivedCycle = 0;
                tile.LastShare = 0f;

                if (activate)
                {
                    AddActiveTile(atmos, tile);

                    for (var i = 0; i < Atmospherics.Directions; i++)
                    {
                        if (atmos.Tiles.TryGetValue(tile.GridIndices.Offset((AtmosDirection) (1 << i)), out var adjacent))
                            AddActiveTile(atmos, adjacent);
                    }
                }

                if (visualsChanged && visuals != null)
                    _gasTileOverlaySystem.Invalidate((grid, visuals), tile.GridIndices);
            }
        }
    }

    /// <summary>Whether tiles holding one air would exchange with the other: a temperature gap they act on, or any gas change.</summary>
    private static bool WgAirDiffers(GasMixture last, GasMixture next)
    {
        if (MathF.Abs(last.Temperature - next.Temperature) >= Atmospherics.MinimumTemperatureDeltaToSuspend
            || !MathHelper.CloseTo(last.Volume, next.Volume))
        {
            return true;
        }

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            if (last.GetMoles(i) != next.GetMoles(i))
                return true;
        }

        return false;
    }
}
