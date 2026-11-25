using Content.Shared._NF.Cloning; // LuaM
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgerySpeedModifierComponent : Component, ITransferredByCloning // LuaM
{
    [DataField]
    public float SpeedModifier = 1.5f;
}