using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._WG.Planets;

/// <summary>
/// Phased storms: telegraph, main, end. The phases are rolled ahead into a queue, so the forecast is what will happen.
/// Lightning comes from the world's WolfWeather for the phase.
/// </summary>
public sealed partial class WGPlanetWeatherSystem
{
    /// <summary>How far ahead each world's weather is rolled.</summary>
    public static readonly TimeSpan ForecastHorizon = TimeSpan.FromHours(3);

    private const int MaxUpcoming = 256;

    /// <summary>Moves a world on to its next rolled phase.</summary>
    private void Advance(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile)
    {
        if (state.Upcoming.Count == 0)
            Fill(state, profile);

        if (state.Upcoming.Count == 0)
        {
            Enter(state, WGStormPhase.None, null, _random.NextFloat(profile.ClearMinSeconds, profile.ClearMaxSeconds));
            return;
        }

        var next = state.Upcoming[0];
        state.Upcoming.RemoveAt(0);
        Enter(state, next.Phase, next.Weather, next.Seconds);
        Fill(state, profile);
    }

    /// <summary>Rolls phases onto the queue until it reaches past the forecast horizon.</summary>
    private void Fill(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile)
    {
        // With the cycle off nothing is coming: an admin's weather holds, then the world stays clear.
        if (!_cfg.GetCVar(PlanetCVars.PlanetWeather))
            return;

        var end = state.NextChange;
        foreach (var phase in state.Upcoming)
        {
            end += TimeSpan.FromSeconds(phase.Seconds);
        }

        var horizon = _timing.CurTime + ForecastHorizon;
        while (end < horizon && state.Upcoming.Count < MaxUpcoming)
        {
            var clear = state.Upcoming.Count > 0
                ? state.Upcoming[^1].Phase == WGStormPhase.None
                : state.Phase == WGStormPhase.None && state.Current == null;
            var season = GetSeason(state, profile, end);
            var count = state.Upcoming.Count;

            if (!clear || !RollWeather(profile, season, state.Upcoming))
            {
                var seconds = _random.NextFloat(profile.ClearMinSeconds, profile.ClearMaxSeconds) * (season?.ClearScale ?? 1f);
                state.Upcoming.Add(new WGWeatherPhase { Phase = WGStormPhase.None, Seconds = MathF.Max(1f, seconds) });
            }

            for (var i = count; i < state.Upcoming.Count; i++)
            {
                end += TimeSpan.FromSeconds(state.Upcoming[i].Seconds);
            }
        }
    }

    /// <summary>Drops what was rolled and rolls again; a storm under way keeps its remaining phases.</summary>
    public void Reroll(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile)
    {
        var keep = 0;
        if (state.Phase is WGStormPhase.Telegraph or WGStormPhase.Main)
        {
            while (keep < state.Upcoming.Count && state.Upcoming[keep].Phase is WGStormPhase.Main or WGStormPhase.End)
            {
                keep++;
            }
        }

        state.Upcoming.RemoveRange(keep, state.Upcoming.Count - keep);
        Fill(state, profile);
    }

    /// <summary>Rolls the next storm of the season, or a single weather; false when the world has none.</summary>
    private bool RollWeather(WGPlanetWeatherPrototype profile, WGPlanetSeason? season, List<WGWeatherPhase> into)
    {
        var storms = season?.Storms ?? profile.Storms;
        var scale = season?.WeatherScale ?? 1f;
        var main = MathF.Max(1f, _random.NextFloat(profile.WeatherMinSeconds, profile.WeatherMaxSeconds) * scale);

        if (storms.Count == 0)
        {
            if (profile.Weather.Count == 0)
                return false;
            into.Add(new WGWeatherPhase { Weather = _random.Pick(profile.Weather), Phase = WGStormPhase.Main, Seconds = main });
            return true;
        }

        var total = 0f;
        foreach (var candidate in storms)
        {
            total += StormWeight(season, candidate);
        }

        var storm = storms[0];
        if (total <= 0f)
        {
            storm = _random.Pick(storms);
        }
        else
        {
            var roll = _random.NextFloat(total);
            foreach (var candidate in storms)
            {
                storm = candidate;
                roll -= StormWeight(season, candidate);
                if (roll < 0f)
                    break;
            }
        }

        if (storm.Telegraph is { } telegraph)
            into.Add(new WGWeatherPhase { Weather = telegraph, Phase = WGStormPhase.Telegraph, Seconds = storm.TelegraphSeconds });
        into.Add(new WGWeatherPhase { Weather = storm.Main, Phase = WGStormPhase.Main, Seconds = main });
        if (storm.End is { } end)
            into.Add(new WGWeatherPhase { Weather = end, Phase = WGStormPhase.End, Seconds = storm.EndSeconds });
        return true;
    }

    private static float StormWeight(WGPlanetSeason? season, WGPlanetStorm storm)
    {
        return season != null && season.StormWeights.TryGetValue(storm.Main, out var weight) ? MathF.Max(0f, weight) : 1f;
    }

    private void Enter(WGPlanetWeatherComponent state, WGStormPhase phase, ProtoId<WeatherPrototype>? weather, float seconds)
    {
        state.Phase = phase;
        state.Current = weather;
        state.NextChange = _timing.CurTime + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// A world's weather from now to the forecast horizon: the current phase first, then each rolled one with its start
    /// and end. Empty while the cycle is off and nothing is held.
    /// </summary>
    public bool TryGetSchedule(EntityUid world, List<WGScheduledWeather> into)
    {
        into.Clear();
        if (!TryComp<WGPlanetWeatherComponent>(world, out var state))
            return false;

        var now = _timing.CurTime;
        into.Add(new WGScheduledWeather(now, state.NextChange, state.Current, state.Phase));
        var start = state.NextChange;
        foreach (var phase in state.Upcoming)
        {
            var end = start + TimeSpan.FromSeconds(phase.Seconds);
            into.Add(new WGScheduledWeather(start, end, phase.Weather, phase.Phase));
            start = end;
        }

        return true;
    }

    /// <summary>The weather the schedule has falling at a time; the current weather for any time already past.</summary>
    public ProtoId<WeatherPrototype>? GetWeatherAt(WGPlanetWeatherComponent state, TimeSpan at)
    {
        if (at < state.NextChange)
            return state.Current;

        var start = state.NextChange;
        foreach (var phase in state.Upcoming)
        {
            start += TimeSpan.FromSeconds(phase.Seconds);
            if (at < start)
                return phase.Weather;
        }

        return null;
    }
}

/// <summary>One stretch of a world's scheduled weather.</summary>
public readonly record struct WGScheduledWeather(TimeSpan Start, TimeSpan End, ProtoId<WeatherPrototype>? Weather, WGStormPhase Phase);
