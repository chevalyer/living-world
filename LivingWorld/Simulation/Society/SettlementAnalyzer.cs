namespace LivingWorld.Simulation;

public sealed record SettlementSummary(
    int Anchor,
    string Name,
    GridPoint Center,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int Homes,
    int Members,
    int Families,
    float FoodCalories,
    int Projects,
    string[] Specializations);

public static class SettlementAnalyzer
{
    public const int HomeLinkDistance = 18;
    public const int ResidentReach = 28;
    private const int BorderPadding = 4;

    public static IReadOnlyList<SettlementSummary> DescribeAll(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var homes=e.Store<ConstructionComponent>().All
            .Where(x=>x.Value.Finished&&e.Has<PositionComponent>(x.Key))
            .Select(x=>new Home(x.Key,e.Get<PositionComponent>(x.Key).Tile,x.Value.Definition))
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
        var usedNames=new HashSet<string>(StringComparer.Ordinal);
        for (var clusterIndex=0;clusterIndex<clusters.Count;clusterIndex++)
        {
            var cluster=clusters[clusterIndex];
            var anchor=cluster.Min(x=>x.Id);
            var center=new GridPoint(
                (int)Math.Round(cluster.Average(x=>x.Position.X)),
                (int)Math.Round(cluster.Average(x=>x.Position.Y)));

            var minX=cluster.Min(x=>x.Position.X-session.Definitions.Buildings[x.Definition].Size/2)-BorderPadding;
            var minY=cluster.Min(x=>x.Position.Y-session.Definitions.Buildings[x.Definition].Size/2)-BorderPadding;
            var maxX=cluster.Max(x=>x.Position.X+session.Definitions.Buildings[x.Definition].Size/2)+BorderPadding;
            var maxY=cluster.Max(x=>x.Position.Y+session.Definitions.Buildings[x.Definition].Size/2)+BorderPadding;
            minX=Math.Clamp(minX,0,s.Map.Width-1);
            minY=Math.Clamp(minY,0,s.Map.Height-1);
            maxX=Math.Clamp(maxX,0,s.Map.Width-1);
            maxY=Math.Clamp(maxY,0,s.Map.Height-1);

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

            bool Inside(GridPoint p)=>p.X>=minX&&p.X<=maxX&&p.Y>=minY&&p.Y<=maxY;
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
                !x.Value.Finished&&e.Try<PositionComponent>(x.Key) is { } position&&
                (Inside(position.Tile)||position.Tile.Distance(center)<=ResidentReach));

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
                NameFor(session,anchor,usedNames),
                center,
                minX,minY,maxX,maxY,
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
            ??new(0,"нет поселений",session.State.Start,0,0,0,0,0,0,0,0,0,[]);
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

    private static string NameFor(SimulationSession session,int anchor,HashSet<string> used)
    {
        var generator=new NameGenerator(session.Definitions.Names);
        for(var attempt=0;;attempt++)
        {
            var random=new DeterministicRandom(RandomService.Hash(session.State.Seed,$"settlement:{anchor}:{attempt}"));
            var value=generator.GenerateWord(random,2,4);
            if(used.Add(value))return value;
        }
    }

    private sealed record Home(int Id,GridPoint Position,string Definition);
}
