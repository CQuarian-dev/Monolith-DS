/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Cleanup;

// -- Обломки, которые никто не подобрал (брёвна от сбитых деревьев), исчезают через время; поднятое остаётся навсегда
public sealed class LuaMLitterSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LuaMLitterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LuaMLitterComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnMapInit(Entity<LuaMLitterComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.DespawnAt = _timing.CurTime + ent.Comp.Lifetime;
    }

    private void OnInserted(Entity<LuaMLitterComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        RemCompDeferred<LuaMLitterComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        var query = EntityQueryEnumerator<LuaMLitterComponent>();
        while (query.MoveNext(out var uid, out var litter))
        {
            if (now >= litter.DespawnAt)
                QueueDel(uid);
        }
    }
}

// -- Сущность, которая пропадёт, если пролежит нетронутой Lifetime
[RegisterComponent]
public sealed partial class LuaMLitterComponent : Component
{
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public TimeSpan DespawnAt;
}
