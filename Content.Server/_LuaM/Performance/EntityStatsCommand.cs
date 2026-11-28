/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Linq;
using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._LuaM.Performance;

[AdminCommand(AdminFlags.Debug)]
public sealed class EntityStatsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILogManager _log = default!;

    public string Command => "luam_entstats";
    public string Description => "Сколько сущностей по прототипам, картам и гридам; пишет и в серверный лог.";
    public string Help => "luam_entstats [top=30]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var top = 30;
        if (args.Length > 0 && (!int.TryParse(args[0], out top) || top <= 0))
        {
            shell.WriteError(Help);
            return;
        }

        var byProto = new Dictionary<string, int>();
        var byMap = new Dictionary<EntityUid, int>();
        var byGrid = new Dictionary<EntityUid, int>();
        var total = 0;
        var nullspace = 0;
        var paused = 0;

        var query = _entManager.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            total++;
            var proto = meta.EntityPrototype?.ID ?? "<no prototype>";
            byProto[proto] = byProto.GetValueOrDefault(proto) + 1;

            if (meta.EntityPaused)
                paused++;

            if (xform.MapUid is { } map)
                byMap[map] = byMap.GetValueOrDefault(map) + 1;
            else
                nullspace++;

            if (xform.GridUid is { } grid)
                byGrid[grid] = byGrid.GetValueOrDefault(grid) + 1;
        }

        var grids = 0;
        var gridQuery = _entManager.AllEntityQueryEnumerator<MapGridComponent>();
        while (gridQuery.MoveNext(out _, out _))
            grids++;

        var sb = new StringBuilder();
        sb.Append($"Entities {total}, paused {paused}, nullspace {nullspace}, maps {byMap.Count}, grids {grids}, heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB");

        sb.Append("\nBy map:");
        foreach (var (uid, count) in byMap.OrderByDescending(p => p.Value).Take(top))
            sb.Append($"\n  {count,8}  {_entManager.ToPrettyString(uid)}{(_entManager.GetComponent<MetaDataComponent>(uid).EntityPaused ? " [paused]" : "")}");

        sb.Append("\nBy grid:");
        foreach (var (uid, count) in byGrid.OrderByDescending(p => p.Value).Take(top))
            sb.Append($"\n  {count,8}  {_entManager.ToPrettyString(uid)} map {_entManager.GetComponent<TransformComponent>(uid).MapID}");

        sb.Append("\nBy prototype:");
        foreach (var (proto, count) in byProto.OrderByDescending(p => p.Value).Take(top))
            sb.Append($"\n  {count,8}  {proto}");

        var text = sb.ToString();
        shell.WriteLine(text);
        _log.GetSawmill("luam.entstats").Info(text);
    }
}
