using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets.Flight;
using Content.Shared.Shuttles.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>How many thrusters a hull needs converted or added to reach full lift, for the console checklist.</summary>
public sealed partial class CEZLevelsSystem
{
    private readonly List<float> _wgConversionGains = new();

    /// <summary>
    /// Counts the conversions, then extra landing thrusters, that would bring the rigid set to full lift.
    /// Counts every enabled engine as powered, so a hull short only of power gets no thruster advice.
    /// </summary>
    public void WgGetLiftAdvice(EntityUid grid, out int convert, out int add)
    {
        convert = 0;
        add = 0;

        if (!WgTryGetPlanetGravity(grid, out var gravity) || gravity <= 0f)
            return;

        _wgConversionGains.Clear();
        var lift = 0f;
        var mass = 0f;
        var ratingSum = 0f;
        var ratingCount = 0;

        foreach (var member in CollectRigidSet(grid))
        {
            if (HasComp<CEZMappingAnchorGridComponent>(member))
                return;

            if (_physQuery.TryComp(member, out var body))
                mass += body.FixturesMass;

            mass += GetWGVirtualMass(member);

            if (_wgThrusterIndex.ThrustersOn(member) is not { } memberThrusters)
                continue;

            foreach (var uid in memberThrusters)
            {
                if (!TryComp<ThrusterComponent>(uid, out var thruster)
                    || thruster.Type != ThrusterType.Linear
                    || !thruster.Enabled
                    || !Transform(uid).Anchored)
                {
                    continue;
                }

                var rated = _wgThrusters.WgRatedThrust(uid, thruster) / ThrusterSystem.WgStandardGravity;
                ratingSum += rated;
                ratingCount++;

                if (HasComp<WGLandingThrusterComponent>(uid))
                {
                    lift += rated;
                    continue;
                }

                lift += rated * ThrusterSystem.WgAtmosphereThrustMultiplier;
                _wgConversionGains.Add(rated * (1f - ThrusterSystem.WgAtmosphereThrustMultiplier));
            }
        }

        var missing = mass * gravity * WGFullLiftRatio - lift;
        if (missing <= 0f)
            return;

        // Biggest engines first, so the count is the fewest conversions that do it.
        _wgConversionGains.Sort((a, b) => b.CompareTo(a));
        foreach (var gain in _wgConversionGains)
        {
            if (missing <= 0f)
                return;

            missing -= gain;
            convert++;
        }

        if (missing <= 0f)
            return;

        var typical = ratingCount > 0
            ? ratingSum / ratingCount
            : new ThrusterComponent().Thrust / ThrusterSystem.WgStandardGravity;

        if (typical > 0f)
            add = (int) MathF.Ceiling(missing / typical);
    }
}
