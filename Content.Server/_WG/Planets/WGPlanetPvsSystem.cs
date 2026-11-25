using Content.Shared._WG.CCVar;
using Robust.Server.GameStates;
using Robust.Shared.Configuration;

namespace Content.Server._WG.Planets;

/// <summary>
/// Keeps planet layers out of the engine's global grid and map force-send, so a client only receives a layer's map
/// and terrain while a viewer is on or near it instead of every planet tile on every join and full-state request.
/// The orbit layer stays force-sent: it carries the radar recipe, the FTL target and the parked hulls.
/// </summary>
/// <remarks>
/// Needs the Lupine engine's <see cref="PvsOverrideSystem.SetRangeLimited"/>. A range-limited layer is deleted on a
/// client when it leaves view and arrives as new when a viewer returns; client code must resolve layer references
/// with <c>TryGetEntity</c>, as the orbit layer's radar fallback already does.
/// </remarks>
public sealed class WGPlanetPvsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGPlanetNetworkBuiltEvent>(OnNetworkBuilt);
        Subs.CVar(_cfg, PlanetCVars.PvsRangeLimited, OnEnabledChanged, true);
    }

    private void OnNetworkBuilt(ref WGPlanetNetworkBuiltEvent args)
    {
        if (!_enabled || !TryComp<WGPlanetNetworkComponent>(args.Network, out var network))
            return;

        Apply(network, true);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        var query = EntityQueryEnumerator<WGPlanetNetworkComponent>();
        while (query.MoveNext(out _, out var network))
        {
            Apply(network, enabled);
        }
    }

    /// <summary>Range-limits, or restores, every layer of a network but its orbit.</summary>
    private void Apply(WGPlanetNetworkComponent network, bool limited)
    {
        foreach (var layer in network.Layers)
        {
            if (layer != network.OrbitMap && Exists(layer))
                _pvs.SetRangeLimited(layer, limited);
        }

        foreach (var layer in network.LowerLayers)
        {
            if (Exists(layer))
                _pvs.SetRangeLimited(layer, limited);
        }
    }
}
