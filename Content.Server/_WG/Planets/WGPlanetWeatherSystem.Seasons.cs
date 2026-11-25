using Content.Shared._WG.Planets;

namespace Content.Server._WG.Planets;

/// <summary>Seasons: each world loops through its profile's list from <see cref="WGPlanetWeatherComponent.SeasonEpoch"/>.</summary>
public sealed partial class WGPlanetWeatherSystem
{
    /// <summary>The season at a time, or null for a world without seasons.</summary>
    public WGPlanetSeason? GetSeason(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile, TimeSpan at)
    {
        return GetSeasonIndex(state, profile, at, out _) is { } index ? profile.Seasons[index] : null;
    }

    /// <summary>The index of the season at a time and the seconds left in it.</summary>
    public int? GetSeasonIndex(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile, TimeSpan at, out float secondsLeft)
    {
        secondsLeft = float.PositiveInfinity;
        if (profile.Seasons.Count == 0)
            return null;
        if (profile.Seasons.Count == 1)
            return 0;

        var total = 0.0;
        foreach (var season in profile.Seasons)
        {
            total += SeasonLength(season);
        }

        var elapsed = ((at - state.SeasonEpoch).TotalSeconds % total + total) % total;
        for (var i = 0; i < profile.Seasons.Count; i++)
        {
            var length = SeasonLength(profile.Seasons[i]);
            if (elapsed < length)
            {
                secondsLeft = (float) (length - elapsed);
                return i;
            }

            elapsed -= length;
        }

        secondsLeft = (float) SeasonLength(profile.Seasons[^1]);
        return profile.Seasons.Count - 1;
    }

    /// <summary>A world's season now, or at a given time; false for anything that isn't a world.</summary>
    public bool TryGetSeason(EntityUid world, out WGPlanetSeason? season, TimeSpan? at = null)
    {
        season = null;
        if (!TryComp<WGPlanetWeatherComponent>(world, out var state) || !_proto.TryIndex(state.Profile, out var profile))
            return false;

        season = GetSeason(state, profile, at ?? _timing.CurTime);
        return true;
    }

    /// <summary>Starts a season now; the weather already rolled for later is rolled again for it.</summary>
    public bool SetSeason(EntityUid world, string id)
    {
        if (!TryComp<WGPlanetWeatherComponent>(world, out var state) || !_proto.TryIndex(state.Profile, out var profile))
            return false;

        var index = profile.Seasons.FindIndex(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        StartSeason(state, profile, index);
        Reroll(state, profile);
        _nextUpdate = TimeSpan.Zero;
        return true;
    }

    /// <summary>Sets the season epoch so the given season begins now.</summary>
    private void StartSeason(WGPlanetWeatherComponent state, WGPlanetWeatherPrototype profile, int index)
    {
        var offset = 0.0;
        for (var i = 0; i < index; i++)
        {
            offset += SeasonLength(profile.Seasons[i]);
        }

        state.SeasonEpoch = _timing.CurTime - TimeSpan.FromSeconds(offset);
    }

    private static double SeasonLength(WGPlanetSeason season)
    {
        return Math.Max(1.0, season.Length);
    }
}
