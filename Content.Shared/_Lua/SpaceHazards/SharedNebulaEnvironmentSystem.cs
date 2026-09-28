using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.SpaceHazards;

public abstract partial class SharedNebulaEnvironmentSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Prototypes = default!;

    private EntityQuery<NebulaPresenceComponent> _presenceQuery;

    public override void Initialize()
    {
        base.Initialize();
        _presenceQuery = GetEntityQuery<NebulaPresenceComponent>();
        SubscribeLocalEvent<GunComponent, QueryFireRateMultiplierEvent>(OnFireRateQuery);
    }

    private void OnFireRateQuery(Entity<GunComponent> ent, ref QueryFireRateMultiplierEvent args)
    {
        if (Transform(ent.Owner).GridUid is not { } gridUid)
            return;

        var cooldownMultiplier = GetWeaponCooldownMultiplier(gridUid);
        if (cooldownMultiplier <= 1f)
            return;

        var resistance = TryComp(ent.Owner, out NebulaWeaponResistanceComponent? resist)
            ? Math.Clamp(resist.Resistance, 0f, 1f)
            : 0f;
        args.ReloadTimeMul *= float.Lerp(cooldownMultiplier, 1f, resistance);
    }

    public float GetThrustMultiplierRaw(EntityUid gridUid)
    {
        var multiplier = 1f;
        if (!_presenceQuery.TryComp(gridUid, out var presence))
            return multiplier;

        if (presence.ActiveWeathers.Count == 0)
            return Prototypes.TryIndex(presence.Weather, out var fallback) ? MathF.Min(multiplier, fallback.ThrustMultiplier) : multiplier;

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (Prototypes.TryIndex(weatherId, out var weather))
                multiplier = MathF.Min(multiplier, weather.ThrustMultiplier);
        }

        return multiplier;
    }

    public float GetWeaponCooldownMultiplier(EntityUid gridUid)
    {
        var multiplier = 1f;
        if (!_presenceQuery.TryComp(gridUid, out var presence))
            return multiplier;

        if (presence.ActiveWeathers.Count == 0)
            return Prototypes.TryIndex(presence.Weather, out var fallback) ? MathF.Max(multiplier, fallback.WeaponCooldownMultiplier) : multiplier;

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (Prototypes.TryIndex(weatherId, out var weather))
                multiplier = MathF.Max(multiplier, weather.WeaponCooldownMultiplier);
        }

        return multiplier;
    }

    public bool HasRadioBlackout(EntityUid gridUid)
    {
        if (!_presenceQuery.TryComp(gridUid, out var presence))
            return false;

        if (presence.ActiveWeathers.Count == 0)
            return Prototypes.TryIndex(presence.Weather, out var fallback) && fallback.RadioBlackout;

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (Prototypes.TryIndex(weatherId, out var weather) && weather.RadioBlackout)
                return true;
        }

        return false;
    }

    public bool BlocksFtl(EntityUid gridUid)
    {
        if (!_presenceQuery.TryComp(gridUid, out var presence))
            return false;

        if (presence.ActiveWeathers.Count == 0)
            return Prototypes.TryIndex(presence.Weather, out var fallback) && fallback.BlocksFtl;

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (Prototypes.TryIndex(weatherId, out var weather) && weather.BlocksFtl)
                return true;
        }

        return false;
    }
}
