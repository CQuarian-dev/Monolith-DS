using Content.Server._WG.Planets.Bounds;
using Content.Shared._WG.Planets;
using Robust.Shared.Timing;

namespace Content.Server._WG.Planets;

/// <summary>
/// Sends every client how far the forming worlds have got, about once a second while any are forming and once
/// empty when they are done, for the lobby's world-forming bar and the in-round one.
/// </summary>
public sealed partial class WGWorldFormingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WGPlanetNetworkSystem _networks = default!;
    [Dependency] private WGPlanetPreloadSystem _preload = default!;
    [Dependency] private WGPlanetRoundHoldSystem _hold = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.25);

    private TimeSpan _next;

    /// <summary>The latest state sent.</summary>
    public WGWorldFormingEvent Last { get; private set; } = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;
        var state = Measure();

        // A changed list or stage goes at once, bare progress once a second, and nothing once all is done
        // (WGWorldFormingSystem.NetTraffic.cs).
        WgSendState(state);
    }

    /// <summary>The forming worlds as they stand.</summary>
    public WGWorldFormingEvent Measure()
    {
        var state = new WGWorldFormingEvent { RoundHeld = _hold.Holding };
        var roundStart = new HashSet<EntityUid>(_networks.PrebuiltGrounds());
        var total = 0f;

        foreach (var (ground, stage, progress) in _preload.Forming())
        {
            if (TerminatingOrDeleted(ground))
                continue;

            var entry = new WGWorldFormingEntry
            {
                Ground = GetNetEntity(ground),
                Name = WorldName(ground),
                Stage = stage,
                Progress = progress,
                RoundStart = roundStart.Contains(ground),
            };

            if (TryComp<WGPlanetLayerComponent>(ground, out var layer)
                && TryGetEntity(layer.Network, out var network)
                && TryComp<WGPlanetNetworkComponent>(network, out var comp))
            {
                foreach (var map in comp.Layers)
                {
                    entry.Maps.Add(GetNetEntity(map));
                }

                if (!comp.Layers.Contains(comp.OrbitMap))
                    entry.Maps.Add(GetNetEntity(comp.OrbitMap));
            }
            else
            {
                entry.Maps.Add(entry.Ground);
            }

            state.Worlds.Add(entry);
            total += progress;
        }

        state.Overall = state.Worlds.Count == 0 ? 1f : total / state.Worlds.Count;
        return state;
    }

    /// <summary>The world's own name, as the weather report gives it; the ground's map name otherwise.</summary>
    private string WorldName(EntityUid ground)
    {
        if (TryComp<WGPlanetLayerComponent>(ground, out var layer)
            && TryGetEntity(layer.Network, out var network)
            && TryComp<WGPlanetWeatherComponent>(network, out var weather)
            && !string.IsNullOrEmpty(weather.PlanetName))
            return weather.PlanetName;

        return MetaData(ground).EntityName;
    }
}
