using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Lua.SpaceHazards;

[RegisterComponent]
[Access(typeof(SectorCelestialPlacerSystem))]
public sealed partial class SectorCelestialPlacerControllerComponent : Component
{
    [DataField]
    public float SpawnDistance = 8000f;

    [DataField]
    public float MinStationClearance = 2000f; // LuaM

    [DataField]
    public float? MaxSpawnDistance; // LuaM

    [DataField]
    public CelestialKind? ForceKind;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string StarPrototype = "SectorCelestialStar";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BlackHolePrototype = "SectorCelestialBlackHole";

    [DataField]
    public bool Spawned;
}
