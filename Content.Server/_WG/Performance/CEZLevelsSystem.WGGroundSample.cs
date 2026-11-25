using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>Reuses a grid's ground sample until it moves, turns or the sample gets old.</summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>Distance a grid may drift before its footprint is sampled again.</summary>
    private const float WgSampleMoveTolerance = 0.05f;

    /// <summary>Turn in radians a grid may make before its footprint is sampled again.</summary>
    private const double WgSampleTurnTolerance = 0.002;

    /// <summary>Longest a sample is kept, so hull and terrain tile changes still show up.</summary>
    private static readonly TimeSpan WgSampleMaxAge = TimeSpan.FromSeconds(2);

    private readonly Dictionary<EntityUid, WgGroundSampleEntry> _wgGroundSamples = new();
    private readonly List<EntityUid> _wgStaleSamples = new();
    private GameTick _wgNextSamplePrune;

    private readonly record struct WgGroundSampleEntry(
        EntityUid Parent,
        Vector2 Position,
        Angle Rotation,
        TimeSpan Taken,
        float Grip,
        bool Contact);

    /// <summary>The grid's last sample if it has stayed put since, else a fresh one.</summary>
    private (float Grip, bool Contact) WgGroundSample(EntityUid grid)
    {
        var xform = Transform(grid);
        var now = _timing.CurTime;
        if (_wgGroundSamples.TryGetValue(grid, out var entry) &&
            entry.Parent == xform.ParentUid &&
            now - entry.Taken < WgSampleMaxAge &&
            (xform.LocalPosition - entry.Position).LengthSquared() < WgSampleMoveTolerance * WgSampleMoveTolerance &&
            Math.Abs((xform.LocalRotation - entry.Rotation).Reduced().Theta) < WgSampleTurnTolerance)
        {
            return (entry.Grip, entry.Contact);
        }

        var sample = ComputeGroundSample(grid);
        _wgGroundSamples[grid] = new WgGroundSampleEntry(xform.ParentUid, xform.LocalPosition, xform.LocalRotation, now,
            sample.Grip, sample.Contact);
        return sample;
    }

    /// <summary>Drops samples no grid has refreshed in a while, such as those of deleted grids.</summary>
    private void WgPruneGroundSamples()
    {
        if (_timing.CurTick < _wgNextSamplePrune)
            return;

        _wgNextSamplePrune = new GameTick(_timing.CurTick.Value + 300);
        var now = _timing.CurTime;
        _wgStaleSamples.Clear();
        foreach (var (grid, entry) in _wgGroundSamples)
        {
            if (now - entry.Taken > WgSampleMaxAge * 2)
                _wgStaleSamples.Add(grid);
        }

        foreach (var grid in _wgStaleSamples)
        {
            _wgGroundSamples.Remove(grid);
        }
    }
}
