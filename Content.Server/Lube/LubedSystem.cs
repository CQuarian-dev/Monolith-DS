using Content.Shared.Hands; // LuaM
using Content.Shared.IdentityManagement;
using Content.Shared.Lube;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Random;

namespace Content.Server.Lube;

public sealed partial class LubedSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private NameModifierSystem _nameMod = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LubedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LubedComponent, BeforeGettingEquippedHandEvent>(OnHandPickUp); // LuaM
        SubscribeLocalEvent<LubedComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    private void OnInit(EntityUid uid, LubedComponent component, ComponentInit args)
    {
        _nameMod.RefreshNameModifiers(uid);
    }

    // LuaM-start
    private void OnHandPickUp(EntityUid uid, LubedComponent component, ref BeforeGettingEquippedHandEvent args)
    {
        if (args.Cancelled)
            return;

        if (component.SlipsLeft <= 0)
        {
            RemComp<LubedComponent>(uid);
            _nameMod.RefreshNameModifiers(uid);
            return;
        }
        component.SlipsLeft--;
        args.Cancelled = true;
        var user = args.User;
    // LuaM-end
        _transform.SetCoordinates(uid, Transform(user).Coordinates);
        _transform.AttachToGridOrMap(uid);
        _throwing.TryThrow(uid, _random.NextVector2(), baseThrowSpeed: component.SlipStrength);
        _popup.PopupEntity(Loc.GetString("lube-slip", ("target", Identity.Entity(uid, EntityManager))), user, user, PopupType.MediumCaution);
    }

    private void OnRefreshNameModifiers(Entity<LubedComponent> entity, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("lubed-name-prefix");
    }
}
