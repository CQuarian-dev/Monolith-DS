using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Map;

// -- LuaM: упрощённые замены помощников радара Wolfgate (RadarLabels, RadarMenu, Cockpit), которые нужны планетным слоям радара; модули не переносим
namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private Vector2 _luamTagBelow;
    private Vector2 _luamTagAbove;

    private void WgBeginTag(Vector2 anchor, Vector2 position, int priority = 0, bool always = false, int? key = null)
    {
        _luamTagBelow = position;
        _luamTagAbove = position;
    }

    private void WgTagLine(DrawingHandleScreen handle, string text, float scale, Color color)
    {
        handle.DrawString(Font, _luamTagBelow, text, scale, color);
        _luamTagBelow.Y += handle.GetDimensions(Font, text, scale).Y;
    }

    private void WgTagAbove(DrawingHandleScreen handle, string text, float scale, Color color)
    {
        _luamTagAbove.Y -= handle.GetDimensions(Font, text, scale).Y;
        handle.DrawString(Font, _luamTagAbove, text, scale, color);
    }

    private bool WgOpenWorldMenu(EntityUid body)
    {
        return false;
    }

    private bool WgOpenRadarMenu(Vector2 relative)
    {
        return false;
    }

    private bool WgContactShown(EntityUid grid)
    {
        return true;
    }

    public bool WgCockpitTryGetWorldToView(out Matrix3x2 worldToView, out MapId map)
    {
        worldToView = default;
        map = MapId.Nullspace;
        if (_coordinates == null || _rotation == null ||
            !EntManager.TryGetComponent<TransformComponent>(_coordinates.Value.EntityId, out var xform) ||
            xform.MapID == MapId.Nullspace)
            return false;

        var centre = _transform.ToMapCoordinates(_coordinates.Value).Offset(_rotation.Value.RotateVec(Offset));
        var worldToShuttle = Matrix3Helpers.CreateTranslation(-centre.Position) * Matrix3Helpers.CreateRotation(-_rotation.Value);
        var shuttleToView = Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale)) * Matrix3x2.CreateTranslation(MidPointVector);
        worldToView = worldToShuttle * shuttleToView;
        map = xform.MapID;
        return true;
    }
}
