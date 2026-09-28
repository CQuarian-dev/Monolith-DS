using System.Numerics;
using Content.Client._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private const int MaxNavNebulaContours = 20;
    private const int CelestialContourSegments = 48;

    private readonly AmbientSpaceNebulaVisibility _nebulaVisibility;
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, TransformComponent Xform, Vector2 Pos, float Radius)> _nebulaFieldScratch = new();
    private readonly Vector2[] _celestialContourScratch = new Vector2[CelestialContourSegments];
    private Vector2[] _nebulaFillScratch = Array.Empty<Vector2>();

    private void DrawNebulaContours(
        DrawingHandleScreen handle,
        TransformComponent consoleXform,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView,
        Vector2 viewCenter) // LuaM
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (cfg.GetCVar(AmbientSpaceCVars.AmbientSpaceEffectsQuality) <= 0)
            return;

        var mapId = consoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var consolePos = viewCenter; // LuaM: console position > view centre, the view can be panned
        var view = worldToShuttle * shuttleToView;
        var maxDist = WorldRange * MathF.Sqrt(2f) + 64f; // LuaM: WorldRange > half-diagonal, the scanner corners reach further
        var cullBox = Box2.CenteredAround(consolePos, new Vector2(maxDist * 2f, maxDist * 2f));
        var drawn = 0;

        _nebulaFieldScratch.Clear();
        var query = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            var fieldBounds = _nebulaVisibility.GetPotentialDrawBounds(field, worldPos, consolePos, radius);
            if (!cullBox.Intersects(fieldBounds))
                continue;

            _nebulaFieldScratch.Add((uid, field, xform, worldPos, radius));
        }

        _nebulaFieldScratch.Sort((a, b) =>
        {
            var da = (a.Pos - consolePos).LengthSquared();
            var db = (b.Pos - consolePos).LengthSquared();
            return da.CompareTo(db);
        });

        foreach (var (_, field, _, worldPos, radius) in _nebulaFieldScratch)
        {
            if (drawn >= MaxNavNebulaContours)
                break;

            if (field.Seed == 0)
                continue;

            var points = AmbientSpaceNebulaNoise.GetMidLayerContour(field, radius); // LuaM: per-control cache > shared cache
            if (!_nebulaVisibility.HasVisibleMidLayer(mapId, worldPos, points))
                continue;

            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawFilledContour(handle, points, worldPos, view, color.WithAlpha(field.HasWeather ? 0.11f : 0.06f));
            DrawClosedPolyline(handle, points, worldPos, view, color.WithAlpha(0.35f), thickness: 3);
            DrawClosedPolyline(handle, points, worldPos, view, color.WithAlpha(0.9f));
            drawn++;
        }

        DrawCelestialContours(handle, mapId, consolePos, view, maxDist);
    }

    private void DrawCelestialContours(
        DrawingHandleScreen handle,
        MapId mapId,
        Vector2 consolePos,
        Matrix3x2 view,
        float maxDist)
    {
        var query = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (query.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(body.HazardRadius, body.SpriteRadius);
            if (Vector2.Distance(worldPos, consolePos) > maxDist + radius)
                continue;

            BuildCircleContour(_celestialContourScratch, radius);
            var color = body.Kind == CelestialKind.BlackHole
                ? Color.FromHex("#A040FF").WithAlpha(0.85f)
                : Color.FromHex("#FFB020").WithAlpha(0.85f);
            DrawClosedPolyline(handle, _celestialContourScratch, worldPos, view, color);
        }
    }

    private static void BuildCircleContour(Span<Vector2> points, float radius)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * MathF.Tau / points.Length;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }

    private static void DrawClosedPolyline(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 worldToView,
        Color color,
        int thickness = 1)
    {
        if (worldPoints.Length < 2)
            return;

        var prev = Vector2.Transform(worldPoints[^1] + worldOffset, worldToView);
        foreach (var local in worldPoints)
        {
            var next = Vector2.Transform(local + worldOffset, worldToView);
            if (thickness <= 1)
            {
                handle.DrawLine(prev, next, color);
            }
            else
            {
                var dir = next - prev;
                if (dir.LengthSquared() > 0.0001f)
                {
                    var n = Vector2.Normalize(new Vector2(-dir.Y, dir.X)) * 0.75f;
                    handle.DrawLine(prev + n, next + n, color);
                    handle.DrawLine(prev - n, next - n, color);
                }

                handle.DrawLine(prev, next, color);
            }

            prev = next;
        }
    }

    private void DrawFilledContour(
        DrawingHandleScreen handle,
        ReadOnlySpan<Vector2> worldPoints,
        Vector2 worldOffset,
        Matrix3x2 worldToView,
        Color color)
    {
        if (worldPoints.Length < 3)
            return;

        var count = worldPoints.Length + 2;
        if (_nebulaFillScratch.Length < count)
            _nebulaFillScratch = new Vector2[count];

        _nebulaFillScratch[0] = Vector2.Transform(worldOffset, worldToView);
        for (var i = 0; i < worldPoints.Length; i++)
            _nebulaFillScratch[i + 1] = Vector2.Transform(worldPoints[i] + worldOffset, worldToView);

        _nebulaFillScratch[count - 1] = _nebulaFillScratch[1];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new Span<Vector2>(_nebulaFillScratch, 0, count), color);
    }
}
