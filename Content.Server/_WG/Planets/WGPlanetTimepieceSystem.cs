using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared._WG.Planets;

namespace Content.Server._WG.Planets;

/// <summary>Shows the weather system's planetary report when the timepiece is used or examined.</summary>
public sealed partial class WGPlanetTimepieceSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WGPlanetWeatherSystem _weather = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGPlanetTimepieceComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WGPlanetTimepieceComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInHand(Entity<WGPlanetTimepieceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        _popup.PopupEntity(GetReport(args.User), args.User, args.User);
        args.Handled = true;
    }

    private void OnExamined(Entity<WGPlanetTimepieceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(GetReport(args.Examiner));
    }

    private string GetReport(EntityUid uid)
    {
        if (!_weather.TryGetReport(uid, out var planet, out var time, out var weather))
            return Loc.GetString("wg-planet-timepiece-no-signal");

        return Loc.GetString("wg-planet-timepiece-report",
            ("planet", planet),
            ("time", time),
            ("weather", weather));
    }
}
