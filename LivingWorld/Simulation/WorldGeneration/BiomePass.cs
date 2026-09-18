namespace LivingWorld.Simulation;
public sealed class BiomePass : IGenerationPass
{
    public string Name => "биомы";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        foreach (var t in state.Map.Tiles) t.Biome=t.Water==WaterKind.Ocean ? Biome.Ocean : t.Height<.31f ? Biome.Beach : t.Height>.80f ? Biome.Alpine : t.Height>.68f ? Biome.Mountain : t.Moisture>.78f && t.Height<.40f ? Biome.Marsh : t.Moisture>.57f && t.Fertility>.5f ? Biome.Forest : Biome.Meadow;
    }
}
