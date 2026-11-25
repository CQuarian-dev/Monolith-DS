using System.Numerics;
using Content.Shared._WG.Planets;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    /// <summary>Whether the radar draws the planet map.</summary>
    public bool ShowPlanetTerrain { get; set; } = true;
    // Keep the server's grid-relative origin even while the user pans onto map coordinates.
    private EntityCoordinates? _wgRadarOrigin;
    private Button? _wgTerrainButton;

    private void AddWgTerrainButton(LayoutContainer parent)
    {
        var button = _wgTerrainButton = new Button
        {
            Text = Loc.GetString(ShowPlanetTerrain ? "wg-planet-map-on" : "wg-planet-map-off"),
            ToolTip = Loc.GetString("wg-planet-map-tooltip"),
            ToggleMode = true,
            Pressed = ShowPlanetTerrain,
        };
        button.OnToggled += args =>
        {
            ShowPlanetTerrain = args.Pressed;
            button.Text = Loc.GetString(args.Pressed ? "wg-planet-map-on" : "wg-planet-map-off");
        };
        parent.AddChild(button);
        LayoutContainer.SetAnchorAndMarginPreset(button, LayoutContainer.LayoutPreset.TopRight, margin: 10);
    }

    /// <summary>The orbit layer of the world a map belongs to, resolved through transit maps and the world's network.</summary>
    private EntityUid? GetWgPlanetOrbit(EntityUid? map)
    {
        if (map is { } own && EntManager.HasComponent<WGOrbitLayerComponent>(own))
            return own;

        // Transit maps sit between two permanent layers of the same world.
        if (EntManager.TryGetComponent<CEZTransitMapComponent>(map, out var transit))
            map = transit.LowerMap ?? transit.UpperMap;
        if (map is { } lower && EntManager.HasComponent<WGOrbitLayerComponent>(lower))
            return lower;
        if (!EntManager.TryGetComponent<WGPlanetLayerComponent>(map, out var layer) || layer.Network == null)
            return null;

        var query = EntManager.EntityQueryEnumerator<WGOrbitLayerComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (candidate.Network == layer.Network)
                return uid;
        }
        return null;
    }

    /// <summary>Draws the planet map of the world the radar is on, beneath radar contacts.</summary>
    private void DrawWgTerrain(DrawingHandleScreen handle, Matrix3x2 worldToView, EntityUid? map)
    {
        _wgMapDrewSites = false;
        if (ShowPlanetTerrain && GetWgPlanetOrbit(map) is { } orbit)
            DrawWgPlanetMap(handle, worldToView, orbit);
    }
}
