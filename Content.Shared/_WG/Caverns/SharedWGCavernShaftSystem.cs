using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._WG.Caverns;

/// <summary>Tells an examiner where a shaft goes, what its air is like and how hard the landing is.</summary>
public sealed partial class SharedWGCavernShaftSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>Below this landing multiplier the bottom reads as soft; at zero it is water.</summary>
    public const float SoftLandingBelow = 0.6f;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGCavernShaftComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<WGCavernShaftComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Cavern is not { } cavernId || !_proto.TryIndex(cavernId, out var cavern))
            return;

        using (args.PushGroup(nameof(WGCavernShaftComponent)))
        {
            args.PushMarkup(Loc.GetString("wg-cavern-shaft-examine", ("cavern", Loc.GetString(cavern.Name))));
            args.PushMarkup(Loc.GetString(AirLine(ent.Comp.Air)));
            args.PushMarkup(Loc.GetString(LandingLine(ent.Comp.LandingMultiplier)));
        }
    }

    /// <summary>The examine key for a shaft's air.</summary>
    public static string AirLine(WGCavernAir air)
    {
        return air switch
        {
            WGCavernAir.Foul => "wg-cavern-shaft-air-foul",
            WGCavernAir.Thin => "wg-cavern-shaft-air-thin",
            WGCavernAir.Toxic => "wg-cavern-shaft-air-toxic",
            WGCavernAir.Scalding => "wg-cavern-shaft-air-scalding",
            WGCavernAir.Freezing => "wg-cavern-shaft-air-freezing",
            _ => "wg-cavern-shaft-air-breathable",
        };
    }

    /// <summary>The examine key for a shaft's landing: water at zero, soft below 0.6, hard otherwise.</summary>
    public static string LandingLine(float multiplier)
    {
        if (multiplier <= 0f)
            return "wg-cavern-shaft-landing-water";

        return multiplier < SoftLandingBelow ? "wg-cavern-shaft-landing-soft" : "wg-cavern-shaft-landing-hard";
    }
}
