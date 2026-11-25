using Content.Shared._WG.ShipPa;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._WG.Planets;

/// <summary>A grid on an orbit layer with no station-keeping left, counting down to atmospheric entry.</summary>
[RegisterComponent, UnsavedComponent, AutoGenerateComponentPause]
public sealed partial class WGOrbitDecayComponent : Component
{
    /// <summary>Warning time between losing station-keeping and dropping out of orbit.</summary>
    [DataField]
    public TimeSpan Grace = TimeSpan.FromSeconds(60);

    /// <summary>When the hull drops out of orbit; set from <see cref="Grace"/> when the warning goes out.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DecayAt;

    /// <summary>Whether the PA warning has gone out and <see cref="DecayAt"/> is set.</summary>
    [DataField]
    public bool Announced;

    /// <summary>Situation code before the warning, restored if the ship recovers.</summary>
    [DataField]
    public ProtoId<ShipAlertCodePrototype>? PriorCode;
}
