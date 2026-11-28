using System.Linq;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Damage;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._WG.Planets.Parachute;
using Content.Shared._WG.Planets;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server._WG.Planets.Flight;

/// <summary>Unprotected orbital falls sever one arm and one leg on surface impact.</summary>
public sealed partial class WGOrbitalMobFallSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private static readonly SoundSpecifier Splat = new SoundPathSpecifier("/Audio/Effects/gib1.ogg");

    // LuaM-start
    private static readonly SoundSpecifier BodyFall = new SoundCollectionSpecifier("BodyFall");

    private void OnMobImpact(Entity<MobStateComponent> ent, ref CEZFallingDamageCalculateEvent args)
    {
        if (args.Fallen == ent.Owner && !HasComp<WGOrbitalMobFallComponent>(ent) && TryComp<CEZPhysicsComponent>(ent, out var body) && body.Velocity < 0)
            _audio.PlayPvs(BodyFall, ent);
    }
    // LuaM-end

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateComponent, CEZFallingDamageCalculateEvent>(OnMobImpact); // LuaM
        SubscribeLocalEvent<MobStateComponent, CEZLevelFallMapEvent>(OnFall);
        SubscribeLocalEvent<WGOrbitalMobFallComponent, CEZFallingDamageCalculateEvent>(OnCalculate);
        SubscribeLocalEvent<WGOrbitalMobFallComponent, CEZLevelHitEvent>(OnHit, after: new[] { typeof(CEZLevelDamageSystem) });
    }

    /// <inheritdoc/>
    // The mark otherwise only comes off on a hard impact, so a faller caught by a jetpack, or set down gently, kept
    // it, and the next ordinary hard landing was taken for the orbital one. A hard landing is handled in z-physics,
    // which raises the hit before the body comes to rest, so OnHit always sees the mark first.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WGOrbitalMobFallComponent, CEZPhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out var body))
        {
            var flying = body.GravityMultiplier <= 0f;
            var atRest = body.Velocity == 0f
                         && body.LocalPosition - body.CachedGroundHeight <= CESharedZLevelsSystem.AirborneHeightThreshold;

            if (flying || atRest)
                RemCompDeferred<WGOrbitalMobFallComponent>(uid);
        }
    }

    private void OnFall(Entity<MobStateComponent> ent, ref CEZLevelFallMapEvent args)
    {
        if (ent.Comp.CurrentState == MobState.Dead || HasComp<WGOrbitalMobFallComponent>(ent)
            || HasComp<WGParachutedComponent>(ent))
            return;
        var map = Transform(ent).MapUid;
        if (map == null || !_zLevels.TryMapUp(map.Value, out var above)
            || !TryComp<WGOrbitLayerComponent>(above, out var orbit)
            || orbit.Network is not { } net || !TryGetEntity(net, out var network)
            || !TryComp<WGPlanetNetworkComponent>(network, out var planet))
            return;
        EnsureComp<WGOrbitalMobFallComponent>(ent).Ground = planet.GroundMap;
        _chat.TryEmoteWithChat(ent, "Scream");
    }

    /// <summary>A downward impact on the ground, or on a planet layer below it that the faller dropped into.</summary>
    private bool IsSurfaceImpact(EntityUid uid, WGOrbitalMobFallComponent fall)
    {
        var map = Transform(uid).MapUid;
        var onSurface = map == fall.Ground
            || TryComp<CEZMapComponent>(map, out var zMap) && zMap.Depth < 0 && HasComp<WGPlanetLayerComponent>(map);

        return onSurface && TryComp<CEZPhysicsComponent>(uid, out var physics) && physics.Velocity < 0;
    }

    private void OnCalculate(Entity<WGOrbitalMobFallComponent> ent, ref CEZFallingDamageCalculateEvent args)
    {
        // Replace the lethal accumulated fall damage, rather than resurrecting a corpse afterwards.
        if (args.Fallen == ent.Owner && IsSurfaceImpact(ent, ent.Comp))
            args.DamageMultiplier = 0;
    }

    private void OnHit(Entity<WGOrbitalMobFallComponent> ent, ref CEZLevelHitEvent args)
    {
        var surface = IsSurfaceImpact(ent, ent.Comp);
        RemComp<WGOrbitalMobFallComponent>(ent);
        if (!surface || !TryComp<MobStateComponent>(ent, out var mob) || mob.CurrentState == MobState.Dead
            || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        _audio.PlayPvs(Splat, ent); // LuaM: ран Onyx у нас нет
        SeverOne(ent, BodyPartType.Arm);
        SeverOne(ent, BodyPartType.Leg);

        if (_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var critical))
        {
            var missing = critical.Value + 1 - damageable.TotalDamage;
            if (missing > 0)
            {
                var damage = new DamageSpecifier(damageable.Damage);
                damage.DamageDict.TryGetValue("Blunt", out var blunt);
                damage.DamageDict["Blunt"] = blunt + missing;
                _damage.SetDamage(ent, damageable, damage);
            }
        }
    }

    private void SeverOne(EntityUid uid, BodyPartType type)
    {
        var parts = _body.GetBodyChildrenOfType(uid, type).Where(p => p.Component.CanSever).ToArray();
        if (parts.Length == 0)
            return;
        var part = _random.Pick(parts).Id;
        var amputate = new AmputateAttemptEvent(part);
        RaiseLocalEvent(part, ref amputate);
    }
}

[RegisterComponent, UnsavedComponent]
public sealed partial class WGOrbitalMobFallComponent : Component
{
    public EntityUid Ground;
}
