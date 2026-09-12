using Content.Server._NF.Bank;
using Content.Server.Administration.Logs;
using Content.Shared._LuaM.Yupi;
using Content.Shared._Mono.CCVar;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Yupi;

public enum YupiTransferResult : byte
{
    Success,
    InvalidAmount,
    InvalidTarget,
    SelfTransfer,
    NoAccount,
    TargetUnavailable,
    InsufficientFunds,
    Cooldown,
    Disabled,
}

/// <summary>
/// Player-to-player bank transfers addressed by per-round YUPI codes.
/// </summary>
public sealed partial class YupiSystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Amounts sent per account, oldest first, used for the sliding commission window.
    /// </summary>
    private readonly Dictionary<NetUserId, Queue<(TimeSpan Time, int Amount)>> _sent = new();

    private readonly Dictionary<NetUserId, TimeSpan> _lastTransfer = new();

    /// <summary>
    /// Returns the YUPI code of a mob, generating one if needed.
    /// Empty if the mob has no usable bank account, which includes Ironman characters living on cash only.
    /// </summary>
    public string GetOrCreateCode(EntityUid mob)
    {
        if (!HasComp<BankAccountComponent>(mob) || HasComp<IronmanComponent>(mob))
            return string.Empty;

        var account = EnsureComp<YupiAccountComponent>(mob);
        if (account.Code == string.Empty)
            account.Code = GenerateUniqueCode();

        return account.Code;
    }

    public bool TryFindByCode(string code, out EntityUid mob)
    {
        var query = EntityQueryEnumerator<YupiAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (account.Code != code)
                continue;

            mob = uid;
            return true;
        }

        mob = default;
        return false;
    }

    /// <summary>
    /// Game time from which the account may transfer again. Zero if there is no cooldown.
    /// </summary>
    public TimeSpan GetNextTransferTime(NetUserId user)
    {
        return _lastTransfer.TryGetValue(user, out var last) ? last + YupiRules.Cooldown : TimeSpan.Zero;
    }

    /// <summary>
    /// How much the account can still send at the low commission rate right now.
    /// </summary>
    public int GetLowRateRemaining(NetUserId user)
    {
        if (!_sent.TryGetValue(user, out var sent))
            return YupiRules.LowRateLimit;

        var cutoff = _timing.CurTime - YupiRules.LowRateWindow;
        while (sent.Count > 0 && sent.Peek().Time <= cutoff)
            sent.Dequeue();

        long total = 0;
        foreach (var (_, amount) in sent)
            total += amount;

        if (sent.Count == 0)
            _sent.Remove(user);

        return (int)Math.Max(0, YupiRules.LowRateLimit - total);
    }

    /// <summary>
    /// Charges <paramref name="sender"/> exactly <paramref name="amount"/> and sends it, minus the commission,
    /// to the owner of <paramref name="targetCode"/>. The receiver's deposit is taxed like any bank deposit.
    /// </summary>
    /// <param name="target">The receiving mob, when found.</param>
    /// <param name="commission">What was taken out of the amount.</param>
    /// <param name="received">What actually landed on the receiver's account after the deposit tax.</param>
    public YupiTransferResult TryTransfer(EntityUid sender,
        string targetCode,
        int amount,
        out EntityUid target,
        out int commission,
        out int received)
    {
        target = default;
        commission = 0;
        received = 0;

        if (amount <= 0)
            return YupiTransferResult.InvalidAmount;

        if (!_cfg.GetCVar(MonoCVars.DepositEnabled))
            return YupiTransferResult.Disabled;

        if (!_player.TryGetSessionByEntity(sender, out var senderSession))
            return YupiTransferResult.InsufficientFunds;

        if (HasComp<IronmanComponent>(sender))
            return YupiTransferResult.NoAccount;

        var user = senderSession.UserId;
        var now = _timing.CurTime;
        if (now < GetNextTransferTime(user))
            return YupiTransferResult.Cooldown;

        targetCode = YupiRules.NormalizeCode(targetCode);
        if (!YupiRules.IsValidCode(targetCode) || !TryFindByCode(targetCode, out target))
            return YupiTransferResult.InvalidTarget;

        if (target == sender)
            return YupiTransferResult.SelfTransfer;

        // Ironman characters never get a code, this only guards against the trait being added afterwards.
        if (HasComp<IronmanComponent>(target))
            return YupiTransferResult.InvalidTarget;

        // Deposits need an attached player with loaded preferences.
        if (!TryComp<BankAccountComponent>(target, out var targetBank) || !_bank.TryGetBalance(target, out _))
            return YupiTransferResult.TargetUnavailable;

        commission = (int) YupiRules.GetCommission(amount, GetLowRateRemaining(user));
        var net = amount - commission;
        if (net <= 0)
            return YupiTransferResult.InvalidAmount;

        if (!_bank.TryGetBalance(sender, out var balance) || balance < amount)
            return YupiTransferResult.InsufficientFunds;

        // Same calculation TryBankDeposit applies, done beforehand so we can report it.
        _bank.GetTaxedDepositAmount(net, targetBank.Balance, out received, out _);

        if (!_bank.TryBankWithdraw(sender, amount))
            return YupiTransferResult.InsufficientFunds;

        if (!_bank.TryBankDeposit(target, net))
        {
            // Refunding the sender's own money, so no tax.
            _bank.TryBankDeposit(sender, amount, tax: false);
            Log.Error($"YUPI transfer of {amount} from {ToPrettyString(sender)} to {ToPrettyString(target)} failed on deposit, refunded");
            return YupiTransferResult.TargetUnavailable;
        }

        if (!_sent.TryGetValue(user, out var sent))
            _sent[user] = sent = new Queue<(TimeSpan, int)>();
        sent.Enqueue((now, amount));
        _lastTransfer[user] = now;

        _adminLogger.Add(LogType.ATMUsage,
            LogImpact.Medium,
            $"{ToPrettyString(sender):actor} sent {amount} via YUPI to {ToPrettyString(target):subject} (commission {commission}, received {net}, of which {net - received} went to savings)");

        return YupiTransferResult.Success;
    }

    private string GenerateUniqueCode()
    {
        Span<char> buffer = stackalloc char[YupiRules.CodeLength];
        string code;
        do
        {
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = YupiRules.CodeAlphabet[_random.Next(YupiRules.CodeAlphabet.Length)];

            code = new string(buffer);
        }
        while (TryFindByCode(code, out _));

        return code;
    }
}
