using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

// -- LuaM: заглушка вместо модуля ShipPa Wolfgate, его не переносим. Пространство имён оставлено их, чтобы код планет собирался без правок.
namespace Content.Shared._WG.ShipPa;

[Prototype]
public sealed partial class ShipAlertCodePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)] public LocId Name;

    [DataField] public LocId? Description;

    [DataField(required: true)] public LocId Announcement;

    [DataField] public Color Color = Color.White;

    [DataField] public int Order;

    [DataField] public bool Selectable = true;

    [DataField] public SoundSpecifier? Sound;
}

[RegisterComponent]
public sealed partial class ShipAlertComponent : Component
{
    [ViewVariables] public ProtoId<ShipAlertCodePrototype>? Code;
}
