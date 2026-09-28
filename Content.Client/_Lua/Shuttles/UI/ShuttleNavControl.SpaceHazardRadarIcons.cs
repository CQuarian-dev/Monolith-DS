using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// LuaM: trimmed from SF, no RadarBlipComponent on the client here and no hover scaling, + planets
public partial class ShuttleNavControl
{
    private const float HazardIconBaseSize = 16f;
    private const float HazardLabelScale = 0.9f;

    private void DrawSpaceHazardRadarIcons(
        DrawingHandleScreen handle,
        TransformComponent consoleXform,
        Matrix3x2 worldToView,
        Vector2 consolePos)
    {
        var mapId = consoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();

        var celestialQuery = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (celestialQuery.MoveNext(out _, out var body, out var icon, out var xform))
        {
            var color = body.Kind == CelestialKind.BlackHole ? Color.FromHex("#D080FF") : Color.FromHex("#FFE080");
            TryDrawHazardIcon(handle, cache, icon, xform, mapId, consolePos, worldToView, color);
        }

        var fieldQuery = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out _, out var field, out var icon, out var xform))
        {
            if (!field.HasWeather)
                continue;

            TryDrawHazardIcon(handle, cache, icon, xform, mapId, consolePos, worldToView, AmbientSpacePalette.ResolveFieldColor(field));
        }

        var planetQuery = EntManager.AllEntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (planetQuery.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID == mapId)
                DrawPlanetMarker(handle, planet, xform, consolePos, worldToView);
        }
    }

    private void DrawPlanetMarker(
        DrawingHandleScreen handle,
        SectorBackgroundPlanetComponent planet,
        TransformComponent xform,
        Vector2 consolePos,
        Matrix3x2 worldToView)
    {
        const float maxDistance = 12000f;
        var color = Color.FromHex("#7FD8FF");
        var worldPos = _transform.GetWorldPosition(xform);
        var worldDist = Vector2.Distance(worldPos, consolePos);
        if (worldDist > maxDistance)
            return;

        var centre = Vector2.Transform(worldPos, worldToView);
        var mid = MidPointVector;
        var offset = centre - mid;
        var edge = MathF.Min(mid.X, mid.Y) * 0.95f;
        var clamped = offset.Length() > edge;
        if (clamped)
        {
            centre = mid + Vector2.Normalize(offset) * edge;
            handle.DrawCircle(centre, 4f * UIScale, color);
        }
        else
        {
            var uiRadius = MathF.Max(planet.SpriteRadius * MinimapScale, 4f * UIScale);
            handle.DrawCircle(centre, uiRadius, color.WithAlpha(0.08f));
            handle.DrawCircle(centre, uiRadius, color.WithAlpha(0.8f), filled: false);
        }

        var displayedDistance = worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", Loc.GetString("radar-label-sector-planet")), ("distance", displayedDistance));
        var labelDimensions = handle.GetDimensions(Font, labelText, HazardLabelScale);
        var s = HazardIconBaseSize * UIScale;
        var labelX = centre.X > mid.X
            ? centre.X - labelDimensions.X * UIScale - s
            : centre.X + s;
        handle.DrawString(Font, new Vector2(labelX, centre.Y - labelDimensions.Y * UIScale / 2f), labelText, UIScale * HazardLabelScale, color);
    }

    private void TryDrawHazardIcon(
        DrawingHandleScreen handle,
        IResourceCache cache,
        RadarBlipIconComponent icon,
        TransformComponent xform,
        MapId mapId,
        Vector2 consolePos,
        Matrix3x2 worldToView,
        Color labelColor)
    {
        if (xform.MapID != mapId || icon.Icon == default)
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var worldDist = Vector2.Distance(worldPos, consolePos);
        if (icon.MaxDistance > 0f && worldDist > icon.MaxDistance)
            return;

        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var centre = Vector2.Transform(worldPos, worldToView);
        var mid = MidPointVector;
        var offset = centre - mid;
        var edge = MathF.Min(mid.X, mid.Y) * 0.95f;
        if (offset.Length() > edge)
            centre = mid + Vector2.Normalize(offset) * edge;

        var s = HazardIconBaseSize * UIScale * icon.Scale;
        var half = new Vector2(s / 2f, s / 2f);

        TextureResource? secondaryTex = null;
        if (icon.SecondaryIcon is { } sec && sec != default && sec != icon.Icon && cache.TryGetResource(sec, out secondaryTex))
        {
            var gap = s * 0.12f;
            var leftCentre = centre - new Vector2(half.X + gap * 0.5f, 0f);
            var rightCentre = centre + new Vector2(half.X + gap * 0.5f, 0f);
            handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            handle.DrawTextureRect(texRes.Texture, new UIBox2(centre - half, centre + half));
        }

        if (icon.Label is not { } labelLoc || string.IsNullOrEmpty(labelLoc))
            return;

        var displayedDistance = worldDist < 50f ? $"{worldDist:0.0}" : worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", Loc.GetString(labelLoc)), ("distance", displayedDistance));
        var labelDimensions = handle.GetDimensions(Font, labelText, HazardLabelScale);
        var labelX = centre.X > mid.X
            ? centre.X - labelDimensions.X * UIScale - s
            : centre.X + s;
        var labelPos = new Vector2(labelX, centre.Y - labelDimensions.Y * UIScale / 2f);
        handle.DrawString(Font, labelPos, labelText, UIScale * HazardLabelScale, labelColor);
    }
}
