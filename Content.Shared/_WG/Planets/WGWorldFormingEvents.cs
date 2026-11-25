using Robust.Shared.Serialization;

namespace Content.Shared._WG.Planets;

/// <summary>What a forming world is working on; each has a <c>wg-world-forming-stage-*</c> label.</summary>
public enum WGWorldFormingStage : byte
{
    Waiting,
    Caverns,
    Cities,
    Sites,
    Roads,
    Wrecks,
    Terrain,
    Boundary,
    Wildlife,
}

/// <summary>One world still forming: its name, stage and progress from 0 to 1.</summary>
[Serializable, NetSerializable]
public sealed class WGWorldFormingEntry
{
    public NetEntity Ground;

    /// <summary>Every map of the world, orbit and caverns included, for the in-round bar.</summary>
    public List<NetEntity> Maps = new();

    public string Name = string.Empty;
    public WGWorldFormingStage Stage;
    public float Progress;

    /// <summary>Built in the lobby, ahead of the round.</summary>
    public bool RoundStart;
}

/// <summary>Sent to every client a few times a second while worlds form, and once empty when they are done.</summary>
[Serializable, NetSerializable]
public sealed class WGWorldFormingEvent : EntityEventArgs
{
    public List<WGWorldFormingEntry> Worlds = new();

    /// <summary>Whether the round start is being held for them.</summary>
    public bool RoundHeld;

    /// <summary>The overall progress over every world, from 0 to 1.</summary>
    public float Overall;
}
