using Content.Shared.Movement.Events;
using Robust.Shared.Audio;

namespace Content.Shared._WG.GroundCover;

/// <summary>Steps on frozen water sound of ice rather than a splash, or crunch when snow covers it.</summary>
public sealed class WGFrozenWaterSystem : EntitySystem
{
    [Dependency] private SharedWGSnowSlowSystem _snow = default!;

    private static readonly SoundSpecifier IceSteps = new SoundCollectionSpecifier("FootstepFloor");
    private static readonly SoundSpecifier SnowSteps = new SoundCollectionSpecifier("FootstepSnow");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WGFreezableWaterComponent, GetFootstepSoundEvent>(OnFootstep);
    }

    private void OnFootstep(Entity<WGFreezableWaterComponent> ent, ref GetFootstepSoundEvent args)
    {
        if (!ent.Comp.Frozen || args.Sound != null)
            return;

        // Left unset, the water's own splash would follow, so snow on the ice answers here.
        args.Sound = _snow.OnCover(args.User) ? SnowSteps : IceSteps;
    }
}
