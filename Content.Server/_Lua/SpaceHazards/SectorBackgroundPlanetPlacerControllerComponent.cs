using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Lua.SpaceHazards;

[RegisterComponent]
[Access(typeof(SectorBackgroundPlanetPlacerSystem))]
public sealed partial class SectorBackgroundPlanetPlacerControllerComponent : Component
{
    [DataField]
    public int MinCount = 2;

    [DataField]
    public int MaxCount = 4;

    [DataField]
    public float MinDistance = 6000f;

    [DataField]
    public float MaxDistance = 12000f;

    [DataField]
    public float MinStationClearance = 2000f;

    // LuaM start
    [DataField]
    public float MinSpriteRadius = 40f;

    [DataField]
    public float MaxSpriteRadius = 80f;

    [DataField]
    public float AnchorMinSpriteRadius = 75f;

    [DataField]
    public float AnchorMaxSpriteRadius = 125f;
    // LuaM end

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PlanetPrototype = "SectorBackgroundPlanet";

    [DataField]
    public bool Spawned;
}
