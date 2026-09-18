namespace LivingWorld.Simulation;
public sealed class WeatherSystem : ISimulationSystem
{
    public string Name=>"weather";
    public int Interval=>30;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var w=s.Weather;
        var r=s.Random.Stream("weather");
        var phase=(s.Clock.Now.DayOfYear-200)/365.2425f*MathF.Tau;
        w.SeasonalOffset=MathF.Cos(phase)*15;
        w.DaylightHours=12+MathF.Cos(phase)*4;
        w.Anomaly=Math.Clamp(w.Anomaly*.98f+r.Range(-.7f, .7f), -7, 7);
        w.Cloud=Math.Clamp(w.Cloud+r.Range(-.15f, .15f), 0, 1);
        w.Wind=Math.Clamp(w.Wind+r.Range(-.1f, .1f), 0, 1);
        w.Rain=Math.Max(0, (w.Cloud-.67f)*3);
        w.Humidity=.4f+w.Cloud*.5f;
        w.Sunlight=Math.Max(0, MathF.Cos((s.Clock.Hour-12)*MathF.PI/w.DaylightHours))*(1-w.Cloud*.6f);
        foreach (var t in s.Map.Tiles) t.Moisture=Math.Clamp(t.Moisture+w.Rain*.003f-w.Sunlight*.0006f, .08f, 1);
        return s.Map.Tiles.Length;
    }
}
