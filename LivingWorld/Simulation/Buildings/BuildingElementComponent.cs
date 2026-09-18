namespace LivingWorld.Simulation;
[Component("BuildingElement")]
public sealed class BuildingElementComponent
{
    public string Kind { get; set; } = "wall";
    public string Material { get; set; } = "wood";
    public int Project { get; set; }
    public float Durability { get; set; } = 100;
}
