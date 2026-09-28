using System.Numerics;

namespace Content.Server._Lua.Worldgen;

[RegisterComponent]
public sealed partial class SectorAsteroidClusterControllerComponent : Component
{
    [DataField]
    public int MinCount = 2;

    [DataField]
    public int MaxCount = 3;

    [DataField]
    public int MinEmptyCount = 1;

    [DataField]
    public int MaxEmptyCount = 2;

    [DataField]
    public float MinDistance = 3000f;

    [DataField]
    public float MaxDistance = 9500f;

    [DataField]
    public float PlanetClusterMinRadius = 400f;

    [DataField]
    public float PlanetClusterMaxRadius = 500f;

    [DataField]
    public float EmptyClusterMinRadius = 250f;

    [DataField]
    public float EmptyClusterMaxRadius = 350f;

    [DataField]
    public float MinStationClearance = 2500f;

    [DataField]
    public float MinSeparation = 1500f;

    [DataField]
    public bool Spawned;

    [ViewVariables]
    public List<SectorAsteroidCluster> Clusters = new();
}

public readonly record struct SectorAsteroidCluster(Vector2 Center, float Radius, bool HasPlanet);
