using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Salvage;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Salvage;

public sealed partial class RestrictedRangeSystem : SharedRestrictedRangeSystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    /// <summary>Metres of boundary each edge fixture covers.</summary>
    private const float BoundaryEdgeLength = 8f; // WOLFGATE(Planets)

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RestrictedRangeComponent, MapInitEvent>(OnRestrictedMapInit);
    }

    private void OnRestrictedMapInit(EntityUid uid, RestrictedRangeComponent component, MapInitEvent args)
    {
        component.BoundaryEntity = CreateBoundary(new EntityCoordinates(uid, component.Origin), component.Range);
    }

    public EntityUid CreateBoundary(EntityCoordinates coordinates, float range)
    {
        var boundaryUid = Spawn(null, coordinates);
        var boundaryPhysics = AddComp<PhysicsComponent>(boundaryUid);
        // WOLFGATE(Planets) START: one fixture per edge, not one chain fixture with many child edges
        // A body touching two of a chain's edges at once was paired with the one fixture twice in a tick: an engine
        // assert in debug builds and a duplicate-key throw in release. Planet bounds are big, so bodies hit it often.
        // var cShape = new ChainShape();
        // // Don't need it to be a perfect circle, just need it to be loosely accurate.
        // cShape.CreateLoop(Vector2.Zero, range + 0.25f, false, count: 4);
        // _fixtures.TryCreateFixture(
        //     boundaryUid,
        //     cShape,
        //     "boundary",
        //     collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable | CollisionGroup.LowImpassable),
        //     body: boundaryPhysics);
        var radius = range + 0.25f;
        var edges = Math.Max(16, (int) MathF.Ceiling(MathF.PI * 2f * radius / BoundaryEdgeLength));
        var step = MathF.PI * 2f / edges;
        for (var i = 0; i < edges; i++)
        {
            var from = new Vector2(MathF.Cos(step * i), MathF.Sin(step * i)) * radius;
            var to = new Vector2(MathF.Cos(step * (i + 1)), MathF.Sin(step * (i + 1))) * radius;
            _fixtures.TryCreateFixture(
                boundaryUid,
                new EdgeShape(from, to),
                $"boundary{i}",
                collisionLayer: (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable | CollisionGroup.LowImpassable),
                updates: false,
                body: boundaryPhysics);
        }

        _fixtures.FixtureUpdate(boundaryUid, body: boundaryPhysics);
        // WOLFGATE END
        _physics.WakeBody(boundaryUid, body: boundaryPhysics);
        AddComp<BoundaryComponent>(boundaryUid);
        return boundaryUid;
    }
}
