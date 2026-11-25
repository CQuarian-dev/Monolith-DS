/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core.Overlays;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.StatusEffect;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client._CE.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CEClientZLevelsSystem : CESharedZLevelsSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private Robust.Shared.Configuration.IConfigurationManager _luamCfg = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new CEZLevelBlurOverlay());
        // LuaM-start: тени слоёв только с механиками #4690
        Subs.CVar(_luamCfg, Content.Shared._LuaM.CCVar.LuaMCVars.ZLevelsGridNetworks, enabled =>
        {
            if (enabled && !_overlay.HasOverlay<CEZLevelShadowOverlay>())
                _overlay.AddOverlay(new CEZLevelShadowOverlay(EntityManager));
            else if (!enabled)
                _overlay.RemoveOverlay<CEZLevelShadowOverlay>();
        }, true);
        // LuaM-end

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CEZPhysicsComponent, AfterAutoHandleStateEvent>((uid, comp, _) => WGNoteHeight(uid, comp.LocalPosition)); // WOLFGATE(Performance): a height from the server joins the sprite passes
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var localPosition = ent.Comp.LocalPosition;
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
        WGNoteHeight(ent, ent.Comp.LocalPosition); // WOLFGATE(Performance): spawned off the ground
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZLevelBlurOverlay>();
        _overlay.RemoveOverlay<CEZLevelShadowOverlay>();
    }
}

/// <summary>
/// Pre-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> BEFORE <see cref="AnimationPlayerSystem"/> every render frame,
/// resetting <see cref="SpriteComponent.Offset"/> to the entity's clean base (no Z).
/// This prevents Z from accumulating across frames when no animation writes to the offset
/// (e.g. entities that only animate scale, like SlimeIceBig).
/// </summary>
internal sealed partial class CEClientZLevelsPreAnimSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<CEZPhysicsComponent> _zPhysQuery = default!;
    // WOLFGATE(Performance): the raised list and the lookup that replace the all-entity query
    [Dependency] private CEClientZLevelsSystem _zLevels = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AnimationPlayerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        // Phase 1 (per render frame): strip any Z left from last frame so the animation player
        // always starts from a Z-free base, and Phase 2 can add exactly one Z contribution.
        // WOLFGATE(Performance) START: walk only raised entities; nothing else has a height to strip
        // var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent>();
        // while (query.MoveNext(out var uid, out var zPhys, out var sprite))
        foreach (var uid in _zLevels.WGRaised)
        {
            if (!_zPhysQuery.TryComp(uid, out var zPhys) || !_spriteQuery.TryComp(uid, out var sprite))
                continue;
            // WOLFGATE END

            var localPosition = zPhys.LocalPosition;
            if (sprite.Offset != zPhys.SpriteOffsetDefault) // LuaM: SetOffset rebuilds the sprite matrix even for the same value
                _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault);

            // WOLFGATE(Species) START: only touch the draw depth when the entity takes off or lands
            // Writing it every frame silently undid every other client-side depth change (sneaking under tables,
            // buckling, death).
            var lifted = localPosition > 0;
            if (lifted == zPhys.DrawDepthLifted)
                continue;

            zPhys.DrawDepthLifted = lifted;
            if (lifted)
            {
                zPhys.DrawDepthDefault = sprite.DrawDepth;
                _sprite.SetDrawDepth((uid, sprite), (int)Shared.DrawDepth.DrawDepth.OverMobs);
            }
            else if (sprite.DrawDepth == (int)Shared.DrawDepth.DrawDepth.OverMobs)
            {
                _sprite.SetDrawDepth((uid, sprite), zPhys.DrawDepthDefault);
            }
            // WOLFGATE END
        }

        // Set parent-synced status effect offsets to the parent's current Z value each frame — prevents accumulation.
        var syncQuery = EntityQueryEnumerator<StatusEffectsComponent, SpriteComponent, TransformComponent>();
        while (syncQuery.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            var parent = xform.ParentUid;
            if (_mapGridQuery.HasComp(parent))
                continue;
            if (!_zPhysQuery.TryComp(parent, out var parentZPhys))
                continue;
            var zOffset = new Vector2(0, parentZPhys.LocalPosition * CESharedZLevelsSystem.ZLevelOffset);
            if (sprite.Offset != zOffset) // LuaM
                _sprite.SetOffset((uid, sprite), zOffset);
        }
    }
}

/// <summary>
/// Post-animation pass for Z-level visuals.
/// Runs its <see cref="FrameUpdate"/> AFTER <see cref="AnimationPlayerSystem"/> so that
/// whatever offset the animation player wrote to <see cref="SpriteComponent.Offset"/> this frame
/// (loop animation, one-shot swing, idle bob, etc.) is preserved, and the Z-height contribution
/// is simply added on top.  No animation code needs to know about Z levels.
/// </summary>
internal sealed partial class CEClientZLevelsPostAnimSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    // WOLFGATE(Performance) START: the raised list and the lookups that replace the all-entity query
    [Dependency] private CEClientZLevelsSystem _zLevels = default!;
    [Dependency] private EntityQuery<CEZPhysicsComponent> _zPhysQuery = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    private readonly List<EntityUid> _landed = new();
    // WOLFGATE END

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        // Phase 2: add the Z-height contribution on top of the animation-player's output.
        // At this point sprite.Offset == animationValue (or SpriteOffsetDefault if no anim ran).
        // The offset is counter-rotated by the entity's world angle so it always points world-up,
        // preventing it from orbiting the pivot when the entity has angular velocity (e.g. shurikens).
        // WOLFGATE(Performance) START: walk only raised entities; one that has landed leaves the list
        // var query = EntityQueryEnumerator<CEZPhysicsComponent, SpriteComponent, TransformComponent>();
        // while (query.MoveNext(out var uid, out var zPhys, out var sprite, out var xform))
        _landed.Clear();
        foreach (var uid in _zLevels.WGRaised)
        {
            if (!_zPhysQuery.TryComp(uid, out var zPhys) || !_spriteQuery.TryComp(uid, out var sprite) ||
                !_xformQuery.TryComp(uid, out var xform) || zPhys.LocalPosition == 0)
            {
                _landed.Add(uid);
                continue;
            }
            // WOLFGATE END

            var rawZ = new Vector2(0, zPhys.LocalPosition * CESharedZLevelsSystem.ZLevelOffset);
            Vector2 zOffset;
            if (sprite.NoRotation)
                zOffset = rawZ;
            else
                zOffset = new Angle(-_xform.GetWorldRotation(xform)).RotateVec(rawZ);
            _sprite.SetOffset((uid, sprite), sprite.Offset + zOffset);
        }

        // WOLFGATE(Performance) START: removed after the walk, which can't change the set it enumerates
        foreach (var uid in _landed)
        {
            _zLevels.WGRaised.Remove(uid);
        }
        // WOLFGATE END
    }
}
