using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.SpaceHazards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectorAsteroidClusterMarkersComponent : Component
{
    [AutoNetworkedField]
    public List<Vector2> Centers = new();

    [AutoNetworkedField]
    public List<float> Radii = new();
}
