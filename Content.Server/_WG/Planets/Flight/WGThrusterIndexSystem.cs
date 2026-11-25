using Content.Server.Shuttles.Components;

namespace Content.Server._WG.Planets.Flight;

/// <summary>Keeps each entity's thrusters indexed by their direct parent, so lift and power sums skip the hull walk.</summary>
public sealed class WGThrusterIndexSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _thrusters = new();
    private readonly Dictionary<EntityUid, EntityUid> _parents = new();

    /// <summary>Bumps whenever a thruster joins, leaves or reparents; memos key on it.</summary>
    public uint Version { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrusterComponent, ComponentAdd>(OnAdd);
        SubscribeLocalEvent<ThrusterComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ThrusterComponent, EntParentChangedMessage>(OnParentChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _thrusters.Clear();
        _parents.Clear();
    }

    /// <summary>Thrusters whose direct parent is the given entity, or null when it has none.</summary>
    public IReadOnlyCollection<EntityUid>? ThrustersOn(EntityUid parent)
    {
        return _thrusters.GetValueOrDefault(parent);
    }

    private void OnAdd(Entity<ThrusterComponent> ent, ref ComponentAdd args)
    {
        Track(ent, Transform(ent).ParentUid);
    }

    private void OnRemove(Entity<ThrusterComponent> ent, ref ComponentRemove args)
    {
        Untrack(ent);
    }

    private void OnParentChanged(Entity<ThrusterComponent> ent, ref EntParentChangedMessage args)
    {
        Untrack(ent);
        Track(ent, args.Transform.ParentUid);
    }

    private void Track(EntityUid thruster, EntityUid parent)
    {
        if (!parent.IsValid())
            return;

        if (!_thrusters.TryGetValue(parent, out var set))
            _thrusters[parent] = set = new HashSet<EntityUid>();

        set.Add(thruster);
        _parents[thruster] = parent;
        Version++;
    }

    private void Untrack(EntityUid thruster)
    {
        if (!_parents.Remove(thruster, out var parent) || !_thrusters.TryGetValue(parent, out var set))
            return;

        set.Remove(thruster);
        if (set.Count == 0)
            _thrusters.Remove(parent);

        Version++;
    }
}
