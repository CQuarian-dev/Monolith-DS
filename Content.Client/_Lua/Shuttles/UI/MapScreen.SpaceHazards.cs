using Content.Client.Stylesheets;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class MapScreen
{
    private void AddSectorLandmarks(MapId mapId)
    {
        if (!_mapHeadings.TryGetValue(mapId, out var gridContents))
            return;

        var celestials = _entManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (celestials.MoveNext(out _, out _, out var icon, out var xform))
        {
            if (xform.MapID != mapId || icon.Label is not { } label)
                continue;

            AddLandmarkButton(gridContents, Loc.GetString(label), new MapCoordinates(_xformSystem.GetWorldPosition(xform), xform.MapID));
        }

        var planets = _entManager.AllEntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (planets.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            AddLandmarkButton(gridContents, Loc.GetString("radar-label-sector-planet"), new MapCoordinates(_xformSystem.GetWorldPosition(xform), xform.MapID));
        }

        if (_entManager.TryGetComponent(_maps.GetMapOrInvalid(mapId), out SectorAsteroidClusterMarkersComponent? markers))
        {
            foreach (var center in markers.Centers)
                AddLandmarkButton(gridContents, Loc.GetString("radar-label-sector-asteroid-cluster"), new MapCoordinates(center, mapId));
        }
    }

    private void AddLandmarkButton(BoxContainer gridContents, string name, MapCoordinates coordinates)
    {
        var button = new Button
        {
            Text = name,
            HorizontalExpand = true,
        };
        button.AddStyleClass(StyleBase.ButtonSquare);

        var container = new BoxContainer
        {
            Margin = new Thickness(2, 2),
            Children = { button },
        };

        _mapObjectControls.Add(container, name);
        gridContents.AddChild(container);

        button.OnPressed += _ =>
        {
            if (!IsFTLBlocked())
                MapRadar.SetMap(coordinates.MapId, coordinates.Position, recentering: true);
        };
    }
}
