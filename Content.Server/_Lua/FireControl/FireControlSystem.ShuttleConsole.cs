using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.Ships.Components;
using Content.Shared.Database;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.FireControl;

/// <summary>
/// Lets a shuttle console act as a gunnery console for its own grid.
/// </summary>
public sealed partial class FireControlSystem
{
    private static readonly TimeSpan ShuttleConsoleLogSpacing = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShuttleConsoleUiSpacing = TimeSpan.FromSeconds(0.5);
    private const float ShuttleConsoleLogLookupRange = 1024f;

    private readonly Dictionary<EntityUid, TimeSpan> _shuttleConsoleNextLog = new();
    private readonly Dictionary<EntityUid, TimeSpan> _shuttleConsoleNextUi = new();
    private readonly HashSet<EntityUid> _pendingShuttleConsoles = new();

    /// <summary>
    /// Weapon -> shuttle console that fired it last, so its guided missiles follow that console's cursor.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _shuttleConsoleWeapons = new();

    /// <summary>
    /// Gets the fire control server a shuttle console can use.
    /// Crewed shuttles keep a dedicated gunner, so their shuttle consoles get no weapons.
    /// </summary>
    public bool TryGetShuttleConsoleServer(EntityUid console,
        [NotNullWhen(true)] out EntityUid? serverUid,
        [NotNullWhen(true)] out FireControlServerComponent? server)
    {
        serverUid = null;
        server = null;

        var grid = Transform(console).GridUid;
        if (grid == null || HasComp<CrewedShuttleComponent>(grid))
            return false;

        var (foundUid, foundServer) = TryGetGridServer(console);
        if (foundUid == null || foundServer == null)
            return false;

        serverUid = foundUid;
        server = foundServer;
        return true;
    }

    /// <summary>
    /// Weapons shown in a shuttle console, or null if it has no fire control.
    /// </summary>
    public FireControllableEntry[]? GetShuttleConsoleWeapons(EntityUid console)
    {
        return TryGetShuttleConsoleServer(console, out _, out var server)
            ? BuildControllableEntries(server)
            : null;
    }

    /// <summary>
    /// Fires the selected weapons. An empty selection only moves the cursor that guided missiles follow.
    /// </summary>
    public void FireFromShuttleConsole(EntityUid console, EntityUid? actor, List<NetEntity> selected, NetCoordinates coordinates)
    {
        if (!TryGetShuttleConsoleServer(console, out var serverUid, out var server))
            return;

        var cursor = GetCoordinates(coordinates);
        if (selected.Count == 0)
        {
            OnGuidanceUpdate(console, cursor);
            return;
        }

        var xform = Transform(console);
        if (xform.GridUid is not { } grid)
            return;

        _consoleMousePositions[console] = cursor;
        LuaMWarnGroundFire(grid, actor); // LuaM
        RememberShuttleConsoleWeapons(console, selected, server);
        FireWeapons(serverUid.Value, selected, coordinates, server);
        LogShuttleConsoleFire(console, actor, grid, xform.MapID, coordinates);
        QueueShuttleConsoleUpdate(console);
    }

    /// <summary>
    /// Gets the shuttle console that should guide missiles shot by this weapon.
    /// </summary>
    private bool TryGetShuttleConsoleGuidance(EntityUid weapon, EntityCoordinates target, out EntityUid console)
    {
        if (!_shuttleConsoleWeapons.TryGetValue(weapon, out console) || TerminatingOrDeleted(console))
            return false;

        _consoleMousePositions.TryAdd(console, target);
        return true;
    }

    /// <summary>
    /// Weapons fired from a gunnery console stop following the shuttle console.
    /// </summary>
    private void ForgetShuttleConsoleWeapons(List<NetEntity> weapons)
    {
        foreach (var weapon in weapons)
        {
            if (TryGetEntity(weapon, out var uid))
                _shuttleConsoleWeapons.Remove(uid.Value);
        }
    }

    private void RememberShuttleConsoleWeapons(EntityUid console, List<NetEntity> weapons, FireControlServerComponent server)
    {
        foreach (var (weapon, owner) in _shuttleConsoleWeapons)
        {
            if (TerminatingOrDeleted(weapon) || TerminatingOrDeleted(owner))
                _shuttleConsoleWeapons.Remove(weapon);
        }

        foreach (var weapon in weapons)
        {
            if (TryGetEntity(weapon, out var uid) && server.Controlled.Contains(uid.Value))
                _shuttleConsoleWeapons[uid.Value] = console;
        }
    }

    public void RefreshShuttleConsoleWeapons(EntityUid console)
    {
        var grid = Transform(console).GridUid;
        if (grid == null || !TryComp<FireControlGridComponent>(grid, out var fcGrid))
            return;

        RefreshControllables(grid.Value, fcGrid);
    }

    private void LogShuttleConsoleFire(EntityUid console, EntityUid? actor, EntityUid grid, MapId mapId, NetCoordinates coordinates)
    {
        var now = _timing.CurTime;
        if (_shuttleConsoleNextLog.TryGetValue(console, out var next) && next > now)
            return;

        _shuttleConsoleNextLog[console] = now + ShuttleConsoleLogSpacing;

        var firePos = _transform.ToMapCoordinates(GetCoordinates(coordinates)).Position;
        var ourPos = _transform.GetWorldPosition(grid);
        var grids = new List<Entity<MapGridComponent>>();
        var adjust = new Vector2(ShuttleConsoleLogLookupRange, ShuttleConsoleLogLookupRange);
        _mapMan.FindGridsIntersecting(mapId, new Box2(firePos - adjust, firePos + adjust), ref grids, approx: true, includeMap: false);

        EntityUid? closest = null;
        foreach (var gridUid in grids.Where(g => g.Owner != grid))
        {
            var newPos = _transform.GetWorldPosition(gridUid);
            if (closest == null || (newPos - firePos).LengthSquared() < (_transform.GetWorldPosition(closest.Value) - firePos).LengthSquared())
                closest = gridUid;
        }

        _adminLogger.Add(LogType.ShipgunFired, LogImpact.High,
            $"{ToPrettyString(actor):user} fired weaponry of ship {ToPrettyString(grid):entity} from a shuttle console from ({ourPos}) to ({firePos}), closest grid: {ToPrettyString(closest)}");
    }

    private void QueueShuttleConsoleUpdate(EntityUid console)
    {
        var now = _timing.CurTime;
        if (!_shuttleConsoleNextUi.TryGetValue(console, out var next) || next <= now)
        {
            _shuttleConsoleNextUi[console] = now + ShuttleConsoleUiSpacing;
            _shuttleConsoleSystem.UpdateConsoleUi(console);
            return;
        }

        _pendingShuttleConsoles.Add(console);
    }

    private void UpdatePendingShuttleConsoles()
    {
        if (_pendingShuttleConsoles.Count == 0)
            return;

        var now = _timing.CurTime;
        _pendingShuttleConsoles.RemoveWhere(console =>
        {
            if (TerminatingOrDeleted(console))
                return true;

            if (_shuttleConsoleNextUi.TryGetValue(console, out var next) && next > now)
                return false;

            _shuttleConsoleNextUi[console] = now + ShuttleConsoleUiSpacing;
            _shuttleConsoleSystem.UpdateConsoleUi(console);
            return true;
        });
    }
}
