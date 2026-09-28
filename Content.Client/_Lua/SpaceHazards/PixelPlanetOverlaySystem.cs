using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.SpaceHazards;

public sealed partial class PixelPlanetOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private PixelPlanetOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new PixelPlanetOverlay(EntityManager, _prototypes);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
