namespace LivingWorld.Simulation;
public sealed class SimulationClock
{
    public DateTime Epoch { get; set; } = new(1372, 4, 1, 7, 0, 0, DateTimeKind.Unspecified);
    public long Tick { get; set; }
    public DateTime Now => Epoch.AddMinutes(Tick);
    public double Days => Tick / 1440.0;
    public float Hour => Now.Hour + Now.Minute / 60f;
    public string Season => Now.Month is 12 or 1 or 2 ? "зима" : Now.Month < 6 ? "весна" : Now.Month < 9 ? "лето" : "осень";
    public void Advance() => Tick++;
    public int Age(DateTime birth)
    {
        var years = Now.Year - birth.Year;
        if (birth.AddYears(years) > Now) years--;
        return Math.Max(0, years);
    }
}
