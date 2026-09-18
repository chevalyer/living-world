namespace LivingWorld.Simulation;
[Component("Thermal")]
public sealed class ThermalComponent
{
    public float Temperature { get; set; } = 37;
    public float HeatCapacity { get; set; } = 60;
    public float Metabolism { get; set; } = 1;
}
