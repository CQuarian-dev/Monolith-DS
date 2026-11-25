using System.Numerics;
using Content.Client._WG.PlanetSignals;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._WG.PlanetCargoHubs;
using Content.Shared._WG.Planets;
using Content.Shared._WG.PlanetScan;
using Content.Shared._WG.PlanetSignals;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private static readonly Color WgSiteColor = Color.FromHex("#ffb347");
    private static readonly Color WgVesselColor = Color.FromHex("#9fc3ff");
    private static readonly Color WgPersonColor = Color.FromHex("#7fe0ff");
    private static readonly Color WgPersonHurtColor = Color.FromHex("#ff4b4b");
    private static readonly Color WgExpeditionColor = Color.FromHex("#3fe05a");

    /// <summary>Where each world was drawn this frame, in pixels: its blip, the circle round it and its name.</summary>
    private readonly List<(EntityUid Body, Vector2 Centre, float Radius, UIBox2 Label)> _wgPlanetHits = new();

    /// <summary>The cargo depots on the world whose tag is being drawn, by site name.</summary>
    private readonly List<string> _wgDepots = new();

    private float _wgViewScale = 1f;
    private Matrix3x2 _wgWorldToView = Matrix3x2.Identity;
    private Vector2? _wgPressedAt;
    private WGPlanetSignalsWindow? _wgSignalsWindow;

    /// <summary>
    /// On a world's layers, marks its sites, the vessels on its other layers and the people a medical tracker gives
    /// away, wherever they are; the ship's own expedition site is
    /// marked in green, with an arrow at the rim while it is off the scope.
    /// </summary>
    private void DrawWgPlanetSignals(DrawingHandleScreen handle, Matrix3x2 worldToView, TransformComponent xform)
    {
        _wgPlanetHits.Clear();
        _wgWorldToView = worldToView;
        _wgViewScale = MathF.Sqrt(worldToView.M11 * worldToView.M11 + worldToView.M12 * worldToView.M12);

        if (!ShowIFF || xform.MapUid is not { } mapUid || WgSignalsOfLayer(mapUid) is not { } signals)
            return;

        var map = EntManager.GetNetEntity(mapUid);
        var ours = WgOurGrid();
        var centre = MidPointVector;
        var rim = MathF.Min(PixelSize.X, PixelSize.Y) / 2f - 20f * UIScale;
        var size = 5f * UIScale;

        foreach (var signal in signals.Signals)
        {
            if (signal.Kind == WGPlanetSignalKind.Vessel && signal.Map == map)
                continue;

            var mine = ours != null && signal.Expedition == ours;
            // The planet map marks sites by kind; only the ship's own expedition site keeps its marker here.
            if (signal.Kind == WGPlanetSignalKind.Site && !mine && _wgMapDrewSites)
                continue;

            var point = Vector2.Transform(new Vector2(signal.Position.X + 0.5f, signal.Position.Y + 0.5f), worldToView);
            var offset = point - centre;

            if (offset.Length() > rim)
            {
                if (!mine)
                    continue;

                // Off the scope: an arrow at the rim, pointing the way, with the distance.
                var direction = offset / offset.Length();
                var tip = centre + direction * rim;
                var side = new Vector2(-direction.Y, direction.X) * size;
                handle.DrawLine(tip, tip - direction * size * 2f + side, WgExpeditionColor);
                handle.DrawLine(tip, tip - direction * size * 2f - side, WgExpeditionColor);
                var distance = offset.Length() / MathF.Max(_wgViewScale, 0.0001f);
                var arrowLabel = Loc.GetString("wg-planet-signals-marker-distance", ("distance", distance.ToString("0")));
                var arrowSize = handle.GetDimensions(Font, arrowLabel, UIScale * 0.8f);
                handle.DrawString(Font, tip - direction * size * 4f - arrowSize / 2f, arrowLabel, UIScale * 0.8f, WgExpeditionColor);
                continue;
            }

            var color = mine ? WgExpeditionColor : signal.Kind switch
            {
                WGPlanetSignalKind.Site => WGPlanetCargoHubSites.IsHub(signal.Name) ? WGPlanetCargoHubSites.Colour : WgSiteColor,
                WGPlanetSignalKind.Person => signal.Condition == WGPlanetSignalCondition.Alive ? WgPersonColor : WgPersonHurtColor,
                _ => WgVesselColor,
            };
            if (signal.Kind == WGPlanetSignalKind.Person)
            {
                handle.DrawLine(point - new Vector2(size, 0f), point + new Vector2(size, 0f), color);
                handle.DrawLine(point - new Vector2(0f, size), point + new Vector2(0f, size), color);
            }
            else if (signal.Kind == WGPlanetSignalKind.Site)
            {
                var corner = mine ? size * 1.6f : size;
                handle.DrawLine(point + new Vector2(0f, -corner), point + new Vector2(corner, 0f), color);
                handle.DrawLine(point + new Vector2(corner, 0f), point + new Vector2(0f, corner), color);
                handle.DrawLine(point + new Vector2(0f, corner), point + new Vector2(-corner, 0f), color);
                handle.DrawLine(point + new Vector2(-corner, 0f), point + new Vector2(0f, -corner), color);
            }
            else
            {
                handle.DrawRect(UIBox2.FromDimensions(point - new Vector2(size, size), new Vector2(size, size) * 2f), color, false);
            }

            var label = signal.Kind == WGPlanetSignalKind.Person
                ? Loc.GetString("wg-planet-signals-marker-person", ("name", signal.Name), ("condition", WGPlanetSignalsWindow.ConditionOf(signal)))
                : signal.Kind == WGPlanetSignalKind.Site
                ? mine
                    ? Loc.GetString("wg-planet-signals-marker-expedition", ("site", WGPlanetSignalsWindow.NameOf(signal)))
                    : WGPlanetSignalsWindow.NameOf(signal)
                : Loc.GetString("wg-planet-signals-marker-vessel", ("name", WGPlanetSignalsWindow.NameOf(signal)),
                    ("layer", WGPlanetSignalsWindow.LayerOf(signal)));
            var labelSize = handle.GetDimensions(Font, label, UIScale * 0.8f);
            WgBeginTag(point, point + new Vector2(size * 2f, -labelSize.Y / 2f), mine ? 2 : 0, always: mine);
            WgTagLine(handle, label, UIScale * 0.8f, color);
        }
    }

    /// <summary>Lets a world pinned to the scope's rim, which has no tag, be clicked by its blip alone.</summary>
    private void WgAddPlanetBlipHit(EntityUid body, Vector2 uiPosition)
    {
        if (EntManager.HasComponent<WGPlanetSignalsComponent>(body))
            _wgPlanetHits.Add((body, uiPosition * UIScale, 16f * UIScale, default));
    }

    /// <summary>
    /// Over a world's name on the radar out in space: how many vessels, sites and people it holds, and whether the ship's
    /// expedition is there, added to the name's tag. A world drawn here is off the scope or has no scope circle, so only
    /// its blip and name take a click.
    /// </summary>
    private void WgDrawPlanetSignalTag(DrawingHandleScreen handle, EntityUid body, Vector2 uiPosition, Vector2 labelOffset, string label)
    {
        if (!EntManager.TryGetComponent<WGPlanetSignalsComponent>(body, out var signals))
            return;

        var labelAt = (uiPosition + labelOffset) * UIScale;
        var labelSize = handle.GetDimensions(Font, label, UIScale * 0.9f);
        _wgPlanetHits.Add((body, uiPosition * UIScale, 16f * UIScale, UIBox2.FromDimensions(labelAt, labelSize)));

        int vessels = 0, sites = 0;
        var ours = WgOurGrid();
        var mine = false;
        _wgDepots.Clear();
        foreach (var signal in signals.Signals)
        {
            if (signal.Kind == WGPlanetSignalKind.Vessel)
                vessels++;
            else if (signal.Kind == WGPlanetSignalKind.Site)
            {
                sites++;
                if (WGPlanetCargoHubSites.IsHub(signal.Name))
                    _wgDepots.Add(signal.Name);
            }
            mine |= ours != null && signal.Expedition == ours;
        }

        if (vessels == 0 && sites == 0 && signals.People == 0)
            return;

        // Stacked upward from the name, so the detailed lines below it keep their place. They show while the name
        // stands alone; in a stack only the name is listed.
        var tag = Loc.GetString("wg-planet-signals-tag", ("vessels", vessels), ("sites", sites), ("people", signals.People));
        WgTagAbove(handle, tag, UIScale * 0.7f, WgSiteColor);

        // A cargo depot on the world is named over it, in the depots' colour.
        foreach (var depot in _wgDepots)
        {
            var line = Loc.GetString("wg-planet-signals-tag-depot", ("depot", Loc.GetString(depot)));
            WgTagAbove(handle, line, UIScale * 0.8f, WGPlanetCargoHubSites.Colour);
        }

        if (mine)
            WgTagAbove(handle, Loc.GetString("wg-planet-signals-tag-expedition"), UIScale * 0.7f, WgExpeditionColor);
    }

    /// <summary>
    /// Answers a left click: the signal list of the world under it, else the radar menu (RadarMenu). False when the
    /// press was a drag or this radar's clicks aim guns. A ship or drone over a world's circle is what was clicked.
    /// </summary>
    private bool WgTryOpenPlanetSignals(Vector2 pixel, Vector2 relative)
    {
        var pressed = _wgPressedAt;
        _wgPressedAt = null;
        if (OnRadarClick != null || pressed is not { } down || (down - pixel).Length() > 6f * UIScale)
            return false;

        if (!WgGridUnderClick(relative))
        {
            foreach (var (body, centre, radius, label) in _wgPlanetHits)
            {
                if ((pixel - centre).Length() > radius && !label.Contains(pixel))
                    continue;

                // The world's menu lists its signals among the rest; a radar without it lists them at once.
                if (!WgOpenWorldMenu(body))
                    WgShowPlanetSignals(body);
                return true;
            }
        }

        return WgOpenRadarMenu(relative);
    }

    /// <summary>Opens the signal list of a world.</summary>
    private void WgShowPlanetSignals(EntityUid body)
    {
        _wgSignalsWindow ??= new WGPlanetSignalsWindow();
        _wgSignalsWindow.Show(body, WgOurGrid());
    }

    /// <summary>
    /// Whether a grid the radar draws lies within a few pixels of a click, a planet's own ground aside: its drawn outline
    /// is tested on the screen, so a tiny or distant grid counts the same as a large one.
    /// </summary>
    private bool WgGridUnderClick(Vector2 relative)
    {
        var pixel = relative * UIScale;
        var reach = 8f * UIScale;
        var transform = EntManager.System<SharedTransformSystem>();
        foreach (var grid in _grids)
        {
            // Only a grid the radar shows can be what was clicked; a hidden one leaves the world clickable.
            if (EntManager.HasComponent<MapComponent>(grid.Owner) || !WgContactShown(grid.Owner))
                continue;

            var gridToView = transform.GetWorldMatrix(grid.Owner) * _wgWorldToView;
            var local = grid.Comp.LocalAABB;
            var box = new Box2(Vector2.Transform(local.BottomLeft, gridToView), Vector2.Transform(local.BottomLeft, gridToView));
            box = box.ExtendToContain(Vector2.Transform(local.BottomRight, gridToView));
            box = box.ExtendToContain(Vector2.Transform(local.TopLeft, gridToView));
            box = box.ExtendToContain(Vector2.Transform(local.TopRight, gridToView));
            if (box.Enlarged(reach).Contains(pixel))
                return true;
        }

        return false;
    }

    /// <summary>The signal list of the world a planet layer belongs to.</summary>
    private WGPlanetSignalsComponent? WgSignalsOfLayer(EntityUid map)
    {
        var network = EntManager.TryGetComponent<WGOrbitLayerComponent>(map, out var orbit) ? orbit.Network
            : EntManager.TryGetComponent<WGPlanetLayerComponent>(map, out var layer) ? layer.Network
            : null;
        if (network == null)
            return null;

        var query = EntManager.AllEntityQueryEnumerator<WGSectorPlanetComponent, WGPlanetSignalsComponent>();
        while (query.MoveNext(out _, out var sector, out var signals))
        {
            if (sector.Network == network)
                return signals;
        }

        return null;
    }

    /// <summary>The drawn radius of a world's circle, in pixels.</summary>
    private float WgPlanetRadius(EntityUid body)
    {
        var xform = EntManager.GetComponent<TransformComponent>(body);
        if (!EntManager.TryGetComponent<StarSystemMapComponent>(xform.MapUid, out var system) || system.StarSystem == null)
            return 0f;

        var position = xform.LocalPosition;
        foreach (var planet in system.StarSystem.Planets)
        {
            if ((planet.Position - position).LengthSquared() < 1f)
                return WGPlanetScanReader.ScopeRadius(planet, WgSurfaceOf(body)) * _wgViewScale;
        }

        return 0f;
    }

    private WGPlanetSurfacePrototype? WgSurfaceOf(EntityUid body)
    {
        return EntManager.TryGetComponent<WGSectorPlanetComponent>(body, out var sector) && sector.Surface is { } id &&
            IoCManager.Resolve<IPrototypeManager>().TryIndex(id, out var surface)
            ? surface
            : null;
    }

    /// <summary>The ship this radar is on, as the signals name it.</summary>
    private NetEntity? WgOurGrid()
    {
        return EntManager.TryGetComponent<TransformComponent>(_consoleEntity, out var xform) && xform.GridUid is { } grid
            ? EntManager.GetNetEntity(grid)
            : null;
    }
}
