namespace LivingWorld.Simulation;
[Component("Memory")]
public sealed class MemoryComponent
{
    public List<Observation> Observations { get; set; } = [];
    public List<MemoryEvent> Events { get; set; } = [];
    public List<GridPoint> Visited { get; set; } = [];
}
