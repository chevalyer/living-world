namespace LivingWorld.Simulation;
public sealed class WorldGenerator
{
    private readonly IGenerationPass[] _passes=[new TerrainPass(), new GeologyPass(), new HydrologyPass(), new ClimatePass(), new BiomePass(), new VegetationPass(), new ResourcePass()];
    public WorldState Generate(DefinitionCatalog defs, int seed=1847, int size=128, int population=14, Action<string>? progress=null)
    {
        if (size<48 || size>512 || population<1 || population>500) throw new ArgumentOutOfRangeException(nameof(size), "Map: 48..512, population: 1..500");
        defs.Validate();
        var state=new WorldState
        {
            Seed=seed, Map=new(size, size), Random=new(seed)
        };
        foreach (var pass in _passes)
        {
            progress?.Invoke(pass.Name);
            pass.Apply(state, defs);
        }
        state.Start=ChooseStart(state);
        var candidates=Reachable(state.Map, state.Start, Math.Max(80, population*5));
        var factory=new NpcFactory(defs);
        for(var i=0;i<population;i++)factory.SpawnAdult(state,candidates[i%candidates.Count]);
        EnsureProductionKnowledge(state,defs);
        state.Log($"Новый мир. Сид {seed}. Поселенцев: {population}.");
        return state;
    }
    private static void EnsureProductionKnowledge(WorldState state,DefinitionCatalog definitions)
    {
        var founders=state.Entities.Store<IdentityComponent>().Ids()
            .Where(id=>state.Clock.Age(state.Entities.Get<IdentityComponent>(id).BirthDate)>=18)
            .OrderBy(id=>id).ToArray();
        if(founders.Length==0)return;
        var required=definitions.Recipes.Values.Select(x=>x.Knowledge)
            .Concat(definitions.Facilities.Values.Select(x=>x.Knowledge))
            .Where(x=>!string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var cursor=0;
        foreach(var fact in required)
        {
            if(founders.Any(id=>state.Entities.Get<KnowledgeComponent>(id).Facts.Contains(fact)))continue;
            state.Entities.Get<KnowledgeComponent>(founders[cursor%founders.Length]).Facts.Add(fact);
            cursor++;
        }
    }

    private static GridPoint ChooseStart(WorldState state)
    {
        var map=state.Map;
        var best=float.NegativeInfinity;
        var result=new GridPoint(map.Width/2, map.Height/2);
        var plants=state.Entities.Store<PlantComponent>().All.Where(x=>x.Value.Definition.Contains("bush", StringComparison.Ordinal)).Select(x=>state.Entities.Get<PositionComponent>(x.Key).Tile).ToArray();
        for (var y=8; y<map.Height-8; y+=2)for (var x=8; x<map.Width-8; x+=2)
        {
            var p=new GridPoint(x, y);
            if (!map.Walkable(p))continue;
            var open=0;
            var water=false;
            for (var dy=-4; dy<=4; dy++)for (var dx=-4; dx<=4; dx++)
            {
                var n=new GridPoint(x+dx, y+dy);
                if (map.Walkable(n))open++;
                if (map[n].Water is WaterKind.River or WaterKind.Lake)water=true;
            }
            var score=open+plants.Count(q=>q.Distance(p)<12)*2+(water?35:0)-p.Distance(new(map.Width/2, map.Height/2))*.1f;
            if (score>best)
            {
                best=score;
                result=p;
            }
        }
        if (best==float.NegativeInfinity)throw new InvalidOperationException("Seed has no suitable land.");
        return result;
    }
    private static List<GridPoint> Reachable(WorldMap map, GridPoint start, int limit)
    {
        var result=new List<GridPoint>();
        var q=new Queue<GridPoint>();
        var seen=new HashSet<GridPoint>
        {
            start
        };
        q.Enqueue(start);
        while (q.TryDequeue(out var p) && result.Count<limit)
        {
            result.Add(p);
            foreach (var n in map.Neighbors(p))if (map.Walkable(n)&&seen.Add(n))q.Enqueue(n);
        }
        return result;
    }
}
