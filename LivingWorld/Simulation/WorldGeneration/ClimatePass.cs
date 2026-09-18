namespace LivingWorld.Simulation;
public sealed class ClimatePass : IGenerationPass
{
    public string Name => "климат и почва";
    public void Apply(WorldState state, DefinitionCatalog definitions)
    {
        var map=state.Map;
        var distance=Enumerable.Repeat(int.MaxValue, map.Tiles.Length).ToArray();
        var q=new Queue<int>();
        for (var i=0; i<map.Tiles.Length; i++) if (map.Tiles[i].Water!=WaterKind.None)
        {
            distance[i]=0;
            q.Enqueue(i);
        }
        while (q.TryDequeue(out var i)) foreach (var p in map.Neighbors(map.Point(i)))
        {
            var n=map.Index(p);
            if (distance[n]>distance[i]+1)
            {
                distance[n]=distance[i]+1;
                q.Enqueue(n);
            }
        }
        var seed=RandomService.Hash(state.Seed, "climate");
        for (var i=0; i<map.Tiles.Length; i++)
        {
            var p=map.Point(i);
            var t=map.Tiles[i];
            t.Moisture=Math.Clamp(.23f+ValueNoise.Fractal(p.X*.045f, p.Y*.045f, seed)*.5f+.3f/(1+distance[i]*.2f), 0, 1);
            t.BaseTemperature=19-t.Height*13+(p.Y/(float)map.Height-.5f)*5;
        }
    }
}
