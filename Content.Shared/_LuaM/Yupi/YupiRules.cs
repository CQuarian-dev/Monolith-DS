using Content.Shared._NF.Bank;

namespace Content.Shared._LuaM.Yupi;

/// <summary>
/// All YUPI tuning numbers and the commission formula, shared so the PDA can preview the commission.
/// </summary>
public static class YupiRules
{
    /// <summary>
    /// Amount sent within <see cref="LowRateWindow"/> that is charged at <see cref="LowRatePercent"/>.
    /// Anything above it is charged at <see cref="HighRatePercent"/>.
    /// </summary>
    public const int LowRateLimit = 250_000;

    public const int LowRatePercent = 3;

    public const int HighRatePercent = 10;

    /// <summary>
    /// Sliding window over which sent amounts are summed for the commission.
    /// </summary>
    public static readonly TimeSpan LowRateWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Minimum time between two successful transfers from the same account.
    /// </summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    public const int CodeLength = 6;

    /// <summary>
    /// Letters without I and O, digits without 0, so codes cannot be misread.
    /// </summary>
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";

    /// <summary>
    /// Commission taken out of <paramref name="amount"/> when <paramref name="lowRateRemaining"/>
    /// of the low-rate allowance is still unused in the current window.
    /// The sender is charged exactly the amount, the receiver gets the amount minus this.
    /// </summary>
    public static long GetCommission(int amount, int lowRateRemaining)
    {
        if (amount <= 0)
            return 0;

        long lowPart = Math.Clamp(lowRateRemaining, 0, amount);
        long highPart = amount - lowPart;
        return CeilPercent(lowPart, LowRatePercent) + CeilPercent(highPart, HighRatePercent);
    }

    /// <summary>
    /// Formats credits with dots between thousands, e.g. 10.000.000.
    /// </summary>
    public static string FormatCredits(long amount)
    {
        return BankSystemExtensions.ToCurrencyString(amount,
            symbolOverride: string.Empty,
            separatorOverride: ".",
            symbolLocation: BankSystemExtensions.CurrencySymbolLocation.Prefix);
    }

    public static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    public static bool IsValidCode(string code)
    {
        if (code.Length != CodeLength)
            return false;

        foreach (var ch in code)
        {
            if (!CodeAlphabet.Contains(ch))
                return false;
        }

        return true;
    }

    private static long CeilPercent(long value, int percent)
    {
        return (value * percent + 99) / 100;
    }
}
