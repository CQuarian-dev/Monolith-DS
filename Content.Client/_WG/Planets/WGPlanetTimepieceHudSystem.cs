using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared._WG.Planets;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;

namespace Content.Client._WG.Planets;

/// <summary>Shows the planet's local time and weather to anyone standing on a planet.</summary>
public sealed partial class WGPlanetTimepieceHudSystem : EntitySystem
{
    private const float RefreshInterval = 0.25f;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private Robust.Shared.Configuration.IConfigurationManager _luamCfg = default!; // LuaM

    private WGPlanetTimepieceHud _hud = default!;
    private float _untilRefresh;
    private bool _active;

    public bool HudVisible => _hud.Visible;
    public string DisplayedPlanet => _hud.PlanetText;
    public string DisplayedTime => _hud.TimeText;
    public string DisplayedWeather => _hud.WeatherText;
    public string DisplayedClimate => _hud.ClimateText;
    public string DisplayedHere => _hud.HereText;
    public string DisplayedFeel => _hud.FeelText;
    public float HudWidth => MathF.Max(_hud.Width, WGPlanetTimepieceHud.PanelWidth);
    public Control Hud => _hud;

    public override void Initialize()
    {
        UpdatesOutsidePrediction = true;
        base.Initialize();

        _hud = new WGPlanetTimepieceHud(_resources)
        {
            Visible = false,
        };
        _ui.PopupRoot.AddChild(_hud);
        LayoutContainer.SetAnchorPreset(_hud, LayoutContainer.LayoutPreset.TopLeft);
        _hud.Dragged += pos => _luamCfg.SetCVar(Content.Shared._LuaM.CCVar.LuaMCVars.PlanetClockPosition, $"{pos.X:0},{pos.Y:0}"); // LuaM

        SubscribeLocalEvent<WGPlanetEnvironmentComponent, AfterAutoHandleStateEvent>(OnEnvironmentState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        _hud.Orphan();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _untilRefresh -= frameTime;
        if (_untilRefresh > 0f)
            return;

        _untilRefresh = RefreshInterval;
        RefreshLocalVisibility();
        ApplyVisibility();
        if (!_active)
            return;
        PositionHud();
        if (_player.LocalEntity is not { } player || !IsOnPlanet(player))
        {
            SetActive(false);
            return;
        }

        RefreshReadout();
    }

    private void OnEnvironmentState(Entity<WGPlanetEnvironmentComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_active ||
            _player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? xform) ||
            xform.MapUid != ent.Owner)
        {
            return;
        }

        RefreshReadout();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RefreshVisibility(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        SetActive(false);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        SetActive(false);
    }

    private void RefreshLocalVisibility()
    {
        if (_player.LocalEntity is { } player)
            RefreshVisibility(player);
        else
            SetActive(false);
    }

    private void RefreshVisibility(EntityUid player)
    {
        if (player != _player.LocalEntity)
            return;

        SetActive(IsOnPlanet(player));
    }

    private bool IsOnPlanet(EntityUid player)
    {
        return TryComp(player, out TransformComponent? xform) && HasComp<WGPlanetEnvironmentComponent>(xform.MapUid);
    }

    private void SetActive(bool active)
    {
        if (_active == active)
            return;
        _active = active;
        ApplyVisibility();
        _untilRefresh = 0f;

        if (active)
            RefreshReadout();
    }

    /// <summary>Shows the panel on a planet, except under the cockpit, which has no HUD corner for it.</summary>
    private void ApplyVisibility()
    {
        _hud.Visible = _active; // LuaM: без кабины Cockpit Wolfgate
    }

    private void PositionHud()
    {
        var size = new Vector2(
            MathF.Max(_hud.Width, WGPlanetTimepieceHud.PanelWidth),
            MathF.Max(_hud.Height, WGPlanetTimepieceHud.PanelHeight));
        // LuaM-start: a spot the player dragged it to wins over the default corner
        if (_hud.Dragging)
            return;

        var saved = _luamCfg.GetCVar(Content.Shared._LuaM.CCVar.LuaMCVars.PlanetClockPosition).Split(',');
        if (saved.Length == 2 && float.TryParse(saved[0], System.Globalization.CultureInfo.InvariantCulture, out var x)
            && float.TryParse(saved[1], System.Globalization.CultureInfo.InvariantCulture, out var y))
        {
            var room = Vector2.Max(Vector2.Zero, _ui.PopupRoot.Size - size);
            LayoutContainer.SetPosition(_hud, Vector2.Clamp(new Vector2(x, y), Vector2.Zero, room));
            return;
        }
        // LuaM-end
        LayoutContainer.SetPosition(_hud, WGHudPlacement.Place(_ui, size));
    }

    private void RefreshReadout()
    {
        if (!_active ||
            _player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? xform) ||
            xform.MapUid is not { } map ||
            !TryComp<WGPlanetEnvironmentComponent>(map, out var environment))
        {
            _hud.SetReadout(Loc.GetString("wg-planet-timepiece-no-signal"), "--:--", string.Empty);
            _hud.SetThermal(string.Empty, string.Empty, Color.White);
            return;
        }

        _hud.SetThermal(string.Empty, string.Empty, Color.White); // LuaM: строка тепла тела из модуля Warmth не переносится

        var hours = environment.MinuteOfDay / 60;
        var minutes = environment.MinuteOfDay % 60;
        _hud.SetReadout(environment.PlanetName, $"{hours:00}:{minutes:00}", environment.Weather,
            ClimateText(environment), environment.Season != null ? environment.SeasonColor : null);
    }

    /// <summary>The season and outdoor temperature line; either half is left out when the world has none.</summary>
    public string ClimateText(WGPlanetEnvironmentComponent environment)
    {
        var temperature = environment.TemperatureCelsius is { } celsius
            ? Loc.GetString("wg-planet-temperature-outside", ("celsius", celsius))
            : null;
        var season = environment.Season is { } name ? Loc.GetString(name) : null;
        if (season != null && temperature != null)
            return Loc.GetString("wg-planet-timepiece-climate", ("season", season), ("temperature", temperature));
        return season ?? temperature ?? string.Empty;
    }
}
