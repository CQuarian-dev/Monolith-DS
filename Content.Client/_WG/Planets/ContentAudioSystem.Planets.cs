using Content.Client.Gameplay;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets;

namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{
    private bool _wgPlanetMusicSuppressed;

    private bool WgOnPlanet()
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? xform) || xform.MapUid is not { } map)
            return false;

        if (TryComp<CEZTransitMapComponent>(map, out var transit) && transit.LowerMap is { } lower)
            map = lower;

        return HasComp<WGPlanetLayerComponent>(map) && !HasComp<WGOrbitLayerComponent>(map);
    }

    /// <summary>Silences ambient music on a planet surface and restarts it on the way back out.</summary>
    private void WgUpdatePlanetMusic()
    {
        var suppress = WgOnPlanet();
        var wasSuppressed = _wgPlanetMusicSuppressed;
        _wgPlanetMusicSuppressed = suppress;
        if (suppress)
        {
            if (!_isCombatMusicPlaying)
            {
                DisableAmbientMusic();
                _replayAmbientMusicBool = false;
                _replayAmbientMusicTimer = 0;
            }
            return;
        }

        // Returning to space may keep the same grid and biome, so no ordinary music event fires.
        if (wasSuppressed && !_isCombatMusicPlaying && _state.CurrentState is GameplayState)
            ReplayAmbientMusic();
    }
}
