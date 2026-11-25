using System.Numerics;
using Content.Shared._WG.Planets;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /// <summary>Blunt damage to a mob a hull comes down on.</summary>
    public const float WgSetDownDamage = 40f;

    /// <summary>How long a mob a hull came down on is left on the ground.</summary>
    public static readonly TimeSpan WgSetDownStun = TimeSpan.FromSeconds(4);

    /// <summary>How many tiles out a mob under a hull is looked a clear spot for.</summary>
    private const int WgSetDownReach = 48;

    private readonly HashSet<EntityUid> _wgSetDownOn = new();
    private GameTick _wgSetDownTick;

    /// <summary>
    /// A hull coming down on a planet layer hurts a mob under it and shoves it clear, where flattening would gib or
    /// delete it. False for anything that is no mob or on no planet, which the caller flattens as before.
    /// </summary>
    private bool WgSetDownOn(EntityUid hull, EntityUid uid, EntityUid map)
    {
        if (!HasComp<MobStateComponent>(uid) || !HasComp<WGPlanetLayerComponent>(map))
            return false;

        if (_wgSetDownTick != _gameTiming.CurTick)
        {
            _wgSetDownTick = _gameTiming.CurTick;
            _wgSetDownOn.Clear();
        }

        // A hull has many fixtures and a mob can lie under several: hurt once, but moved from under each.
        if (_wgSetDownOn.Add(uid))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", WgSetDownDamage);
            _damageSys.TryChangeDamage(uid, damage, origin: hull);
            _stuns.TryParalyze(uid, WgSetDownStun, true);
            _logger.Add(LogType.ShuttleImpact, LogImpact.Medium,
                $"{ToPrettyString(uid):player} was under {ToPrettyString(hull)} as it came down at {Transform(uid).Coordinates:coordinates}");
        }

        if (WgClearSpot(hull, uid, map) is { } spot)
            _transform.SetCoordinates(uid, new EntityCoordinates(map, spot));

        return true;
    }

    /// <summary>The middle of the nearest ground tile to a mob that no hull is over and nothing blocks, ring by ring.</summary>
    private Vector2? WgClearSpot(EntityUid hull, EntityUid uid, EntityUid map)
    {
        if (!TryComp<MapGridComponent>(map, out var ground) || !TryComp<MapGridComponent>(hull, out var hullGrid))
            return null;

        var origin = _mapSystem.WorldToTile(map, ground, _transform.GetWorldPosition(uid));

        for (var ring = 1; ring <= WgSetDownReach; ring++)
        {
            for (var x = -ring; x <= ring; x++)
            for (var y = -ring; y <= ring; y++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring)
                    continue;

                var index = origin + new Vector2i(x, y);

                if (!_mapSystem.TryGetTileRef(map, ground, index, out var tile) || tile.Tile.IsEmpty)
                    continue;

                var centre = _mapSystem.GridTileToWorldPos(map, ground, index);

                if (WgUnderHull(hull, hullGrid, map, centre) || _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                    continue;

                return centre;
            }
        }

        return null;
    }

    /// <summary>Whether the tile round a spot on a map is under the hull coming down, or any other.</summary>
    private bool WgUnderHull(EntityUid hull, MapGridComponent hullGrid, EntityUid map, Vector2 centre)
    {
        var half = new Vector2(0.5f);

        // The hull's own tiles, as its place among the map's grids may not be set yet.
        foreach (var tile in _mapSystem.GetTilesIntersecting(hull, hullGrid, new Box2(centre - half, centre + half)))
        {
            if (!tile.Tile.IsEmpty)
                return true;
        }

        return _mapSystem.TryFindGridAt(map, centre, out var at, out _) && at != map;
    }
}
