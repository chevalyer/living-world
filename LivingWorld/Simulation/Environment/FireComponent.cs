namespace LivingWorld.Simulation;
[Component("Fire")]
public sealed class FireComponent
{
    public float FuelMinutes { get; set; } = 360;
    public float Heat { get; set; } = 18;
}
