using System.Numerics;
using Content.Client._WG.Caverns;
using Content.Shared._WG.Planets;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    private WGCavernViewSystem? _wgCavernView;

    /// <summary>Adds the cavern under a ground layer as a pass and floor of the view, when a hole shows it to an observer on that ground.</summary>
    // Lower passes draw first and the ground's empty tiles draw nothing, so the cavern shows only through its holes.
    // A mouth, not any empty tile, opens it: unloaded ground at the edge of a far view keeps the sky it always had.
    // Seen from above the ground there is no pass: the server sends no cavern there, and the hole stays dark.
    private void WgAddCavernPass(EntityUid ground, float groundDepth, float ownDepth, ref float? occludeBelowDepth)
    {
        _wgCavernView ??= _entityManager.System<WGCavernViewSystem>();

        var onGround = _entityManager.GetComponentOrNull<TransformComponent>(_player.LocalEntity)?.MapUid == ground;
        var known = _wgCavernView.TryGetCavernBelow(ground, out var cavern);
        var mouth = onGround && known && _wgCavernView.AnyHoleWithin(ground, WgLevelView(ground, groundDepth, ownDepth));
        if (WGCavernViewSystem.CavernPassDepth(groundDepth, known, mouth, onGround) is not { } depth)
            return;

        _zPasses.Add((cavern!.Value, depth, false, false));
        occludeBelowDepth = depth;
    }

    /// <summary>The world box a pass at this depth shows, plus a tile.</summary>
    private Box2 WgLevelView(EntityUid ground, float depth, float ownDepth)
    {
        var drawBox = GetDrawBox();
        var bottomLeft = _eyeManager.ScreenToMap(drawBox.BottomLeft).Position;
        var bottomRight = _eyeManager.ScreenToMap(drawBox.BottomRight).Position;
        var topLeft = _eyeManager.ScreenToMap(drawBox.TopLeft).Position;
        var topRight = _eyeManager.ScreenToMap(drawBox.TopRight).Position;

        // The screen's corners on the map; a turned eye turns them, so take the box round all four.
        var min = Vector2.Min(Vector2.Min(bottomLeft, bottomRight), Vector2.Min(topLeft, topRight));
        var max = Vector2.Max(Vector2.Max(bottomLeft, bottomRight), Vector2.Max(topLeft, topRight));
        var view = new Box2(min, max);

        var rotation = _fallbackEye?.Rotation ?? Angle.Zero;
        return WGCavernViewSystem.LevelViewBox(view, rotation, depth, ownDepth, WGPlanetView.Shrink(_entityManager, ground, _player.LocalEntity)).Enlarged(1f);
    }
}
