using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// LuaM: trimmed from SF, shared contour cache, plain DrawLine/DrawCircle, no drone routes, + planets
public sealed partial class ShuttleMapControl
{
    private readonly List<(AmbientSpaceFieldComponent Field, Vector2 Pos, float Radius)> _nebulaFieldScratch = new();
    private Vector2[] _mapNebulaFillScratch = Array.Empty<Vector2>();

    private void DrawMapSpaceHazards(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(AmbientSpaceCVars.AmbientSpaceEffectsQuality) > 0)
            DrawMapNebulaContours(handle, matty, viewBox);

        DrawMapCelestialIcons(handle, matty, viewBox);
        DrawMapDangerousNebulaIcons(handle, matty, viewBox);
        DrawMapPlanets(handle, matty, viewBox);
        DrawMapAsteroidClusters(handle, matty, viewBox);
    }

    private void DrawMapAsteroidClusters(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        if (ViewingMap == MapId.Nullspace ||
            !EntManager.TryGetComponent(Maps.GetMapOrInvalid(ViewingMap), out SectorAsteroidClusterMarkersComponent? markers))
            return;

        var color = Color.FromHex("#C8A26A");
        for (var i = 0; i < markers.Centers.Count && i < markers.Radii.Count; i++)
        {
            var center = markers.Centers[i];
            var radius = markers.Radii[i];
            if (!viewBox.Enlarged(radius + 64f).Contains(center))
                continue;

            var localPos = WorldToMapUi(center, matty);
            var uiRadius = MathF.Max(radius * MinimapScale, 6f);
            handle.DrawCircle(localPos, uiRadius, color.WithAlpha(0.05f));
            handle.DrawCircle(localPos, uiRadius, color.WithAlpha(0.6f), filled: false);
            DrawMapLabel(handle, Loc.GetString("radar-label-sector-asteroid-cluster"), localPos + new Vector2(uiRadius + 4f, 0f), center, color);
        }
    }

    private void DrawMapPlanets(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        var color = Color.FromHex("#7FD8FF");
        var query = EntManager.AllEntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (query.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            var radius = MathF.Max(planet.SpriteRadius, 1f);
            if (!viewBox.Enlarged(radius + 64f).Contains(worldPos))
                continue;

            var localPos = WorldToMapUi(worldPos, matty);
            var uiRadius = MathF.Max(radius * MinimapScale, 4f);
            handle.DrawCircle(localPos, uiRadius, color.WithAlpha(0.08f));
            handle.DrawCircle(localPos, uiRadius, color.WithAlpha(0.8f), filled: false);
            DrawMapLabel(handle, Loc.GetString("radar-label-sector-planet"), localPos + new Vector2(uiRadius + 4f, 0f), worldPos, color);
        }
    }

    private void DrawMapLabel(DrawingHandleScreen handle, string name, Vector2 anchor, Vector2 worldPos, Color color)
    {
        var worldDist = Vector2.Distance(worldPos, Offset);
        var displayedDistance = worldDist < 50f ? $"{worldDist:0.0}" : worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", name), ("distance", displayedDistance));
        var labelDimensions = handle.GetDimensions(_font, labelText, 1f);
        var labelPos = anchor - new Vector2(0f, labelDimensions.Y / 2f);
        handle.DrawString(_font, labelPos + Vector2.One, labelText, Color.Black.WithAlpha(0.5f));
        handle.DrawString(_font, labelPos, labelText, color.WithAlpha(0.9f));
    }

    private Vector2 WorldToMapUi(Vector2 world, Matrix3x2 matty)
    {
        var adjusted = Vector2.Transform(world, matty);
        return ScalePosition(adjusted with { Y = -adjusted.Y });
    }

    private void DrawMapNebulaContours(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        _nebulaFieldScratch.Clear();
        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || field.Seed == 0)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if (!viewBox.Intersects(Box2.CenteredAround(worldPos, new Vector2(radius * 2f, radius * 2f))))
                continue;

            _nebulaFieldScratch.Add((field, worldPos, radius));
        }

        foreach (var (field, worldPos, radius) in _nebulaFieldScratch)
        {
            var points = AmbientSpaceNebulaNoise.GetMidLayerContour(field, radius);
            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawMapFilledContour(handle, points, worldPos, matty, color.WithAlpha(field.HasWeather ? 0.1f : 0.05f));
            DrawMapClosedPolyline(handle, points, worldPos, matty, color.WithAlpha(0.85f));
        }
    }

    private void DrawMapCelestialIcons(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var query = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out _, out var body, out var icon, out var xform))
        {
            if (xform.MapID != mapId || icon.Icon == default)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            var maxRadius = body.Kind == CelestialKind.BlackHole
                ? MathF.Max(body.PullRadius, MathF.Max(body.RadiationRange, body.HazardRadius))
                : MathF.Max(body.RadiationRange, body.HazardRadius);
            if (!viewBox.Enlarged(maxRadius + 64f).Contains(worldPos))
                continue;

            var localPos = WorldToMapUi(worldPos, matty);
            if (body.Kind == CelestialKind.BlackHole)
            {
                DrawMapRing(handle, localPos, body.PullRadius, Color.FromHex("#A040FF").WithAlpha(0.35f));
                DrawMapRing(handle, localPos, body.HazardRadius, Color.FromHex("#D080FF").WithAlpha(0.45f));
                DrawMapRing(handle, localPos, body.EventHorizonRadius, Color.FromHex("#F5F540").WithAlpha(0.55f));
            }
            else
            {
                DrawMapRing(handle, localPos, body.RadiationRange, Color.FromHex("#FFE080").WithAlpha(0.3f));
                DrawMapRing(handle, localPos, body.HazardRadius, Color.FromHex("#FFB020").WithAlpha(0.45f));
            }

            var labelColor = body.Kind == CelestialKind.BlackHole ? Color.FromHex("#D080FF") : Color.FromHex("#FFE080");
            DrawMapHazardIcon(handle, cache, icon, localPos, worldPos, 18f, labelColor);
        }
    }

    private void DrawMapRing(DrawingHandleScreen handle, Vector2 localPos, float radius, Color color)
    {
        if (radius > 1f)
            handle.DrawCircle(localPos, radius * MinimapScale, color, filled: false);
    }

    private void DrawMapDangerousNebulaIcons(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var mapId = ViewingMap;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var icon, out var xform))
        {
            if (xform.MapID != mapId || !field.HasWeather || icon.Icon == default)
                continue;

            var worldPos = _xformSystem.GetWorldPosition(xform);
            if (!viewBox.Enlarged(64f).Contains(worldPos))
                continue;

            DrawMapHazardIcon(handle, cache, icon, WorldToMapUi(worldPos, matty), worldPos, 16f, AmbientSpacePalette.ResolveFieldColor(field));
        }
    }

    private void DrawMapHazardIcon(
        DrawingHandleScreen handle,
        IResourceCache cache,
        RadarBlipIconComponent icon,
        Vector2 localPos,
        Vector2 worldPos,
        float iconBase,
        Color labelColor)
    {
        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var s = iconBase * UIScale * icon.Scale;
        var half = new Vector2(s / 2f, s / 2f);

        TextureResource? secondaryTex = null;
        if (icon.SecondaryIcon is { } sec && sec != default && sec != icon.Icon && cache.TryGetResource(sec, out secondaryTex))
        {
            var gap = s * 0.12f;
            var leftCentre = localPos - new Vector2(half.X + gap * 0.5f, 0f);
            var rightCentre = localPos + new Vector2(half.X + gap * 0.5f, 0f);
            handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            handle.DrawTextureRect(texRes.Texture, new UIBox2(localPos - half, localPos + half));
        }

        if (icon.Label is not { } labelLoc || string.IsNullOrEmpty(labelLoc))
            return;

        var worldDist = Vector2.Distance(worldPos, Offset);
        var displayedDistance = worldDist < 50f ? $"{worldDist:0.0}" : worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", Loc.GetString(labelLoc)), ("distance", displayedDistance));
        var labelDimensions = handle.GetDimensions(_font, labelText, 1f);
        var labelPos = localPos + new Vector2(s * 0.6f, -labelDimensions.Y / 2f);
        handle.DrawString(_font, labelPos + Vector2.One, labelText, Color.Black.WithAlpha(0.5f));
        handle.DrawString(_font, labelPos, labelText, labelColor.WithAlpha(0.9f));
    }

    private void DrawMapFilledContour(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 matty,
        Color color)
    {
        if (worldPoints.Length < 3)
            return;

        var count = worldPoints.Length + 2;
        if (_mapNebulaFillScratch.Length < count)
            _mapNebulaFillScratch = new Vector2[count];

        _mapNebulaFillScratch[0] = WorldToMapUi(worldOffset, matty);
        for (var i = 0; i < worldPoints.Length; i++)
            _mapNebulaFillScratch[i + 1] = WorldToMapUi(worldPoints[i] + worldOffset, matty);

        _mapNebulaFillScratch[count - 1] = _mapNebulaFillScratch[1];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new Span<Vector2>(_mapNebulaFillScratch, 0, count), color);
    }

    private void DrawMapClosedPolyline(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 matty,
        Color color)
    {
        if (worldPoints.Length < 2)
            return;

        var prev = WorldToMapUi(worldPoints[^1] + worldOffset, matty);
        foreach (var local in worldPoints)
        {
            var next = WorldToMapUi(local + worldOffset, matty);
            handle.DrawLine(prev, next, color);
            prev = next;
        }
    }
}
