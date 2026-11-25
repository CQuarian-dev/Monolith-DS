using Content.Server._WG.Planets.Flight;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>Mass other modules add to a grid's pooled lift load, collected through WGGridLiftLoadEvent.</summary>
    /// <param name="grid">Grid to weigh when no network is supplied.</param>
    /// <param name="networkGrids">Every member of the grid's z-network, when the caller pooled one.</param>
    public float GetWGVirtualMass(EntityUid grid, IReadOnlyCollection<EntityUid>? networkGrids = null)
    {
        if (networkGrids == null)
            return WgLiftLoad(grid);

        var mass = 0f;

        foreach (var member in networkGrids)
        {
            mass += WgLiftLoad(member);
        }

        return mass;
    }

    /// <summary>Virtual cargo mass across a grid's whole rigid set, as lift counts it.</summary>
    public float WgGetSetLiftLoad(EntityUid grid)
    {
        return GetWGVirtualMass(grid, CollectRigidSet(grid));
    }

    /// <summary>Hull mass and virtual cargo mass across a grid's whole rigid set, as lift weighs them.</summary>
    public void WgGetSetLiftMass(EntityUid grid, out float hull, out float load)
    {
        hull = 0f;
        load = 0f;
        foreach (var member in CollectRigidSet(grid))
        {
            if (_physQuery.TryComp(member, out var body))
                hull += body.FixturesMass;

            load += WgLiftLoad(member);
        }
    }

    /// <summary>Virtual cargo mass other modules report for one grid.</summary>
    private float WgLiftLoad(EntityUid grid)
    {
        var ev = new WGGridLiftLoadEvent(0f);
        RaiseLocalEvent(grid, ref ev);
        return ev.Mass;
    }
}
