namespace Content.Shared._WG.WolfWeather;

public abstract partial class SharedWolfWeatherSystem
{
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    /// <summary>
    /// The hull of the vehicle whose interior a body is in, if any. Such a body is on a private map but lives under the
    /// sky of the map the hull is on.
    /// </summary>
    public bool TryGetHull(EntityUid body, out EntityUid hull)
    {
        // -- LuaM: у нашей техники нет отдельной карты салона
        hull = default;
        return false;
    }

    /// <summary>The map whose sky a body is under: its own, or for someone inside a vehicle the hull's.</summary>
    public EntityUid? GetSkyMap(EntityUid body)
    {
        if (TryGetHull(body, out var hull))
            body = hull;

        return _xformQuery.TryComp(body, out var xform) ? xform.MapUid : null;
    }
}
