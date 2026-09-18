namespace LivingWorld.Simulation;
[Component("Resource")]
public sealed class ResourceComponent
{
    public string Product { get; set; } = "granite";
    public int Units { get; set; } = 12;
    public float Hardness { get; set; } = 1;
}
