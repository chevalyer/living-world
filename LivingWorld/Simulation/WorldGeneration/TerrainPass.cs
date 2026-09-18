namespace LivingWorld.Simulation;
public sealed class TerrainPass : IGenerationPass
{
    public string Name => "рельеф";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var map=state.Map;
        var seed=RandomService.Hash(state.Seed, "terrain");
        for (var i=0; i<map.Tiles.Length; i++)
        {
            var p=map.Point(i);
            var nx=p.X/(float)(map.Width-1);
            var ny=p.Y/(float)(map.Height-1);
            var island=1-MathF.Pow(Math.Max(Math.Abs(nx-.5f)*2, Math.Abs(ny-.5f)*2), 4);
            var noise=ValueNoise.Fractal(nx*4.7f, ny*4.7f, seed);
            var ridge=1-Math.Abs(ValueNoise.Sample(nx*3.3f, ny*3.3f, seed+91)*2-1);
            var h=Math.Clamp(noise*.68f+ridge*.24f+island*.30f-.18f, 0, 1);
            var t=map.Tiles[i];
            t.Height=h;
            t.DrainageHeight=h;
            if (h<.29f)
            {
                t.Water=WaterKind.Ocean;
                t.Biome=Biome.Ocean;
            }
        }
    }
}
