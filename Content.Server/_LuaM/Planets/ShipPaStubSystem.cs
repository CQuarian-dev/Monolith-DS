using Content.Server.Chat.Managers;
using Content.Server.Shuttles.Components;
using Content.Shared._WG.ShipPa;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

// -- LuaM: заглушка вместо модуля ShipPa Wolfgate. Объявления планет уходят пилотам: popup и строка в чат.
namespace Content.Server._WG.ShipPa;

public sealed partial class ShipPaSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _pilots = new();

    public bool Announce(EntityUid grid, string message, SoundSpecifier? sound = null, string? sender = null, Color? color = null)
    {
        if (!Exists(grid))
            return false;

        _pilots.Clear();
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out _, out var console, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            foreach (var pilot in console.SubscribedPilots)
            {
                _pilots.Add(pilot);
            }
        }

        foreach (var pilot in _pilots)
        {
            _popup.PopupEntity(message, pilot, pilot, PopupType.LargeCaution);

            if (_players.TryGetSessionByEntity(pilot, out var session))
                _chat.DispatchServerMessage(session, message);
        }

        return false;
    }

    public string GetShipName(EntityUid grid)
    {
        return TryComp(grid, out MetaDataComponent? meta) && !string.IsNullOrWhiteSpace(meta.EntityName)
            ? meta.EntityName
            : Loc.GetString("wg-ship-unknown");
    }
}

public sealed class ShipAlertSystem : EntitySystem
{
    public void SetCode(EntityUid grid, ProtoId<ShipAlertCodePrototype> code, bool announce = true)
    {
        EnsureComp<ShipAlertComponent>(grid).Code = code;
    }
}
