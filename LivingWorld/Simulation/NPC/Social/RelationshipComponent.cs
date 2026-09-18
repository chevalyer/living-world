namespace LivingWorld.Simulation;
[Component("Relationship")]
public sealed class RelationshipComponent
{
    public Dictionary<int, Relationship> People { get; set; } = [];
}
