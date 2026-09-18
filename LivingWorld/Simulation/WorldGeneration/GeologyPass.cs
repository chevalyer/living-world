namespace LivingWorld.Simulation;
public sealed class GeologyPass : IGenerationPass
{
    public string Name => "геология";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var seed=RandomService.Hash(state.Seed, "geology");
        for (var i=0; i<state.Map.Tiles.Length; i++)
        {
            var p=state.Map.Point(i);
            var t=state.Map.Tiles[i];
            t.Ore=ValueNoise.Fractal(p.X*.09f, p.Y*.09f, seed);
            t.Fertility=Math.Clamp(1-t.Height*.65f+ValueNoise.Sample(p.X*.06f, p.Y*.06f, seed+7)*.3f-.2f, 0, 1);
        }
    }
}
