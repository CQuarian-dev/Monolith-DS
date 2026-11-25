using System.Linq;
using Content.Shared._WG.Planets;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._WG.Planets;

public sealed partial class WGWorldFormingSystem
{
    [Dependency] private IPlayerManager _players = default!;

    /// <summary>The shortest gap between two sends that only move progress.</summary>
    private static readonly TimeSpan WgProgressInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _wgNextProgress;
    private readonly HashSet<ICommonSession> _wgJoined = new();

    /// <summary>How many times the state went to every player.</summary>
    public int WgBroadcasts { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += WgOnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= WgOnPlayerStatusChanged;
        _wgJoined.Clear();
    }

    private void WgOnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
            _wgJoined.Add(args.Session);
        else
            _wgJoined.Remove(args.Session);
    }

    /// <summary>
    /// Sends the state to everyone when the worlds, their stages or the hold changed, or at most once a second when
    /// only progress moved; else sends the last state to players who joined since.
    /// </summary>
    private void WgSendState(WGWorldFormingEvent state)
    {
        var now = _timing.CurTime;
        if (WgStructureChanged(state, Last) || now >= _wgNextProgress && WgProgressChanged(state, Last))
        {
            Last = state;
            _wgNextProgress = now + WgProgressInterval;
            _wgJoined.Clear();
            WgBroadcasts++;
            RaiseNetworkEvent(state);
            return;
        }

        if (_wgJoined.Count == 0)
            return;

        // An empty state is what a fresh client already holds.
        if (Last.Worlds.Count > 0 || Last.RoundHeld)
        {
            var filter = Filter.Empty();
            foreach (var session in _wgJoined)
            {
                if (session.Status == SessionStatus.InGame)
                    filter.AddPlayer(session);
            }

            RaiseNetworkEvent(Last, filter);
        }

        _wgJoined.Clear();
    }

    /// <summary>Whether anything but progress differs between two states.</summary>
    private static bool WgStructureChanged(WGWorldFormingEvent a, WGWorldFormingEvent b)
    {
        if (a.RoundHeld != b.RoundHeld || a.Worlds.Count != b.Worlds.Count)
            return true;

        for (var i = 0; i < a.Worlds.Count; i++)
        {
            var x = a.Worlds[i];
            var y = b.Worlds[i];
            if (x.Ground != y.Ground
                || x.Stage != y.Stage
                || x.Name != y.Name
                || x.RoundStart != y.RoundStart
                || !x.Maps.SequenceEqual(y.Maps))
                return true;
        }

        return false;
    }

    /// <summary>Whether any progress differs between two states of the same worlds.</summary>
    private static bool WgProgressChanged(WGWorldFormingEvent a, WGWorldFormingEvent b)
    {
        // With no worlds the overall is a placeholder.
        if (a.Worlds.Count == 0)
            return false;

        if (!a.Overall.Equals(b.Overall))
            return true;

        for (var i = 0; i < a.Worlds.Count; i++)
        {
            if (!a.Worlds[i].Progress.Equals(b.Worlds[i].Progress))
                return true;
        }

        return false;
    }
}
