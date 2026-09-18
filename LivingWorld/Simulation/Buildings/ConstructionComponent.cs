namespace LivingWorld.Simulation;
[Component("Construction")]
public sealed class ConstructionComponent
{
    public string Definition { get; set; } = "wooden_cabin";
    public List<PlannedElement> Elements { get; set; } = [];
    public List<int> Contributors { get; set; } = [];
    public int Completed { get; set; }
    public bool Finished => Completed >= Elements.Count;
}
