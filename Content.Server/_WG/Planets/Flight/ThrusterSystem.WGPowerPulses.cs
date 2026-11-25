using Content.Server._WG.Planets.Flight;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Power.Pow3r;
using Content.Shared._WG.Planets.Flight;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ThrusterSystem
{
    public static readonly TimeSpan WgOverloadRest = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan WgOverloadBurst = TimeSpan.FromSeconds(1);

    private bool WgCooling(EntityUid uid) =>
        TryComp<WGAtmosphereThrusterComponent>(uid, out var state) && state.Cooling;

    /// <summary>Snapshot whole electrical networks before changing any loads, including docked engines.</summary>
    private void WgUpdatePowerPulses()
    {
        var groups = new Dictionary<PowerState.Network, List<(EntityUid Uid, ThrusterComponent Engine, ApcPowerReceiverComponent Receiver)>>();
        var query = EntityQueryEnumerator<ThrusterComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var engine, out var receiver))
        {
            if (engine.Type != ThrusterType.Linear)
                continue;
            var xform = Transform(uid);
            if (engine.Enabled && xform.Anchored && xform.GridUid is { } grid && WgInAtmosphere(grid) && !WgRestingWreck(grid)
                && receiver.NeedsPower && !receiver.PowerDisabled && receiver.Provider?.Net is { } net)
            {
                if (!groups.TryGetValue(net.NetworkNode, out var group))
                    groups[net.NetworkNode] = group = new();
                group.Add((uid, engine, receiver));
            }
            else if (TryComp<WGAtmosphereThrusterComponent>(uid, out var old))
                WgSetPowerPulse(old, false);
        }
        foreach (var (net, engines) in groups)
        {
            var validNetwork = WgTryGetPowerStatistics(net, out var stats);
            var requested = stats.Consumption;
            foreach (var (uid, engine, receiver) in engines)
            {
                var load = TryComp<WGAtmosphereThrusterComponent>(uid, out var state) ? state.RatedLoad : engine.OriginalLoad;
                requested += load * (HasComp<WGLandingThrusterComponent>(uid) ? 1f : WgAtmospherePowerMultiplier) - receiver.Load;
            }
            var overloaded = !validNetwork || requested > stats.SupplyCurrent + 1f;
            foreach (var (uid, engine, _) in engines)
            {
                if (!TryComp<WGAtmosphereThrusterComponent>(uid, out var state))
                {
                    state = AddComp<WGAtmosphereThrusterComponent>(uid);
                    state.RatedThrust = engine.Thrust;
                    state.RatedLoad = engine.OriginalLoad;
                    state.WasPowered = engine.IsOn;
                }
                WgSetPowerPulse(state, overloaded && !HasComp<WGLandingThrusterComponent>(uid));
            }
        }
    }

    // Destroyed nodes linger until the next reconnect pass, so a missing network means no forecast.
    private bool WgTryGetPowerStatistics(PowerState.Network network, out NetworkPowerStatistics statistics)
    {
        try
        {
            statistics = _wgPowerNet.GetNetworkStatistics(network);
            return true;
        }
        catch (KeyNotFoundException)
        {
            statistics = default;
            return false;
        }
    }

    /// <summary>Cycles an overloaded engine between resting and bursting; clears it once power is sufficient.</summary>
    public void WgSetPowerPulse(WGAtmosphereThrusterComponent state, bool overloaded)
    {
        if (!overloaded)
        {
            state.PowerLimited = false;
            state.Cooling = false;
            state.PulseAt = TimeSpan.Zero;
            return;
        }
        if (!state.PowerLimited)
        {
            state.PowerLimited = true;
            state.Cooling = true;
            state.PulseAt = _timing.CurTime + WgOverloadRest;
        }
        else if (_timing.CurTime >= state.PulseAt)
        {
            state.Cooling = !state.Cooling;
            state.PulseAt = _timing.CurTime + (state.Cooling ? WgOverloadRest : WgOverloadBurst);
        }
    }
}
