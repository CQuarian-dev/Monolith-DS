using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed class NebulaTintOverlay : Overlay
{
    private const float MaxAlpha = 0.1f;
    private const float EdgeFade = 150f;
    private const float FadeSpeed = 2f;

    private readonly IEntityManager _entities;
    private readonly IConfigurationManager _cfg;
    private readonly IEyeManager _eye;
    private readonly SharedTransformSystem _transform;

    private Vector3 _color;
    private float _strength;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NebulaTintOverlay(IEntityManager entities, IConfigurationManager cfg)
    {
        _entities = entities;
        _cfg = cfg;
        _eye = IoCManager.Resolve<IEyeManager>();
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var target = 0f;
        var color = Vector3.Zero;
        if (_cfg.GetCVar(AmbientSpaceCVars.AmbientSpaceTint) &&
            _cfg.GetCVar(AmbientSpaceCVars.AmbientSpaceEffectsQuality) > 0)
        {
            target = Sample(_eye.CurrentEye.Position, out color);
        }

        var blend = 1f - MathF.Exp(-FadeSpeed * args.DeltaSeconds);
        if (target > 0f)
            _color = _strength < 0.01f ? color : Vector3.Lerp(_color, color, blend);

        _strength += (target - _strength) * blend;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _strength > 0.002f && args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
        handle.DrawRect(args.WorldBounds, new Color(_color.X, _color.Y, _color.Z, _strength * MaxAlpha));
    }

    private float Sample(MapCoordinates eye, out Vector3 color)
    {
        color = Vector3.Zero;
        if (eye.MapId == MapId.Nullspace)
            return 0f;

        var total = 0f;
        var strongest = 0f;
        var query = _entities.EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var field, out var xform))
        {
            if (field.Seed == 0 || xform.MapID != eye.MapId)
                continue;

            var radius = MathF.Max(field.Radius, 1f);
            var delta = eye.Position - _transform.GetWorldPosition(xform);
            if (delta.LengthSquared() > radius * radius)
                continue;

            var depth = EdgeDepth(AmbientSpaceNebulaNoise.GetMidLayerContour(field, radius), delta);
            if (depth <= 0f)
                continue;

            var weight = Math.Clamp(depth / EdgeFade, 0f, 1f);
            weight = weight * weight * (3f - 2f * weight);
            var fieldColor = AmbientSpacePalette.ColorFromSeed(field.Seed);
            color += new Vector3(fieldColor.R, fieldColor.G, fieldColor.B) * weight;
            total += weight;
            strongest = MathF.Max(strongest, weight);
        }

        if (total > 0f)
            color /= total;

        return strongest;
    }

    private static float EdgeDepth(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        if (polygon.Length < 3)
            return 0f;

        var inside = false;
        var minDistanceSquared = float.MaxValue;
        var previous = polygon.Length - 1;
        for (var current = 0; current < polygon.Length; current++)
        {
            var a = polygon[current];
            var b = polygon[previous];

            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }

            var edge = b - a;
            var t = Math.Clamp(Vector2.Dot(point - a, edge) / MathF.Max(edge.LengthSquared(), 0.0001f), 0f, 1f);
            minDistanceSquared = MathF.Min(minDistanceSquared, (a + edge * t - point).LengthSquared());
            previous = current;
        }

        return inside ? MathF.Sqrt(minDistanceSquared) : 0f;
    }
}
