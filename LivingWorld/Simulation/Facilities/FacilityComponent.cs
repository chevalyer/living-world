namespace LivingWorld.Simulation;
[Component("Facility")]
public sealed class FacilityComponent
{
    public string Definition { get; set; } = "";
    public int Project { get; set; }
    public int Builder { get; set; }
    public float Durability { get; set; } = 100;
}
