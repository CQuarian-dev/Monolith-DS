namespace Content.Server._WG.GroundCover;

/// <summary>Marks a puddle whose warmth has already melted the snow it lay on, so it melts that snow only once.</summary>
[RegisterComponent]
public sealed partial class WGPuddleMeltComponent : Component;
