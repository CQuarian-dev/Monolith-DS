using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Yupi;

[Serializable, NetSerializable]
public sealed class YupiUiState : BoundUserInterfaceState
{
    /// <summary>
    /// YUPI code of the PDA holder, empty if they have no bank account.
    /// </summary>
    public readonly string OwnCode;

    public readonly int Balance;

    /// <summary>
    /// How much can still be sent at the low commission rate in the current window.
    /// </summary>
    public readonly int LowRateRemaining;

    /// <summary>
    /// Server game time when the next transfer is allowed, so the client can count down on its own.
    /// </summary>
    public readonly TimeSpan NextTransfer;

    public YupiUiState(string ownCode, int balance, int lowRateRemaining, TimeSpan nextTransfer)
    {
        OwnCode = ownCode;
        Balance = balance;
        LowRateRemaining = lowRateRemaining;
        NextTransfer = nextTransfer;
    }
}

[Serializable, NetSerializable]
public sealed class YupiTransferMessage : CartridgeMessageEvent
{
    public readonly string TargetCode;
    public readonly int Amount;

    public YupiTransferMessage(string targetCode, int amount)
    {
        TargetCode = targetCode;
        Amount = amount;
    }
}
