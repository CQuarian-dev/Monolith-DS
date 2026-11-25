using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Content.Shared._WG.Planets.Forecast;
using Content.Shared.Atmos;
using Content.Shared.Weather;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WG.Planets.Forecast;

/// <summary>Weather forecast consoles: a sector world's season, its weather now and what the scheduler has rolled next.</summary>
public sealed partial class WGWeatherForecastConsoleSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private WGPlanetClimateSystem _climate = default!;
    [Dependency] private WGPlanetWeatherSystem _weather = default!;

    /// <summary>Rows starting within this are named as likely; later ones only as possible.</summary>
    public static readonly TimeSpan LikelyWithin = TimeSpan.FromMinutes(45);

    /// <summary>Wind at or above this many tiles a second is flagged.</summary>
    public const float WindyAt = 1.5f;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly List<WGScheduledWeather> _schedule = new();
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WGWeatherForecastConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<WGWeatherForecastConsoleComponent, WGWeatherForecastSelectMessage>(OnSelect);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextRefresh)
            return;
        _nextRefresh = _timing.CurTime + RefreshInterval;

        var consoles = EntityQueryEnumerator<WGWeatherForecastConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, WGWeatherForecastUiKey.Key))
                _ui.SetUiState(uid, WGWeatherForecastUiKey.Key, BuildState((uid, console)));
        }
    }

    private void OnOpened(Entity<WGWeatherForecastConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, WGWeatherForecastUiKey.Key, BuildState(ent));
    }

    private void OnSelect(Entity<WGWeatherForecastConsoleComponent> ent, ref WGWeatherForecastSelectMessage args)
    {
        ent.Comp.Selected = args.World;
        _ui.SetUiState(ent.Owner, WGWeatherForecastUiKey.Key, BuildState(ent));
    }

    /// <summary>What a console shows: the worlds to pick from and the picked one's forecast.</summary>
    public WGWeatherForecastState BuildState(Entity<WGWeatherForecastConsoleComponent> ent)
    {
        var state = new WGWeatherForecastState();
        var worlds = EntityQueryEnumerator<WGPlanetNetworkComponent, WGPlanetWeatherComponent>();
        while (worlds.MoveNext(out var uid, out _, out var weather))
        {
            state.Worlds.Add(new WGForecastWorld { World = GetNetEntity(uid), Name = weather.PlanetName });
        }

        state.Worlds.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        if (state.Worlds.Count == 0)
            return state;

        EntityUid? selected = null;
        if (ent.Comp.Selected is { } picked && TryGetEntity(picked, out var pickedUid) && HasComp<WGPlanetWeatherComponent>(pickedUid))
            selected = pickedUid;
        else if (Transform(ent).MapUid is { } map && _weather.TryGetWorld(map, out var here))
            selected = here;
        selected ??= GetEntity(state.Worlds[0].World);

        state.Selected = GetNetEntity(selected.Value);
        Forecast(selected.Value, state);
        return state;
    }

    /// <summary>Fills the season and rows for one world.</summary>
    private void Forecast(EntityUid world, WGWeatherForecastState into)
    {
        if (!TryComp<WGPlanetWeatherComponent>(world, out var state) || !_proto.TryIndex(state.Profile, out var profile) ||
            !_weather.TryGetSchedule(world, _schedule))
        {
            return;
        }

        var now = _timing.CurTime;
        var season = _weather.GetSeason(state, profile, now);
        into.Season = season?.Name;
        into.SeasonColor = season?.Color ?? Color.White;
        into.Cycling = _cfg.GetCVar(PlanetCVars.PlanetWeather);

        var horizon = now + WGPlanetWeatherSystem.ForecastHorizon;
        TimeSpan? rowStart = null;
        var rowEnd = now;
        ProtoId<WeatherPrototype>? rowWeather = null;
        var rowClear = false;
        var rowHasMain = false;

        for (var i = 0; i < _schedule.Count; i++)
        {
            var entry = _schedule[i];
            if (entry.Start >= horizon)
                break;

            var clear = entry.Weather == null;
            var main = entry.Phase == WGStormPhase.Main;
            // Each storm is one row from its telegraph to its end, and each clear spell another; the weather now is
            // always its own row.
            var startsRow = i <= 1 || clear != rowClear || entry.Phase == WGStormPhase.Telegraph || main && rowHasMain;
            if (startsRow)
            {
                if (rowStart is { } start)
                    AddRow(into, state, profile, start, rowEnd, rowWeather, now);
                rowStart = entry.Start;
                rowWeather = entry.Weather;
                rowClear = clear;
                rowHasMain = main;
            }
            else if (main)
            {
                rowWeather = entry.Weather;
                rowHasMain = true;
            }

            rowEnd = entry.End;
        }

        if (rowStart is { } last)
            AddRow(into, state, profile, last, rowEnd, rowWeather, now);

        // With the cycle off only the weather now (an admin's, or clear) is real.
        if (!into.Cycling && into.Rows.Count > 1)
            into.Rows.RemoveRange(1, into.Rows.Count - 1);
    }

    private void AddRow(WGWeatherForecastState into, WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile,
        TimeSpan start, TimeSpan end, ProtoId<WeatherPrototype>? weather, TimeSpan now)
    {
        var index = into.Rows.Count;
        var row = new WGForecastRow
        {
            InSeconds = (float) Math.Max(0, (start - now).TotalSeconds),
            MinuteOfDay = _weather.GetMinuteOfDay(state, profile, start > now ? start : now),
            Weather = weather?.Id ?? string.Empty,
            Certainty = index <= 1 ? WGForecastCertainty.Exact
                : start - now <= LikelyWithin ? WGForecastCertainty.Likely
                : WGForecastCertainty.Possible,
        };

        if (_weather.ResolveWolfWeather(state.Profile, weather) is { } wolfId && _proto.TryIndex(wolfId, out var wolf))
        {
            row.Wind = wolf.Wind.Length() >= WindyAt;
            row.Lightning = wolf.Lightning != null;
        }

        var kelvin = index == 0 && !float.IsNaN(state.Temperature)
            ? state.Temperature
            : _climate.GetTarget(state, profile, start + (end - start) / 2, weather);
        row.Celsius = (int) MathF.Round(kelvin - Atmospherics.T0C);
        into.Rows.Add(row);
    }
}
