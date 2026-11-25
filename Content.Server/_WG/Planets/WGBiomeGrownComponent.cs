namespace Content.Server._WG.Planets;

/// <summary>On a biome entity its chunk's unload kept: the tile the biome grew it on.</summary>
[RegisterComponent]
public sealed partial class WGBiomeGrownComponent : Component
{
    [DataField]
    public Vector2i Tile;
}
