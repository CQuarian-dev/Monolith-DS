namespace Content.Shared.Item;

/// <summary>
///     Raised on a *mob* when it tries to pickup something
/// </summary>
public sealed class PickupAttemptEvent : BasePickupAttemptEvent
{
    public PickupAttemptEvent(EntityUid user, EntityUid item, bool showPopup = false) : base(user, item, showPopup) { } // LuaM
}

/// <summary>
///     Raised directed at entity being picked up when someone tries to pick it up
/// </summary>
public sealed class GettingPickedUpAttemptEvent : BasePickupAttemptEvent
{
    public GettingPickedUpAttemptEvent(EntityUid user, EntityUid item, bool showPopup = false) : base(user, item, showPopup) { } // LuaM
}

[Virtual]
public class BasePickupAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntityUid Item;

    // LuaM-start
    /// <summary>
    /// Whether to show a popup telling the player why the attempt was cancelled.
    /// False when raised for verbs or other checks that do not perform the pickup.
    /// </summary>
    public bool ShowPopup;

    public BasePickupAttemptEvent(EntityUid user, EntityUid item, bool showPopup = false)
    {
        User = user;
        Item = item;
        ShowPopup = showPopup;
    }
    // LuaM-end
}
