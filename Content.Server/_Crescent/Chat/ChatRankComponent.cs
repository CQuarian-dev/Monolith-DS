using Content.Shared._NF.Cloning; // LuaM

namespace Content.Server.Crescent.Chat;

/// <summary>
/// Stores "rank" of the user for certain contexts.
/// Mostly given by role.
/// </summary>

[RegisterComponent]
public sealed partial class ChatRankComponent : Component, ITransferredByCloning // LuaM
{
    [DataField]
    public LocId Rank;
}
