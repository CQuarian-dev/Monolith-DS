using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Movement;

public sealed partial class FastGridLerpSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;

    private const float EngineLimitSquared = 2f * 2f;
    private const float MaxDistanceSquared = 64f * 64f;

    private readonly Dictionary<EntityUid, (Vector2 From, Vector2 To, bool Done)> _lerps = new();
    private readonly List<EntityUid> _stale = new();
    private GameTick _lastTick;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(TransformSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _lerps.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var newTick = _timing.CurTick != _lastTick;
        _lastTick = _timing.CurTick;
        var step = (float) (_timing.TickRemainder.TotalSeconds / _timing.TickPeriod.TotalSeconds);

        _stale.Clear();
        foreach (var uid in _lerps.Keys)
        {
            if (!HasComp<MapGridComponent>(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _lerps.Remove(uid);
        }

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.NextPosition is not { } to
                || xform.LerpParent != xform.ParentUid
                || !xform.ParentUid.IsValid())
            {
                _lerps.Remove(uid);
                continue;
            }

            var from = xform.PrevPosition;
            var distance = (to - from).LengthSquared();
            if (distance < EngineLimitSquared || distance > MaxDistanceSquared)
            {
                _lerps.Remove(uid);
                continue;
            }

            if (_lerps.TryGetValue(uid, out var lerp) && lerp.From == from && lerp.To == to)
            {
                if (lerp.Done)
                    continue;

                if (newTick)
                {
                    _transform.SetLocalPositionNoLerp(uid, to, xform);
                    _lerps[uid] = (from, to, true);
                    continue;
                }
            }
            else
            {
                _lerps[uid] = (from, to, false);
            }

            _transform.SetLocalPositionNoLerp(uid, Vector2.Lerp(from, to, step), xform);
        }
    }
}
