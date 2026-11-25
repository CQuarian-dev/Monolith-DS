using System.Numerics;
using Content.Shared._WG.Planets.Parachute;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._WG.Planets.Parachute;

/// <summary>Draws a parachute on its wearer: the pack strapped to them, and the open canopy over them in a fall.</summary>
public sealed partial class WGParachuteVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier Canopy =
        new SpriteSpecifier.Rsi(new ResPath("_WG/Planets/Effects/parachute_canopy.rsi"), "canopy");

    private static readonly SpriteSpecifier Pack =
        new SpriteSpecifier.Rsi(new ResPath("_WG/Planets/Objects/parachute.rsi"), "icon");

    /// <summary>The pack is drawn small and low, so it reads as worn rather than as an item lying on the wearer.</summary>
    private static readonly Vector2 PackScale = new(0.6f, 0.6f);
    private static readonly Vector2 PackOffset = new(0f, -0.1f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGParachutedComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<WGParachutedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<WGParachutedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent, sprite), CanopyKey.Pack, out _, false))
        {
            var pack = _sprite.AddLayer((ent, sprite), Pack);
            _sprite.LayerMapSet((ent, sprite), CanopyKey.Pack, pack);
            _sprite.LayerSetScale((ent, sprite), pack, PackScale);
            _sprite.LayerSetOffset((ent, sprite), pack, PackOffset);
        }

        var has = _sprite.LayerMapTryGet((ent, sprite), CanopyKey.Key, out var layer, false);

        if (!ent.Comp.Deployed)
        {
            if (has)
                _sprite.RemoveLayer((ent, sprite), layer);

            return;
        }

        if (has)
            return;

        layer = _sprite.AddLayer((ent, sprite), Canopy);
        _sprite.LayerMapSet((ent, sprite), CanopyKey.Key, layer);

        // The canopy art has its harness at the bottom edge; sit that on the wearer's top.
        _sprite.LayerSetOffset((ent, sprite), layer, new Vector2(0f, _sprite.GetLocalBounds((ent, sprite)).Height / 2f + 0.5f));
    }

    private void OnShutdown(Entity<WGParachutedComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), CanopyKey.Key, out var layer, false))
            _sprite.RemoveLayer((ent, sprite), layer);

        // Looked up afresh: removing the canopy may have shifted the pack's index.
        if (_sprite.LayerMapTryGet((ent, sprite), CanopyKey.Pack, out var pack, false))
            _sprite.RemoveLayer((ent, sprite), pack);
    }

    private enum CanopyKey
    {
        Key,
        Pack,
    }
}
