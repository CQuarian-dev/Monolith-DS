using System.Numerics;
using Content.Client._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._WG.Planets;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    /// <summary>How far past the plot's corners the world's disc reaches, as a share of its diagonal.</summary>
    private const float WgPlanetFill = 0.6f;

    private PlanetOverlay? _wgPlanets;
    private ShaderInstance? _wgPlanetShader;
    private Planet? _wgPlanet;
    private EntityUid? _wgPlanetBody;

    /// <summary>
    /// Over a world's layers, the plot's backdrop is the world itself, as the sector draws it, close enough to fill the
    /// plot. False anywhere else, so the space background is drawn instead.
    /// </summary>
    private bool DrawWgPlanetBackground(DrawingHandleScreen handle, EntityUid map)
    {
        if (!WgEnsurePlanetShader(map) || _wgPlanetShader is not { } shader || _wgPlanet is not { } planet)
            return false;

        // The shader places the body in world space: hand it a frame round the body, the plot's shape, with the
        // body's disc just past the corners.
        var size = new Vector2(PixelWidth, PixelHeight) / MathF.Max(PixelWidth, PixelHeight);
        shader.SetParameter("planetRadius", size.Length() * WgPlanetFill);
        shader.SetParameter("parallaxCenter", planet.Position);
        shader.SetParameter("viewportMin", planet.Position - size / 2f);
        shader.SetParameter("viewportSize", size);

        var box = new UIBox2(Vector2.Zero, PixelSize);
        handle.DrawRect(box, Color.Black);
        handle.UseShader(shader);
        handle.DrawRect(box, Color.White);
        handle.UseShader(null);
        return true;
    }

    /// <summary>Finds the sector body a planet layer belongs to and builds its shader, once per body.</summary>
    private bool WgEnsurePlanetShader(EntityUid map)
    {
        var network = EntManager.TryGetComponent<WGOrbitLayerComponent>(map, out var orbit) ? orbit.Network
            : EntManager.TryGetComponent<WGPlanetLayerComponent>(map, out var layer) ? layer.Network
            : null;
        if (network == null)
            return false;

        EntityUid? body = null;
        var query = EntManager.AllEntityQueryEnumerator<WGSectorPlanetComponent>();
        while (query.MoveNext(out var uid, out var sector))
        {
            if (sector.Network != network)
                continue;

            body = uid;
            break;
        }

        if (body == null)
            return false;

        if (body == _wgPlanetBody)
            return _wgPlanetShader != null;

        _wgPlanetBody = body;
        _wgPlanetShader = null;
        _wgPlanet = null;

        var xform = EntManager.GetComponent<TransformComponent>(body.Value);
        if (!EntManager.TryGetComponent<StarSystemMapComponent>(xform.MapUid, out var system) || system.StarSystem is not { } stars)
            return false;

        foreach (var planet in stars.Planets)
        {
            if ((planet.Position - xform.LocalPosition).LengthSquared() < 1f)
                _wgPlanet = planet;
        }

        if (_wgPlanet == null)
            return false;

        _wgPlanets ??= new PlanetOverlay(EntManager, IoCManager.Resolve<IPrototypeManager>());
        _wgPlanetShader = _wgPlanets.SetupPlanetShader(_wgPlanet, stars.Star);
        return _wgPlanetShader != null;
    }
}
