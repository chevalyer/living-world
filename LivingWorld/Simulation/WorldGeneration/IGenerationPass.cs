namespace LivingWorld.Simulation;
public interface IGenerationPass
{
    string Name
    { get; }
    void Apply(WorldState state, DefinitionCatalog definitions);
}
