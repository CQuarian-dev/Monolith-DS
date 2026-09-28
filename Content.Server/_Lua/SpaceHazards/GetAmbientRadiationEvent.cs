namespace Content.Server._Lua.SpaceHazards;

[ByRefEvent]
public record struct GetAmbientRadiationEvent(float Radiation = 0f);
