namespace LivingWorld.Simulation;

public sealed class VegetationPass : IGenerationPass
{
    public string Name => "растительность";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var distribution = state.Random.Stream("vegetation.distribution");
        for (var i = 0; i < state.Map.Tiles.Length; i++)
        {
            var tile = state.Map.Tiles[i];
            if (tile.Water != WaterKind.None || tile.Biome is Biome.Alpine or Biome.Beach) continue;
            var roll = distribution.NextDouble();
            string? kind = null;
            if (tile.Biome == Biome.Forest && roll < .16) kind = "tree";
            else
            {
                if (tile.Biome == Biome.Forest) roll = (roll - .16) / .84;
                kind = roll < .105 ? "bush" : roll < .14 ? "grass" : roll < .17 ? "wild_crop" : roll < .205 ? "flower" : null;
            }
            if (kind is null) continue;
            var candidates = definitions.Plants.Values
                .Where(d => d.Kind == kind && d.SpawnWeight > 0 && tile.Moisture >= d.MinMoisture)
                .Where(d => tile.BaseTemperature > d.FrostTolerance && tile.BaseTemperature < d.MaxTemperature)
                .OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0) continue;
            var random = state.Random.Stream("vegetation.species." + kind);
            float Weight(PlantDefinition d) => d.SpawnWeight / (1 + Math.Abs(tile.BaseTemperature - d.OptimalTemperature) * .1f);
            var selection = random.Range(0, candidates.Sum(Weight));
            var definition = candidates[^1];
            foreach (var candidate in candidates)
            {
                selection -= Weight(candidate);
                if (selection <= 0) { definition = candidate; break; }
            }
            var id = state.Entities.Create();
            state.Entities.Set(id, new PositionComponent { Tile = state.Map.Point(i) });
            state.Entities.Set(id, new PlantComponent
            {
                Definition = definition.Id,
                Growth = random.Range(.65f, 1f),
                Yield = definition.Yield * random.Range(.6f, 1f),
                AgeDays = random.Range(10f, 100f)
            });
        }
    }
}
