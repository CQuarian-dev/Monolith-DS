namespace Content.Server._LuaM.Yupi;

/// <summary>
/// Holds the YUPI code of a mob with a bank account. Added on demand and lives as long as the mob,
/// so a character gets a new code every round.
/// </summary>
[RegisterComponent, Access(typeof(YupiSystem))]
public sealed partial class YupiAccountComponent : Component
{
    [ViewVariables]
    public string Code = string.Empty;
}

/// <summary>
/// Marks the YUPI transfers PDA program.
/// </summary>
[RegisterComponent]
public sealed partial class YupiCartridgeComponent : Component
{
}
