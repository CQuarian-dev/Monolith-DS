using Robust.Shared.Containers;

namespace Content.Server._LuaM.Pvs;

public sealed partial class PvsDetailSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;

    private const int DetailDepth = 2;

    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ContainerManagerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnInserted(EntGotInsertedIntoContainerMessage args)
    {
        Refresh(args.Entity);
    }

    private void OnRemoved(EntRemovedFromContainerMessage args)
    {
        Refresh(args.Entity);
    }

    private void OnMapInit(Entity<ContainerManagerComponent> ent, ref MapInitEvent args)
    {
        foreach (var container in ent.Comp.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                Refresh(child);
            }
        }
    }

    private void Refresh(EntityUid uid)
    {
        if (!_metaQuery.TryComp(uid, out var meta) || meta.EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        var detail = ContainerDepth(uid) >= DetailDepth;
        if (((meta.Flags & MetaDataFlags.PvsDetail) != 0) != detail)
            _meta.SetFlag((uid, meta), MetaDataFlags.PvsDetail, detail);

        if (!_containerQuery.TryComp(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                Refresh(child);
            }
        }
    }

    private int ContainerDepth(EntityUid uid)
    {
        var depth = 0;
        while (depth < DetailDepth
               && _metaQuery.TryComp(uid, out var meta)
               && (meta.Flags & MetaDataFlags.InContainer) != 0)
        {
            depth++;
            uid = _xformQuery.GetComponent(uid).ParentUid;
        }

        return depth;
    }
}
