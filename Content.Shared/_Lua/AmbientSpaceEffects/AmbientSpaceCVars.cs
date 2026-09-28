using Robust.Shared.Configuration;

namespace Content.Shared._Lua.AmbientSpaceEffects;

[CVarDefs]
public sealed class AmbientSpaceCVars
{
    public static readonly CVarDef<bool> AmbientSpaceEffectsEnabled =
        CVarDef.Create("lua.ambient_space.enabled", true, CVar.ARCHIVE | CVar.SERVERONLY);

    public static readonly CVarDef<int> AmbientSpaceEffectsQuality =
        CVarDef.Create("lua.ambient_space.quality", 2, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AmbientSpaceLayerLower =
        CVarDef.Create("lua.ambient_space.layer_lower", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AmbientSpaceLayerMid =
        CVarDef.Create("lua.ambient_space.layer_mid", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AmbientSpaceLayerUpper =
        CVarDef.Create("lua.ambient_space.layer_upper", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> AmbientSpaceEffectsDensity =
        CVarDef.Create("lua.ambient_space.density", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AmbientSpaceBake =
        CVarDef.Create("lua.ambient_space.bake", true, CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AmbientSpaceTint =
        CVarDef.Create("lua.ambient_space.tint", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
