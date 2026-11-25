using Robust.Shared.Configuration;

namespace Content.Shared._WG.CCVar;

/// <summary>
/// Settings for Wolfgate caverns.
/// </summary>
[CVarDefs]
public sealed class CavernCVars
{
    /// <summary>
    /// Whether planet networks built from now on get a cavern below their ground. On by default, so caverns come with
    /// planets (wg.planet_networks); set it false to build worlds without them.
    /// </summary>
    public static readonly CVarDef<bool> Caverns =
        CVarDef.Create("wg.caverns", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether cavern mouths are claimed ahead of players as they explore; off leaves only the gate and admin mouths.
    /// </summary>
    public static readonly CVarDef<bool> CavernClaims =
        CVarDef.Create("wg.cavern_claims", true, CVar.SERVERONLY);
}
