using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Ghost;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    private readonly HashSet<EntityUid> _wgStanders = new();

    /// <summary>
    /// Reparents loose entities standing on a hull's tiles to the hull before it changes map. Ones the ground
    /// layer still owns, such as crates and players who walked aboard, would be left behind.
    /// </summary>
    private void WgAdoptStanders(HashSet<EntityUid> hulls)
    {
        var lookup = EntityManager.System<EntityLookupSystem>();

        foreach (var hull in hulls)
        {
            if (!TryComp<MapGridComponent>(hull, out var grid)
                || Transform(hull) is not { MapID: var mapId } hullXform
                || mapId == MapId.Nullspace)
                continue;

            var matrix = _transform.GetWorldMatrix(hullXform);
            var box = matrix.TransformBox(grid.LocalAABB);

            _wgStanders.Clear();
            lookup.GetEntitiesIntersecting(mapId, box, _wgStanders, LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);

            var inverse = _transform.GetInvWorldMatrix(hullXform);
            foreach (var stander in _wgStanders)
            {
                if (TerminatingOrDeleted(stander) || HasComp<MapGridComponent>(stander) || HasComp<MapComponent>(stander)
                    || HasComp<GhostComponent>(stander))
                    continue;

                var xform = Transform(stander);
                var parent = xform.ParentUid;
                // GridTraversal off opts out of riding hulls, as the orbit marker does; adopting it carried the
                // planet's marker off on the first ship to lift off over it.
                if (xform.Anchored || !xform.GridTraversal || parent == hull || hulls.Contains(parent) || xform.MapUid is null)
                    continue;

                // Only what rests directly on the ground layer or its map; never another ship's deck.
                if (parent != xform.MapUid && !(HasComp<MapGridComponent>(parent) && !HasComp<ShuttleComponent>(parent)))
                    continue;

                var local = Vector2.Transform(_transform.GetWorldPosition(xform), inverse);
                if (!_map.TryGetTileRef(hull, grid, _map.LocalToTile(hull, grid, new EntityCoordinates(hull, local)), out var tile)
                    || tile.Tile.IsEmpty)
                    continue;

                _transform.SetParent(stander, xform, hull);
            }
        }
    }
}
