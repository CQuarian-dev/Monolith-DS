/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Server._Mono.FireControl;
using Content.Server.Shuttles.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._LuaM.Planets;

// -- Снаряд корабельного орудия в небе планеты задевает корабли на слое ниже и в зазоре между слоями: от огня не уйти
// -- на соседнюю высоту. Землю и город снаряд не трогает
public sealed class LuaMShipFireLayersSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float ProbeHalfSize = 0.1f;

    private readonly List<EntityUid> _targets = new();
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(EntityUid Shot, MapCoordinates To)> _moves = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireControllableComponent, AmmoShotEvent>(OnShot);
    }

    private void OnShot(Entity<FireControllableComponent> ent, ref AmmoShotEvent args)
    {
        if (!IsSkyMap(Transform(ent).MapUid))
            return;

        foreach (var shot in args.FiredProjectiles)
        {
            EnsureComp<LuaMShipShotComponent>(shot);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _moves.Clear();
        var query = EntityQueryEnumerator<LuaMShipShotComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } map)
                continue;

            CollectTargetMaps(map);
            if (_targets.Count == 0)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            foreach (var target in _targets)
            {
                if (!ShipAt(target, pos))
                    continue;

                _moves.Add((uid, new MapCoordinates(pos, Transform(target).MapID)));
                break;
            }
        }

        foreach (var (shot, to) in _moves)
        {
            if (TerminatingOrDeleted(shot))
                continue;

            RemComp<LuaMShipShotComponent>(shot);
            _transform.SetMapCoordinates(shot, to);
        }
    }

    private bool IsSkyMap(EntityUid? map)
    {
        if (map is not { } uid || HasComp<WGOrbitLayerComponent>(uid))
            return false;

        if (TryComp<CEZTransitMapComponent>(uid, out var transit))
            return transit.LowerMap is { } lower && HasComp<WGPlanetLayerComponent>(lower);

        return HasComp<WGPlanetLayerComponent>(uid) && TryComp<CEZMapComponent>(uid, out var zMap) && zMap.Depth > 0;
    }

    // -- Со слоя: слой ниже и зазор между ними. Из зазора: оба его слоя. Земля не цель
    private void CollectTargetMaps(EntityUid map)
    {
        _targets.Clear();
        if (TryComp<CEZTransitMapComponent>(map, out var transit))
        {
            AddLayer(transit.UpperMap);
            AddLayer(transit.LowerMap);
            return;
        }

        if (!TryComp<CEZMapComponent>(map, out var zMap) || zMap.Depth <= 0
            || !_zLevels.TryMapDown((map, zMap), out var below))
            return;

        AddLayer(below.Owner);
        var transits = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transits.MoveNext(out var gap, out var gapComp))
        {
            if (gapComp.LowerMap == below.Owner && gapComp.UpperMap == map)
                _targets.Add(gap);
        }
    }

    private void AddLayer(EntityUid? layer)
    {
        if (layer is { } uid && TryComp<CEZMapComponent>(uid, out var zMap) && zMap.Depth > 0)
            _targets.Add(uid);
    }

    private bool ShipAt(EntityUid map, Vector2 pos)
    {
        _grids.Clear();
        var box = Box2.CenteredAround(pos, new Vector2(ProbeHalfSize * 2f));
        _map.FindGridsIntersecting(map, box, ref _grids, includeMap: false);
        foreach (var grid in _grids)
        {
            if (grid.Owner == map || !HasComp<ShuttleComponent>(grid))
                continue;

            if (_map.TryGetTileRef(grid.Owner, grid.Comp, _map.WorldToTile(grid.Owner, grid.Comp, pos), out var tile)
                && !tile.Tile.IsEmpty)
                return true;
        }

        return false;
    }
}

// -- Снаряд корабельного орудия, ещё не перешедший на соседний слой
[RegisterComponent]
public sealed partial class LuaMShipShotComponent : Component;
