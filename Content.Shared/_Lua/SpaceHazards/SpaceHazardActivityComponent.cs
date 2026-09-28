namespace Content.Shared._Lua.SpaceHazards;

[RegisterComponent]
public sealed partial class SpaceHazardActivityComponent : Component
{
    [DataField]
    public float ActivationRange = 2500f;

    [DataField]
    public TimeSpan IdleTimeout = TimeSpan.FromSeconds(600);

    [DataField]
    public bool Active;

    [DataField]
    public TimeSpan? LastSeenPlayer;
}
