using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud; // LuaM
using Content.Client.Stylesheets; // LuaM
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input; // LuaM

namespace Content.Client._WG.Planets;

/// <summary>A compact, mouse-transparent readout created once and updated only when its text changes.</summary>
public sealed class WGPlanetTimepieceHud : PanelContainer
{
    /// <summary>Narrowest the panel gets; it widens to fit its longest line, up to <see cref="MaxPanelWidth"/>.</summary>
    public const float PanelWidth = 164f;
    public const float MaxPanelWidth = 240f;
    public const float PanelHeight = 98f;

    private readonly Label _planet;
    private readonly Label _time;
    private readonly Label _weather;
    private readonly Label _climate;
    private readonly Label _here;
    private readonly Label _feel;

    public bool GaugeVisible => false; // LuaM: модуль Warmth не переносим, шкалы тепла тела нет
    public string PlanetText => _planet.Text ?? string.Empty;
    public string TimeText => _time.Text ?? string.Empty;
    public string WeatherText => _weather.Text ?? string.Empty;
    public string ClimateText => _climate.Text ?? string.Empty;
    public string HereText => _here.Text ?? string.Empty;
    public string FeelText => _feel.Text ?? string.Empty;

    public WGPlanetTimepieceHud(IResourceCache resources)
    {
        MinSize = new Vector2(PanelWidth, PanelHeight);
        MaxWidth = MaxPanelWidth;
        // LuaM-start: dragged by the mouse, styled as the lobby HUD
        MouseFilter = Control.MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Move;

        var panel = HudPalette.Box(HudPalette.Panel.WithAlpha(0.92f), HudPalette.Border, HudPalette.Fg);
        panel.SetContentMarginOverride(StyleBox.Margin.Horizontal, 10);
        panel.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        PanelOverride = panel;

        var regular = LobbyHudStyles.UiFont(resources, 10);
        var clock = LobbyHudStyles.MonoFont(resources, 20, semi: true);
        // LuaM-end

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        AddChild(column);

        _planet = MakeLabel(regular, HudPalette.FgLabel); // LuaM
        _time = MakeLabel(clock, HudPalette.Fg); // LuaM
        _weather = MakeLabel(regular, HudPalette.TextDim); // LuaM
        _climate = MakeLabel(regular, HudPalette.Text); // LuaM

        column.AddChild(_planet);
        column.AddChild(_time);
        column.AddChild(_weather);
        column.AddChild(_climate);

        // The air on the wearer's own tile and how warm their body feels, the word coloured by danger.
        _here = MakeLabel(regular, Color.FromHex("#A8C8BE"));
        _here.HorizontalAlignment = HAlignment.Right;
        _here.HorizontalExpand = true;
        _feel = MakeLabel(regular, Color.FromHex("#8FD694"));
        _feel.HorizontalAlignment = HAlignment.Left;
        _feel.HorizontalExpand = true;
        var thermal = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        thermal.AddChild(_here);
        thermal.AddChild(_feel);
        column.AddChild(thermal);

    }

    /// <summary>The thermal row: the tile's air temperature and the body feel word in its danger colour.</summary>
    public void SetThermal(string here, string feel, Color feelColor)
    {
        if (_here.Text != here)
            _here.Text = here;
        if (_feel.Text != feel)
            _feel.Text = feel;
        _feel.FontColorOverride = feelColor;
        _feel.Visible = feel.Length > 0;
    }

    public void SetReadout(string planet, string time, string weather, string climate = "", Color? climateColor = null)
    {
        if (_climate.Text != climate)
            _climate.Text = climate;
        _climate.FontColorOverride = climateColor ?? HudPalette.Text; // LuaM
        if (_planet.Text != planet)
            _planet.Text = planet;
        if (_time.Text != time)
            _time.Text = time;
        if (_weather.Text != weather)
            _weather.Text = weather;
    }

    // LuaM-start: the player drags the readout where it doesn't cover the view; the system keeps the spot
    public event Action<Vector2>? Dragged;
    public bool Dragging { get; private set; }
    private Vector2 _grab;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Dragging = true;
        _grab = args.PointerLocation.Position / UIScale - Position;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !Dragging)
            return;

        Dragging = false;
        Dragged?.Invoke(Position);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!Dragging || Parent == null)
            return;

        var pos = Vector2.Clamp(args.GlobalPosition - _grab, Vector2.Zero, Vector2.Max(Vector2.Zero, Parent.Size - Size));
        LayoutContainer.SetPosition(this, pos);
    }
    // LuaM-end

    private static Label MakeLabel(Font font, Color color)
    {
        return new Label
        {
            FontOverride = font,
            FontColorOverride = color,
            HorizontalAlignment = HAlignment.Stretch,
            Align = Label.AlignMode.Center,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
    }
}
