namespace LivingWorld.Simulation;

public readonly record struct SettlementArea(int X,int Y)
{
    public int MinX=>X;
    public int MinY=>Y;
    public int MaxX=>X+SettlementAnalyzer.SettlementCellSize-1;
    public int MaxY=>Y+SettlementAnalyzer.SettlementCellSize-1;
    public bool Contains(GridPoint point)=>point.X>=MinX&&point.X<=MaxX&&point.Y>=MinY&&point.Y<=MaxY;
}

public sealed record SettlementSummary(
    int Anchor,
    string Name,
    GridPoint Center,
    SettlementArea[] Areas,
    int Homes,
    int Members,
    int Families,
    float FoodCalories,
    int Projects,
    string[] Specializations)
{
    public int MinX=>Areas.Length==0?Center.X:Areas.Min(x=>x.MinX);
    public int MinY=>Areas.Length==0?Center.Y:Areas.Min(x=>x.MinY);
    public int MaxX=>Areas.Length==0?Center.X:Areas.Max(x=>x.MaxX);
    public int MaxY=>Areas.Length==0?Center.Y:Areas.Max(x=>x.MaxY);
    public bool Contains(GridPoint point)=>Areas.Any(area=>area.Contains(point));
}

public static class SettlementAnalyzer
{
    public const int HomeLinkDistance = 18;
    public const int ResidentReach = 28;
    public const int SettlementCellSize = 7;
    private const int HomePadding = 1;

    public static IReadOnlyList<SettlementSummary> DescribeAll(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var homes=e.Store<ConstructionComponent>().All
            .Where(x=>x.Value.Finished&&e.Has<PositionComponent>(x.Key))
            .Select(x=>new Home(x.Key,e.Get<PositionComponent>(x.Key).Tile,x.Value.Definition,x.Value))
            .OrderBy(x=>x.Id)
            .ToArray();
        if (homes.Length==0)return Array.Empty<SettlementSummary>();

        var clusters=Clusters(homes);
        var clusterByHome=new Dictionary<int,int>();
        for (var index=0;index<clusters.Count;index++)
            foreach (var home in clusters[index])clusterByHome[home.Id]=index;

        var membersByCluster=Enumerable.Range(0,clusters.Count).Select(_=>new List<int>()).ToArray();
        foreach (var id in LivingPeople(session))
        {
            var family=e.Get<FamilyComponent>(id);
            if (family.HomeProject!=0)
            {
                if (clusterByHome.TryGetValue(family.HomeProject,out var homeCluster))
                    membersByCluster[homeCluster].Add(id);
                continue;
            }

            var position=e.Get<PositionComponent>(id).Tile;
            var nearest=clusters
                .Select((cluster,index)=>new
                {
                    Index=index,
                    Distance=cluster.Min(home=>home.Position.Distance(position)),
                    Anchor=cluster.Min(home=>home.Id)
                })
                .OrderBy(x=>x.Distance)
                .ThenBy(x=>x.Anchor)
                .First();
            if (nearest.Distance<=ResidentReach)membersByCluster[nearest.Index].Add(id);
        }

        var result=new List<SettlementSummary>(clusters.Count);
        for (var clusterIndex=0;clusterIndex<clusters.Count;clusterIndex++)
        {
            var cluster=clusters[clusterIndex];
            var anchor=cluster.Min(x=>x.Id);
            var center=new GridPoint(
                (int)Math.Round(cluster.Average(x=>x.Position.X)),
                (int)Math.Round(cluster.Average(x=>x.Position.Y)));

            var areas=AreasFor(cluster,session.Definitions,s.Map);

            var members=membersByCluster[clusterIndex].Distinct().OrderBy(id=>id).ToArray();
            var memberSet=members.ToHashSet();

            var families=new HashSet<int>();
            foreach (var id in members)
            {
                var family=e.Get<FamilyComponent>(id);
                if (family.Partner!=0&&memberSet.Contains(family.Partner))
                    families.Add(Math.Min(id,family.Partner));
                else if (family.Children.Any(memberSet.Contains))
                    families.Add(id);
            }

            bool Inside(GridPoint p)=>areas.Any(area=>area.Contains(p));
            var food=0f;
            foreach (var (itemId,item) in e.Store<ItemComponent>().All)
            {
                var definition=session.Definitions.Items[item.Definition];
                if (definition.Calories<=0||item.Freshness<=0)continue;
                var local=item.Holder!=0&&memberSet.Contains(item.Holder);
                if (!local&&item.Holder!=0&&e.Try<PositionComponent>(item.Holder) is { } holderPosition)
                    local=Inside(holderPosition.Tile);
                if (!local&&item.Holder==0&&e.Try<PositionComponent>(itemId) is { } itemPosition)
                    local=Inside(itemPosition.Tile);
                if (local)food+=definition.Calories*item.Freshness;
            }

            var projects=e.Store<ConstructionComponent>().All.Count(x=>
            {
                if (x.Value.Finished)return false;
                if (x.Value.Footprint.Count>0)return x.Value.Footprint.Any(Inside);
                return e.Try<PositionComponent>(x.Key) is { } position&&Inside(position.Tile);
            });

            var specializations=members
                .Select(id=>e.Get<SkillsComponent>(id).Experience
                    .OrderByDescending(x=>x.Value)
                    .ThenBy(x=>x.Key,StringComparer.Ordinal)
                    .FirstOrDefault())
                .Where(x=>!string.IsNullOrWhiteSpace(x.Key)&&x.Value>0)
                .OrderByDescending(x=>x.Value)
                .Select(x=>x.Key)
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();

            result.Add(new(
                anchor,
                NameFor(s.Seed,anchor),
                center,
                areas,
                cluster.Count,
                members.Length,
                families.Count,
                food,
                projects,
                specializations));
        }
        return result.OrderBy(x=>x.Anchor).ToArray();
    }

    public static SettlementSummary Describe(SimulationSession session)
    {
        return DescribeAll(session)
            .OrderByDescending(x=>x.Members)
            .ThenBy(x=>x.Anchor)
            .FirstOrDefault()
            ??new(0,"нет поселений",session.State.Start,[],0,0,0,0,0,[]);
    }

    private static int[] LivingPeople(SimulationSession session)
    {
        var e=session.State.Entities;
        return e.Store<IdentityComponent>().Ids()
            .Where(id=>e.Try<HealthComponent>(id) is { Alive:true }&&e.Has<PositionComponent>(id)&&e.Has<FamilyComponent>(id))
            .ToArray();
    }

    private static List<List<Home>> Clusters(Home[] homes)
    {
        var result=new List<List<Home>>();
        var unvisited=new HashSet<int>(Enumerable.Range(0,homes.Length));
        while (unvisited.Count>0)
        {
            var first=unvisited.Min();
            unvisited.Remove(first);
            var cluster=new List<Home>();
            var queue=new Queue<int>();
            queue.Enqueue(first);
            while (queue.TryDequeue(out var index))
            {
                cluster.Add(homes[index]);
                foreach (var other in unvisited
                    .Where(other=>homes[index].Position.Distance(homes[other].Position)<=HomeLinkDistance)
                    .ToArray())
                {
                    unvisited.Remove(other);
                    queue.Enqueue(other);
                }
            }
            cluster.Sort((a,b)=>a.Id.CompareTo(b.Id));
            result.Add(cluster);
        }
        return result;
    }

    private static SettlementArea[] AreasFor(List<Home> homes,DefinitionCatalog definitions,WorldMap map)
    {
        var areas=new HashSet<SettlementArea>();
        foreach (var home in homes)
        {
            IEnumerable<GridPoint> occupied;
            if (home.Project.Footprint.Count>0)occupied=home.Project.Footprint;
            else
            {
                var radius=definitions.Buildings[home.Definition].Size/2;
                occupied=
                    from y in Enumerable.Range(home.Position.Y-radius,radius*2+1)
                    from x in Enumerable.Range(home.Position.X-radius,radius*2+1)
                    select new GridPoint(x,y);
            }
            foreach (var cell in occupied)
                for (var dy=-HomePadding;dy<=HomePadding;dy++)
                    for (var dx=-HomePadding;dx<=HomePadding;dx++)
                    {
                        var p=cell+new GridPoint(dx,dy);
                        if (!map.Contains(p))continue;
                        areas.Add(new(p.X/SettlementCellSize*SettlementCellSize,p.Y/SettlementCellSize*SettlementCellSize));
                    }
        }
        return areas.OrderBy(x=>x.Y).ThenBy(x=>x.X).ToArray();
    }

    private static string NameFor(int seed,int anchor)
    {
        string[] roots=["берез","соснов","реч","озер","камен","лугов","дубров","ясн","верх","тих","зареч","серебр","мелов","ветров","светл","родник"];
        string[] endings=["овка","ино","ское","ье","ица","ово","бор","доль","поле","град","ное","ск"];
        unchecked
        {
            uint hash=2166136261;
            hash=(hash^(uint)seed)*16777619;
            hash=(hash^(uint)anchor)*16777619;
            var root=roots[(int)(hash%(uint)roots.Length)];
            hash=(hash>>1)^(hash*2246822519u);
            var ending=endings[(int)(hash%(uint)endings.Length)];
            var value=root+ending;
            return char.ToUpperInvariant(value[0])+value[1..];
        }
    }

    private sealed record Home(int Id,GridPoint Position,string Definition,ConstructionComponent Project);
}
