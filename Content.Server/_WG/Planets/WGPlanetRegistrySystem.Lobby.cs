using System.Numerics;
using Content.Server._FarHorizons.StarSystem;
using Content.Server.GameTicking;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.Planets;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WG.Planets;

/// <summary>
/// Builds the lobby preset's round-start worlds while the lobby is open, at the spots its star system will put
/// them, so their preload runs before anyone is in the round. Whatever no body takes over is deleted once the
/// round is under way.
/// </summary>
public sealed partial class WGPlanetRegistrySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;

    /// <summary>How long after the round starts unadopted prebuilt networks are deleted: the star system spawns on the first tick.</summary>
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromSeconds(5);

    private bool _prebuildPending;
    private TimeSpan? _orphanSweep;

    /// <summary>Round-start worlds still to build, one a tick so no lobby tick builds them all.</summary>
    private readonly Queue<(WGPlanetSurfacePrototype Surface, Vector2 Centre, string Name)> _prebuildQueue = new();
    private int _prebuildBuilt;
    private string? _prebuildPreset;

    /// <summary>Whether lobby worlds are still waiting their turn to be built.</summary>
    public bool PrebuildQueued => _prebuildQueue.Count > 0;

    private void InitializeLobby()
    {
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        switch (args.New)
        {
            // Next tick: the lobby's preset is settled after the run level changes.
            case GameRunLevel.PreRoundLobby:
                _prebuildPending = true;
                _prebuildQueue.Clear();
                // A round restarted inside the grace would otherwise sweep the new lobby's worlds away.
                _orphanSweep = null;
                break;
            case GameRunLevel.InRound:
                _prebuildPending = false;
                // What never got its turn is built by the star system as the round starts, as without prebuilding.
                _prebuildQueue.Clear();
                _orphanSweep = _timing.CurTime + OrphanGrace;
                break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_prebuildPending)
        {
            _prebuildPending = false;
            Prebuild();
        }
        else if (_prebuildQueue.TryDequeue(out var next))
        {
            if (_ticker.RunLevel != GameRunLevel.PreRoundLobby || !PlanetCVars.NetworksEnabled(_cfg)) // LuaM
                _prebuildQueue.Clear();
            else if (_networks.Prebuild(next.Surface, next.Centre, next.Name))
                _prebuildBuilt++;

            if (_prebuildQueue.Count == 0 && _prebuildBuilt > 0)
                Log.Info($"Prebuilt {_prebuildBuilt} planet networks for preset \"{_prebuildPreset}\" in the lobby.");
        }

        if (_orphanSweep is { } due && _timing.CurTime >= due)
        {
            _orphanSweep = null;
            var cleared = _networks.ClearPrebuilt();

            if (cleared > 0)
                Log.Warning($"Deleted {cleared} prebuilt planet networks no sector body took over.");
        }
    }

    /// <summary>Queues every round-start world of the lobby preset's star system to be built, one a tick.</summary>
    private void Prebuild()
    {
        if (!PlanetCVars.NetworksEnabled(_cfg) || !_cfg.GetCVar(PlanetCVars.Prebuild))
            return;

        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby || _ticker.Preset is not { } preset)
            return;

        _networks.ClearPrebuilt();
        _prebuildQueue.Clear();
        _prebuildBuilt = 0;
        _prebuildPreset = preset.ID;

        foreach (var ruleId in preset.Rules)
        {
            if (!_proto.TryIndex<EntityPrototype>(ruleId, out var rule)
                || !rule.TryComp<StarSystemRuleComponent>(out var starSystem, _factory)
                || !_proto.TryIndex(SectorLayout(starSystem.System), out var system))
                continue;

            foreach (var entry in system.Planets)
            {
                if (!_surfaces.TryGetValue(entry.Planet, out var surface) || !surface.BuildAtRoundStart)
                    continue;

                if (!_proto.TryIndex(entry.Planet, out var type))
                    continue;

                // The same spot the star system builder spawns the body at.
                var centre = new Vector2(MathF.Cos(entry.Angle), MathF.Sin(entry.Angle)) * entry.Distance;

                _prebuildQueue.Enqueue((surface, centre, entry.Name ?? type.Name));
            }
        }
    }
}
