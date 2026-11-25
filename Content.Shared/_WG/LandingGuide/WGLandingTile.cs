namespace Content.Shared._WG.LandingGuide;

/// <summary>What a ground tile under a hull holds for a set-down.</summary>
public enum WGLandingTile : byte
{
    /// <summary>Ground with nothing solid on it.</summary>
    Clear,

    /// <summary>Something hard and anchored, such as a wall or rock, that refuses a gentle set-down.</summary>
    Blocked,

    /// <summary>Another grid, which a hull setting down crushes.</summary>
    Grid,

    /// <summary>No ground known here: not loaded yet, outside the world's circle or a hole.</summary>
    NoGround,
}
