using Content.Server.Power.Nodes;
using Content.Shared.NodeContainer;

namespace Content.Server._CE.Power;

/// <summary>
/// Grids that change level or z-network are refloaded together at the end of the tick, in one scan of the node
/// containers, instead of one whole-world scan per grid.
/// </summary>
public sealed partial class CEZCableVerticalSystem
{
    private readonly HashSet<EntityUid> _wgRefloodGrids = new();

    [Dependency] private EntityQuery<NodeContainerComponent> _wgNodeContainerQuery = default!;

    /// <summary>Queues a grid's vertical cables for the batched reflood.</summary>
    private void WGQueueReflood(EntityUid gridUid)
    {
        _wgRefloodGrids.Add(gridUid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_wgRefloodGrids.Count == 0)
            return;

        // Anchored cables are direct children of their grid, so walk the queued grids instead of every node container.
        foreach (var grid in _wgRefloodGrids)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!_wgNodeContainerQuery.TryComp(child, out var nc))
                    continue;

                foreach (var node in nc.Nodes.Values)
                {
                    if (node is CECableVerticalNode)
                        _nodeGroup.QueueReflood(node);
                }
            }
        }

        _wgRefloodGrids.Clear();
    }
}
