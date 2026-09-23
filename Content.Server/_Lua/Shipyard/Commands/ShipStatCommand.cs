using System.Linq;
using System.Text;
using Content.Server._Mono.FireControl;
using Content.Server.Administration;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Warps;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Administration;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Lua.Shipyard.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class ShipStatCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    public string Command => "shipstat";
    public string Description => _loc.GetString("cmd-shipstat-desc");
    public string Help => _loc.GetString("cmd-shipstat-help");

    private const int MicroMaxTiles = 81;
    private const int SmallMaxTiles = 441;
    private const int MediumMaxTiles = 961;
    private const int LargeMaxTiles = 1412;
    private const int MaxAirAlarms = 2;
    private const string Indent = "  ";

    private static readonly HashSet<string> ForbiddenPower = new() { "DebugSMES" };

    private static readonly HashSet<string> ForbiddenGenerators = new()
    {
        "GeneratorWallmountAPU", "GeneratorWallmountBasic", "GeneratorRTG", "GeneratorRTGDamaged",
        "GeneratorBasic15kW", "DebugGenerator", "GeneratorBasic",
    };

    private static readonly HashSet<string> Indestructible = new()
    {
        "WallCultIndestructible", "WindowCultIndestructibleInvisible", "WallPlastitaniumDiagonalIndestructible",
        "WallPlastitaniumIndestructible", "PlastitaniumWindowIndestructible", "StationAnchorIndestructible",
    };

    private static readonly HashSet<string> ForbiddenFtl = new() { "MachineFTLDrive", "MachineFTLDrive50", "MachineFTLDrive25S" };

    private static readonly HashSet<string> ForbiddenIff = new()
    {
        "ComputerIFFPOI", "ComputerTabletopIFFPOI", "ComputerIFFSyndicate", "ComputerTabletopIFFSyndicate",
    };

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryGetGrid(shell, args, out var grid))
            return;

        shell.WriteLine(BuildReport(grid));
    }

    private bool TryGetGrid(IConsoleShell shell, string[] args, out EntityUid grid)
    {
        grid = default;

        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return false;
        }

        if (args.Length == 1)
        {
            if (!NetEntity.TryParse(args[0], out var netGrid)
                || !_entManager.TryGetEntity(netGrid, out var uid)
                || !_entManager.HasComponent<MapGridComponent>(uid))
            {
                shell.WriteError(_loc.GetString("cmd-shipstat-bad-grid", ("value", args[0])));
                return false;
            }

            grid = uid.Value;
            return true;
        }

        if (shell.Player?.AttachedEntity is not { } attached
            || _entManager.GetComponent<TransformComponent>(attached).GridUid is not { } gridUid)
        {
            shell.WriteError(_loc.GetString("cmd-shipstat-no-grid"));
            return false;
        }

        grid = gridUid;
        return true;
    }

    private string BuildReport(EntityUid grid)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_loc.GetString("cmd-shipstat-header"));

        var tiles = 0;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        var gridComp = _entManager.GetComponent<MapGridComponent>(grid);
        foreach (var tile in _entManager.System<SharedMapSystem>().GetAllTiles(grid, gridComp))
        {
            tiles++;
            minX = Math.Min(minX, tile.X);
            minY = Math.Min(minY, tile.Y);
            maxX = Math.Max(maxX, tile.X);
            maxY = Math.Max(maxY, tile.Y);
        }

        var width = tiles > 0 ? maxX - minX + 1 : 0;
        var height = tiles > 0 ? maxY - minY + 1 : 0;
        sb.AppendLine(_loc.GetString("cmd-shipstat-size",
            ("width", width), ("height", height), ("tiles", tiles), ("side", Math.Max(width, height))));

        if (tiles > LargeMaxTiles)
            sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-too-big", ("tiles", tiles), ("max", LargeMaxTiles)));

        var size = tiles > MediumMaxTiles ? VesselSize.Large
            : tiles > SmallMaxTiles ? VesselSize.Medium
            : tiles > MicroMaxTiles ? VesselSize.Small
            : VesselSize.Micro;

        sb.AppendLine(_loc.GetString("cmd-shipstat-category", ("size", size.ToString()),
            ("micro", MicroMaxTiles), ("small", SmallMaxTiles), ("medium", MediumMaxTiles), ("large", LargeMaxTiles)));

        var price = _entManager.System<PricingSystem>().AppraiseGrid(grid);
        sb.AppendLine(_loc.GetString("cmd-shipstat-appraisal",
            ("price", (int) price), ("min", (int) (price * 1.05)), ("max", (int) (price * 1.3))));

        AppendRules(sb, grid, size);

        if (_entManager.GetComponent<MetaDataComponent>(grid).EntityLifeStage < EntityLifeStage.MapInitialized)
        {
            var mapId = _entManager.GetComponent<TransformComponent>(grid).MapID;
            sb.AppendLine(_loc.GetString("cmd-shipstat-uninitialized", ("map", mapId.ToString())));
        }

        sb.AppendLine(_loc.GetString("cmd-shipstat-footer"));
        return sb.ToString();
    }

    private void AppendRules(StringBuilder sb, EntityUid grid, VesselSize size)
    {
        var fireControl = _entManager.System<FireControlSystem>();
        var violations = new List<string>();
        var gunneryServers = new List<string>();

        int guns = 0, gunCost = 0, serverCapacity = 0, airAlarms = 0, cash = 0, godmode = 0;
        int smesBasic = 0, smesAdvanced = 0, substationWall = 0, substationBasic = 0;
        var hasWarp = false;

        var query = _entManager.AllEntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var xform, out var meta))
        {
            if (xform.GridUid != grid)
                continue;

            if (_entManager.TryGetComponent<FireControllableComponent>(uid, out var controllable))
            {
                guns++;
                gunCost += fireControl.GetProcessingPowerCost(uid, controllable);
            }

            if (_entManager.TryGetComponent<FireControlServerComponent>(uid, out var server))
            {
                serverCapacity += server.ProcessingPower;
                if (meta.EntityPrototype?.ID is { } serverId && serverId.StartsWith("GunneryServer"))
                    gunneryServers.Add(serverId);
            }

            if (_entManager.HasComponent<AirAlarmComponent>(uid))
                airAlarms++;
            if (_entManager.HasComponent<WarpPointComponent>(uid))
                hasWarp = true;
            if (_entManager.HasComponent<CashComponent>(uid))
                cash++;
            if (_entManager.HasComponent<GodmodeComponent>(uid))
                godmode++;

            if (meta.EntityPrototype?.ID is not { } id)
                continue;

            switch (id)
            {
                case "SubstationWallBasic":
                    substationWall++;
                    break;
                case "SubstationBasic" or "SubstationBasicEmpty":
                    substationBasic++;
                    break;
                case "SMESBasic" or "SMESBasicEmpty":
                    smesBasic++;
                    break;
                case "SMESAdvanced" or "SMESAdvancedEmpty":
                    smesAdvanced++;
                    break;
            }

            if (GetForbiddenCategory(id) is { } category)
            {
                violations.Add(_loc.GetString("cmd-shipstat-forbidden",
                    ("category", _loc.GetString($"cmd-shipstat-category-{category}")), ("id", id)));
            }
        }

        sb.AppendLine(_loc.GetString("cmd-shipstat-rules"));
        sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-guns",
            ("count", guns), ("cost", gunCost), ("capacity", serverCapacity), ("status", Status(gunCost <= serverCapacity))));
        sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-air-alarms",
            ("count", airAlarms), ("max", MaxAirAlarms), ("status", Status(airAlarms <= MaxAirAlarms))));
        sb.Append(Indent).AppendLine(_loc.GetString(hasWarp ? "cmd-shipstat-warp-ok" : "cmd-shipstat-warp-missing"));

        if (cash > 0)
            sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-cash", ("count", cash)));
        if (godmode > 0)
            sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-godmode", ("count", godmode)));

        sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-power",
            ("smesBasic", smesBasic), ("smesAdvanced", smesAdvanced),
            ("substationWall", substationWall), ("substationBasic", substationBasic)));

        CheckPowerLimits(violations, size, smesBasic, smesAdvanced, substationWall, substationBasic);

        var allowedServers = GetAllowedGunneryServers(size);
        foreach (var serverId in gunneryServers)
        {
            if (!allowedServers.Contains(serverId))
            {
                violations.Add(_loc.GetString("cmd-shipstat-gunnery",
                    ("id", serverId), ("size", size.ToString()), ("allowed", string.Join(", ", allowedServers))));
            }
        }

        if (violations.Count == 0)
        {
            sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-no-violations"));
            return;
        }

        sb.Append(Indent).AppendLine(_loc.GetString("cmd-shipstat-violations"));
        foreach (var violation in violations.Distinct())
        {
            sb.Append(Indent).Append(Indent).AppendLine(violation);
        }
    }

    private void CheckPowerLimits(List<string> violations, VesselSize size,
        int smesBasic, int smesAdvanced, int substationWall, int substationBasic)
    {
        switch (size)
        {
            case VesselSize.Micro:
                Limit(violations, "SubstationWallBasic", substationWall, 1, size);
                Limit(violations, "SMESBasic", smesBasic, 1, size);
                Banned(violations, "SMESAdvanced", smesAdvanced, size);
                Banned(violations, "SubstationBasic", substationBasic, size);
                break;
            case VesselSize.Small:
                Limit(violations, "SubstationWallBasic", substationWall, 2, size);
                Limit(violations, "SMESBasic", smesBasic, 1, size);
                Banned(violations, "SMESAdvanced", smesAdvanced, size);
                Banned(violations, "SubstationBasic", substationBasic, size);
                break;
            case VesselSize.Medium:
                Limit(violations, "SubstationWallBasic", substationWall, 2, size);
                Limit(violations, "SMESBasic", smesBasic, 2, size);
                Banned(violations, "SMESAdvanced", smesAdvanced, size);
                Banned(violations, "SubstationBasic", substationBasic, size);
                break;
            case VesselSize.Large:
                if (smesBasic > 0 && smesAdvanced > 0)
                    violations.Add(_loc.GetString("cmd-shipstat-smes-mixed", ("size", size.ToString())));
                else
                {
                    Limit(violations, "SMESBasic", smesBasic, 4, size);
                    Limit(violations, "SMESAdvanced", smesAdvanced, 4, size);
                }

                if (substationWall > 0 && substationBasic > 0)
                {
                    LimitMixed(violations, "SubstationWallBasic", substationWall, 1, size);
                    LimitMixed(violations, "SubstationBasic", substationBasic, 2, size);
                }
                else
                {
                    Limit(violations, "SubstationWallBasic", substationWall, 3, size);
                    Limit(violations, "SubstationBasic", substationBasic, 2, size);
                }
                break;
        }
    }

    private void Limit(List<string> violations, string id, int count, int limit, VesselSize size)
    {
        if (count > limit)
        {
            violations.Add(_loc.GetString("cmd-shipstat-limit",
                ("id", id), ("count", count), ("limit", limit), ("size", size.ToString())));
        }
    }

    private void LimitMixed(List<string> violations, string id, int count, int limit, VesselSize size)
    {
        if (count > limit)
        {
            violations.Add(_loc.GetString("cmd-shipstat-limit-mixed",
                ("id", id), ("count", count), ("limit", limit), ("size", size.ToString())));
        }
    }

    private void Banned(List<string> violations, string id, int count, VesselSize size)
    {
        if (count > 0)
            violations.Add(_loc.GetString("cmd-shipstat-banned-for-size", ("id", id), ("size", size.ToString())));
    }

    private string Status(bool ok)
    {
        return _loc.GetString(ok ? "cmd-shipstat-status-ok" : "cmd-shipstat-status-over");
    }

    private static string[] GetAllowedGunneryServers(VesselSize size)
    {
        return size switch
        {
            VesselSize.Micro => new[] { "GunneryServerLow" },
            VesselSize.Small => new[] { "GunneryServerLow", "GunneryServerMedium" },
            VesselSize.Medium => new[] { "GunneryServerLow", "GunneryServerMedium", "GunneryServerHigh" },
            VesselSize.Large => new[] { "GunneryServerLow", "GunneryServerMedium", "GunneryServerHigh", "GunneryServerUltra" },
            _ => Array.Empty<string>(),
        };
    }

    private static string? GetForbiddenCategory(string id)
    {
        if (ForbiddenPower.Contains(id))
            return "power";
        if (ForbiddenGenerators.Contains(id))
            return "generator";
        if (Indestructible.Contains(id))
            return "structure";
        if (ForbiddenFtl.Contains(id))
            return "ftl";
        if (ForbiddenIff.Contains(id))
            return "iff";
        if (id == "ShieldGeneratorPOI")
            return "shield";
        if (id.Contains("GasMiner"))
            return "atmos";
        if (id.Contains("Debug"))
            return "debug";

        return null;
    }
}
