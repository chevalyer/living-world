namespace LivingWorld.Simulation;
[Component("Knowledge")]
public sealed class KnowledgeComponent
{
    public SortedSet<string> Facts { get; set; } = new(StringComparer.Ordinal);
    public SortedSet<string> EdiblePlants { get; set; } = new(StringComparer.Ordinal);
}
