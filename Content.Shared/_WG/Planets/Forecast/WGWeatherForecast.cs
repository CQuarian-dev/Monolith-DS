using Robust.Shared.Serialization;

namespace Content.Shared._WG.Planets.Forecast;

/// <summary>A console that shows a sector world's season, weather and what is coming.</summary>
[RegisterComponent]
public sealed partial class WGWeatherForecastConsoleComponent : Component
{
    /// <summary>The world picked on this console; null shows the one it stands on, else the first.</summary>
    [ViewVariables] public NetEntity? Selected;
}

[Serializable, NetSerializable]
public enum WGWeatherForecastUiKey : byte
{
    Key,
}

/// <summary>How sure the forecast is of a row.</summary>
[Serializable, NetSerializable]
public enum WGForecastCertainty : byte
{
    Exact,
    Likely,
    Possible,
}

/// <summary>One stretch of forecast weather.</summary>
[Serializable, NetSerializable]
public sealed class WGForecastRow
{
    /// <summary>Real seconds from now the stretch begins; zero for the weather now.</summary>
    public float InSeconds;
    /// <summary>The world's local minute of the day it begins.</summary>
    public int MinuteOfDay;
    /// <summary>The stock weather id, or empty for clear skies.</summary>
    public string Weather = string.Empty;
    public WGForecastCertainty Certainty;
    public bool Wind;
    public bool Lightning;
    /// <summary>Forecast outdoor temperature, whole degrees Celsius.</summary>
    public int Celsius;
}

/// <summary>A world the console can show.</summary>
[Serializable, NetSerializable]
public sealed class WGForecastWorld
{
    public NetEntity World;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class WGWeatherForecastState : BoundUserInterfaceState
{
    public List<WGForecastWorld> Worlds = new();
    public NetEntity? Selected;
    /// <summary>The season's Fluent name, or null for a world without seasons.</summary>
    public string? Season;
    public Color SeasonColor = Color.White;
    /// <summary>False while the world's weather cycle is off: nothing is coming.</summary>
    public bool Cycling;
    /// <summary>The weather now first, then what follows.</summary>
    public List<WGForecastRow> Rows = new();
}

/// <summary>Picks the world a forecast console shows.</summary>
[Serializable, NetSerializable]
public sealed class WGWeatherForecastSelectMessage : BoundUserInterfaceMessage
{
    public NetEntity World;

    public WGWeatherForecastSelectMessage(NetEntity world)
    {
        World = world;
    }
}
