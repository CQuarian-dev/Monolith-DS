using Content.Shared._WG.Planets.Flight;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._WG.Planets.Flight;

/// <summary>Converts an existing linear engine for atmospheric flight, keeping its ratings and parts.</summary>
public sealed partial class WGLandingThrusterKitSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrusterSystem _thrusters = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private static readonly SoundSpecifier InstallSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGLandingThrusterKitComponent, AfterInteractEvent>(OnAfterInteract);
    }

    /// <summary>Converts the thruster the kit was used on, consuming the kit.</summary>
    private void OnAfterInteract(Entity<WGLandingThrusterKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!args.CanReach)
        {
            _popup.PopupEntity(Loc.GetString("wg-landing-kit-out-of-reach"), args.User, args.User);
            return;
        }

        if (!TryComp<ThrusterComponent>(target, out var thruster) || thruster.Type != ThrusterType.Linear)
        {
            _popup.PopupEntity(Loc.GetString("wg-landing-kit-wrong-target"), ent, args.User);
            return;
        }

        if (HasComp<WGLandingThrusterComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("wg-landing-kit-already-converted"), target, args.User);
            return;
        }

        var xform = Transform(target);

        if (!xform.Anchored)
        {
            _popup.PopupEntity(Loc.GetString("wg-landing-kit-not-anchored"), ent, args.User);
            return;
        }

        // Keep the original engine's footprint, damage, parts, ratings and power connection.
        EnsureComp<WGLandingThrusterComponent>(target);
        _thrusters.WgRefreshAtmosphereThruster(target, thruster);

        QueueDel(ent.Owner);

        _popup.PopupEntity(Loc.GetString("wg-landing-kit-converted"), target, args.User, PopupType.Medium);
        _audio.PlayEntity(InstallSound, args.User, target);
    }
}
