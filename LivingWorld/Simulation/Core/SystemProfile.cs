namespace LivingWorld.Simulation;
public sealed class SystemProfile
{
    public string Name { get; set; } = "";
    public double LastMilliseconds { get; set; }
    public double AverageMilliseconds { get; set; }
    public double MaxMilliseconds { get; set; }
    public long Calls { get; set; }
    public int Entities { get; set; }
}
