using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class PvsDetailFlagTest
{
    [Test]
    public async Task NestedStorageContentsAreDetail()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containers = entMan.System<SharedContainerSystem>();
            var crate = entMan.SpawnEntity("CrateGenericSteel", map.GridCoords);
            var box = entMan.SpawnEntity("BoxCardboard", map.GridCoords);
            var crowbar = entMan.SpawnEntity("Crowbar", map.GridCoords);

            var boxStorage = containers.GetContainer(box, StorageComponent.ContainerId);
            var crateStorage = containers.GetContainer(crate, SharedEntityStorageSystem.ContainerName);

            Assert.That(containers.Insert(crowbar, boxStorage, force: true));
            Assert.That(IsDetail(entMan, crowbar), Is.False);

            Assert.That(containers.Insert(box, crateStorage, force: true));
            Assert.That(IsDetail(entMan, box), Is.False);
            Assert.That(IsDetail(entMan, crowbar), Is.True);

            Assert.That(containers.Remove(box, crateStorage, force: true));
            Assert.That(IsDetail(entMan, crowbar), Is.False);

            Assert.That(containers.Insert(box, crateStorage, force: true));
            Assert.That(containers.Remove(crowbar, boxStorage, force: true));
            Assert.That(IsDetail(entMan, crowbar), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static bool IsDetail(IEntityManager entMan, EntityUid uid)
    {
        return (entMan.GetComponent<MetaDataComponent>(uid).Flags & MetaDataFlags.PvsDetail) != 0;
    }
}
