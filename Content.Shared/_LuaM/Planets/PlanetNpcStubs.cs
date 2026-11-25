using Robust.Shared.Prototypes;

// -- LuaM: заглушки типов NPC Wolfgate (лагеря, патрули на технике) до шага 5 переноса. Пространства имён их, файлы удалить вместе с переносом настоящих.
namespace Content.Shared._WG.PlanetPois
{
    [Prototype("wgPlanetCamp")]
    public sealed partial class WGPlanetCampPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField]
        public LocId Name;
    }

    [DataDefinition]
    public sealed partial class WGPlanetCampEntry
    {
        [DataField(required: true)]
        public ProtoId<WGPlanetCampPrototype> Camp;

        [DataField]
        public int MinCount;

        [DataField]
        public int MaxCount = 1;
    }
}

namespace Content.Shared._WG.NpcVehicleCrew
{
    [Prototype]
    public sealed partial class WGVehicleCrewPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;
    }
}
