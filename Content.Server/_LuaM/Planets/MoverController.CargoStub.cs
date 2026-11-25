// -- LuaM: модуль CargoLoad Wolfgate не переносим, груз на тягу не влияет
namespace Content.Server.Physics.Controllers;

public sealed partial class MoverController
{
    private float WgCargoSpaceFactor(EntityUid grid) => 1f;
}
