/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._WG.Caverns;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.PlanetCities;
using Content.Shared._WG.Planets;
using Content.Shared.Doors.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Planets;

// -- Крыша держится на стенах, дверях и окнах. Когда они рушатся, крыша над тайлами без опоры в пределах SupportReach
// -- тайлов обваливается, как в RMC: от взорванного дома не остаётся висящей кровли
public sealed class LuaMRoofCollapseSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private TagSystem _tags = default!;

    private const int SupportReach = 7;
    private const int CellSize = RoofComponent.ChunkSize;
    // -- Выгрузка чанков биома сносит сотни скал за тик: участки разбираются понемногу, а не все сразу
    private const int CellsPerTick = 4;
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private readonly HashSet<(EntityUid Grid, Vector2i Cell)> _pending = new();
    private readonly List<(EntityUid Grid, Vector2i Cell)> _batch = new();


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        if (args.Anchored || args.Transform.GridUid is not { } grid || !IsPlanetRoof(grid, args.Transform.MapUid)
            || !PlanetCVars.NetworksEnabled(_cfg) || !TryComp<RoofComponent>(grid, out var roof)
            || roof.Data.Count == 0 || !IsSupport(args.Entity)
            || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var cell = Cell(_map.TileIndicesFor(grid, gridComp, args.Transform.Coordinates));
        // -- Без крыши рядом (скалы в чистом поле) проверять нечего
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var around = cell + new Vector2i(x, y);
                if (roof.Data.TryGetValue(around, out var bits) && bits != 0)
                    _pending.Add((grid, around));
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pending.Count == 0)
            return;

        if (!PlanetCVars.NetworksEnabled(_cfg))
        {
            _pending.Clear();
            return;
        }

        _batch.Clear();
        foreach (var entry in _pending)
        {
            _batch.Add(entry);
            if (_batch.Count >= CellsPerTick)
                break;
        }

        foreach (var (grid, cell) in _batch)
        {
            _pending.Remove((grid, cell));
            if (TerminatingOrDeleted(grid) || !IsPlanetRoof(grid, Transform(grid).MapUid)
                || !TryComp<MapGridComponent>(grid, out var gridComp)
                || !TryComp<RoofComponent>(grid, out var roof))
                continue;

            CheckCell((grid, gridComp, roof), cell);
        }
    }

    private void CheckCell(Entity<MapGridComponent, RoofComponent> grid, Vector2i cell)
    {
        var origin = cell * CellSize;
        if (!grid.Comp2.Data.TryGetValue(cell, out var unsupported) || unsupported == 0)
            return;

        for (var x = -SupportReach; x < CellSize + SupportReach; x++)
        {
            for (var y = -SupportReach; y < CellSize + SupportReach; y++)
            {
                var anchored = _map.GetAnchoredEntities(grid, grid.Comp1, origin + new Vector2i(x, y));
                while (anchored.MoveNext(out var ent))
                {
                    if (TerminatingOrDeleted(ent.Value) || !IsSupport(ent.Value))
                        continue;

                    var left = Math.Max(0, x - SupportReach);
                    var right = Math.Min(CellSize - 1, x + SupportReach);
                    var row = ((1UL << (right - left + 1)) - 1) << left;
                    for (var iy = Math.Max(0, y - SupportReach); iy <= Math.Min(CellSize - 1, y + SupportReach); iy++)
                        unsupported &= ~(row << (iy * CellSize));
                    if (unsupported == 0)
                        return;
                    break;
                }
            }
        }

        while (unsupported != 0)
        {
            var bit = System.Numerics.BitOperations.TrailingZeroCount(unsupported);
            unsupported &= unsupported - 1;
            _roof.SetRoof((grid, grid.Comp1, grid.Comp2), origin + new Vector2i(bit % CellSize, bit / CellSize), false);
        }
    }

    private static Vector2i Cell(Vector2i tile)
    {
        return new Vector2i((int) Math.Floor(tile.X / (float) CellSize), (int) Math.Floor(tile.Y / (float) CellSize));
    }

    private bool IsPlanetRoof(EntityUid grid, EntityUid? map) =>
        HasComp<WGPlanetLayerComponent>(map)
        && !HasComp<WGCavernLayerComponent>(map)
        && (grid == map || HasComp<WGPlanetCityGridComponent>(grid));

    private bool IsSupport(EntityUid uid)
    {
        return HasComp<DoorComponent>(uid) || _tags.HasTag(uid, WallTag) || HardBlocker(uid);
    }

    private bool HardBlocker(EntityUid uid)
    {
        return TryComp<PhysicsComponent>(uid, out var body) && body.Hard
               && (body.CollisionLayer & (int) CollisionGroup.Impassable) != 0;
    }
}
