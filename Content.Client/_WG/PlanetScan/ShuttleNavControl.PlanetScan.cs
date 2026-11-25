using System.Numerics;
using Content.Client._WG.PlanetScan;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    /// <summary>The smallest radius a world is drawn at on the radar, in virtual pixels.</summary>
    private const float WgScopeMinRadius = 12f;

    private WGPlanetScopeSprites? _wgScopeSprites;

    /// <summary>Lays the planet scan over the radar.</summary>
    private void WgAddPlanetScanner()
    {
        var scanner = new WGPlanetScanner(this,
            () => WgCockpitTryGetWorldToView(out var worldToView, out var map) ? (worldToView, map) : null,
            WgScopeMinRadius)
        {
            Name = "PlanetScan",
        };
        AddChild(scanner);
        LayoutContainer.SetAnchorPreset(scanner, LayoutContainer.LayoutPreset.Wide);
    }

    /// <summary>Draws the star system's worlds as themselves, at a size that reads as a world; false leaves them to upstream.</summary>
    private bool WgDrawScopePlanets(DrawingHandleScreen handle, PlanetarySystem system, Matrix3x2 worldToView, EntityUid? mapUid)
    {
        if (!EntManager.TryGetComponent<TransformComponent>(mapUid, out var xform))
            return false;

        _wgScopeSprites ??= new WGPlanetScopeSprites(EntManager, IoCManager.Resolve<IPrototypeManager>());
        _wgScopeSprites.Draw(handle, xform.MapID, system, worldToView, WgScopeMinRadius * UIScale,
            new UIBox2(Vector2.Zero, PixelSize));
        return true;
    }

    /// <summary>
    /// Whether a radar beacon is a world on the scope, whose sprite and hover scan stand in for its label. Records where
    /// it is for a click to open its signal list.
    /// </summary>
    private bool WgIsScopePlanet(EntityUid uid, Vector2 uiPosition)
    {
        if (WgPlanetRadius(uid) <= 0f)
            return false;

        _wgPlanetHits.Add((uid, uiPosition * UIScale, MathF.Max(WgScopeMinRadius * UIScale, WgPlanetRadius(uid)), default));
        return true;
    }
}
