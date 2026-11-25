using Content.Shared._WG.Planets.Forecast;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._WG.Planets.Forecast;

/// <summary>Opens the forecast window and passes a world pick back to the console.</summary>
[UsedImplicitly]
public sealed class WGWeatherForecastBoundUserInterface : BoundUserInterface
{
    private WGWeatherForecastWindow? _window;

    public WGWeatherForecastBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<WGWeatherForecastWindow>();
        _window.OnWorldSelected += world => SendMessage(new WGWeatherForecastSelectMessage(world));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is WGWeatherForecastState forecast)
            _window?.UpdateState(forecast);
    }
}
