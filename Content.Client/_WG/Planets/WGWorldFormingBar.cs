using Content.Shared._WG.Planets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._WG.Planets;

/// <summary>
/// A world-forming progress bar: the lobby's follows the server's state by itself, hidden when nothing is forming;
/// the compact in-round one is filled by <see cref="WGWorldFormingClientSystem"/>.
/// </summary>
public sealed class WGWorldFormingBar : PanelContainer
{
    /// <summary>The width of the compact in-round bar.</summary>
    public const float HudWidth = 360f;

    private readonly bool _compact;
    private readonly Label _title;
    private readonly Label _percent;
    private readonly Label _note;
    private readonly ProgressBar _bar;
    private WGWorldFormingClientSystem? _system;

    public WGWorldFormingBar(bool compact)
    {
        _compact = compact;
        MouseFilter = MouseFilterMode.Ignore;
        Visible = false;
        HorizontalExpand = true;

        if (compact)
        {
            var panel = new StyleBoxFlat { BackgroundColor = Color.FromHex("#141A1FDD") };
            panel.SetContentMarginOverride(StyleBox.Margin.Horizontal, 10);
            panel.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
            PanelOverride = panel;
        }

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MouseFilter = MouseFilterMode.Ignore,
        };
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _title = new Label
        {
            ClipText = true,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            StyleClasses = { "LobbyHeroText" },
        };
        _percent = new Label
        {
            Align = Label.AlignMode.Right,
            MouseFilter = MouseFilterMode.Ignore,
            StyleClasses = { "LobbyHeroText" },
        };
        _bar = new ProgressBar
        {
            MinValue = 0f,
            MaxValue = 1f,
            MinHeight = compact ? 6 : 10,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _note = new Label
        {
            ClipText = true,
            Visible = false,
            MouseFilter = MouseFilterMode.Ignore,
            StyleClasses = { "LobbyHeroText" },
        };

        row.AddChild(_title);
        row.AddChild(_percent);
        column.AddChild(row);
        column.AddChild(_bar);
        column.AddChild(_note);
        AddChild(column);
    }

    /// <summary>The title line's text, for tests.</summary>
    public string Title => _title.Text ?? string.Empty;

    /// <summary>The bar's value, from 0 to 1.</summary>
    public float Value => _bar.Value;

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_compact)
            return;

        _system = IoCManager.Resolve<IEntityManager>().System<WGWorldFormingClientSystem>();
        _system.Changed += Show;
        Show(_system.State);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_system != null)
            _system.Changed -= Show;
        _system = null;
    }

    /// <summary>Shows the whole state: the world forming now, the overall percentage, and any hold on the round.</summary>
    public void Show(WGWorldFormingEvent state)
    {
        if (state.Worlds.Count == 0)
        {
            Visible = false;
            return;
        }

        // The first world not waiting is the one forming; the rest wait their turn.
        var current = state.Worlds[0];
        foreach (var world in state.Worlds)
        {
            if (world.Stage == WGWorldFormingStage.Waiting)
                continue;

            current = world;
            break;
        }

        _title.Text = Loc.GetString("wg-world-forming-title", ("name", current.Name), ("stage", StageName(current.Stage)));
        SetProgress(state.Overall);

        var notes = new List<string>();
        if (state.Worlds.Count > 1)
            notes.Add(Loc.GetString("wg-world-forming-count", ("count", state.Worlds.Count)));
        if (state.RoundHeld)
            notes.Add(Loc.GetString("wg-world-forming-held"));
        _note.Text = string.Join(" ", notes);
        _note.Visible = notes.Count > 0;
        Visible = true;
    }

    /// <summary>Shows one world, for the in-round bar.</summary>
    public void ShowWorld(WGWorldFormingEntry world)
    {
        _title.Text = Loc.GetString("wg-world-forming-hud", ("name", world.Name), ("stage", StageName(world.Stage)));
        SetProgress(world.Progress);
    }

    private void SetProgress(float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        _bar.Value = progress;
        _percent.Text = Loc.GetString("wg-world-forming-percent", ("percent", (int) MathF.Floor(progress * 100f)));
    }

    private static string StageName(WGWorldFormingStage stage)
    {
        return Loc.GetString($"wg-world-forming-stage-{stage.ToString().ToLowerInvariant()}");
    }
}
