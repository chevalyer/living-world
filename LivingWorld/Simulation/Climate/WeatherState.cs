namespace LivingWorld.Simulation;
public sealed class WeatherState
{
    public float SeasonalOffset { get; set; }
    public float Anomaly { get; set; }
    public float Wind { get; set; } = .2f;
    public float Rain { get; set; }
    public float Cloud { get; set; } = .3f;
    public float Humidity { get; set; } = .6f;
    public float DaylightHours { get; set; } = 12;
    public float Sunlight { get; set; } = .7f;
}
