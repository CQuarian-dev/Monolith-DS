using Content.Server.Power.Components;
using Content.Server.Wires;
using Content.Server.Power.EntitySystems;
using Content.Shared._WG.Planets.Flight;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server._WG.Planets.Flight;

/// <summary>Crash-damaged APCs flicker their output until repaired with a multitool.</summary>
public sealed partial class WGCrashApcFaultSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(PowerNetSystem));
        SubscribeLocalEvent<WGCrashApcFaultComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WGCrashApcFaultComponent, InteractUsingEvent>(OnInteract, before: new[] { typeof(WiresSystem) });
        SubscribeLocalEvent<WGCrashApcFaultComponent, WGApcRepairDoAfterEvent>(OnRepair);
        SubscribeLocalEvent<WGCrashApcFaultComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>Faults every APC on the grid that feeds a power-limited engine.</summary>
    public void DamageOverloadedApcs(EntityUid grid)
    {
        var circuits = new HashSet<Content.Server.Power.Pow3r.PowerState.Network>();
        var query = EntityQueryEnumerator<WGAtmosphereThrusterComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var engine, out var receiver))
            if (engine.PowerLimited && Transform(uid).GridUid == grid && receiver.Provider?.Net is { } network)
                circuits.Add(network.NetworkNode);

        // Receivers commonly attach to extension cables, not directly to the APC entity.
        var apcs = EntityQueryEnumerator<ApcComponent>();
        while (apcs.MoveNext(out var uid, out var apc))
        {
            if (Transform(uid).GridUid != grid || apc.Net is not { } network || !circuits.Contains(network.NetworkNode))
                continue;
            var fault = EnsureComp<WGCrashApcFaultComponent>(uid);
            fault.Interrupted = true;
            fault.NextFlicker = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(2f, 5f));
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<WGCrashApcFaultComponent, ApcComponent, PowerNetworkBatteryComponent>();
        while (query.MoveNext(out _, out var fault, out var apc, out var battery))
        {
            if (_timing.CurTime >= fault.NextFlicker)
            {
                fault.Interrupted = !fault.Interrupted;
                fault.NextFlicker = _timing.CurTime + TimeSpan.FromSeconds(fault.Interrupted ? _random.NextFloat(2f, 5f) : _random.NextFloat(1.2f, 3.2f));
            }
            // Preserve the user's main breaker and never manufacture supply or bypass a disabled battery.
            battery.CanDischarge = apc.MainBreakerEnabled && !fault.Interrupted;
        }
    }

    private void OnExamine(Entity<WGCrashApcFaultComponent> ent, ref ExaminedEvent args) =>
        args.PushText(Loc.GetString("wg-crash-apc-fault-examine"));

    private void OnInteract(Entity<WGCrashApcFaultComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tools.HasQuality(args.Used, "Pulsing"))
            return;
        args.Handled = _tools.UseTool(args.Used, args.User, ent.Owner, 3f, "Pulsing", new WGApcRepairDoAfterEvent());
    }

    private void OnRepair(Entity<WGCrashApcFaultComponent> ent, ref WGApcRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        RemComp<WGCrashApcFaultComponent>(ent.Owner);
        _popup.PopupEntity(Loc.GetString("wg-crash-apc-fault-repaired"), ent.Owner, args.User, PopupType.Medium);
    }

    private void OnShutdown(Entity<WGCrashApcFaultComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<ApcComponent>(ent.Owner, out var apc) && TryComp<PowerNetworkBatteryComponent>(ent.Owner, out var battery))
            battery.CanDischarge = apc.MainBreakerEnabled;
    }
}
