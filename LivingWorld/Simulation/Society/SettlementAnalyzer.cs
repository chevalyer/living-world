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
        var result=new List<SettlementSummary>(clusters.Count);
        foreach (var cluster in clusters)
        {
            var homeIds=cluster.Select(x=>x.Id).ToHashSet();
            var anchor=cluster.Min(x=>x.Id);
            var center=new GridPoint(
                (int)MathF.Round(cluster.Average(x=>x.Position.X)),
                (int)MathF.Round(cluster.Average(x=>x.Position.Y)));

            var minX=cluster.Min(x=>x.Position.X-session.Definitions.Buildings[x.Definition].Size/2)-BorderPadding;
            var minY=cluster.Min(x=>x.Position.Y-session.Definitions.Buildings[x.Definition].Size/2)-BorderPadding;
            var maxX=cluster.Max(x=>x.Position.X+session.Definitions.Buildings[x.Definition].Size/2)+BorderPadding;
            var maxY=cluster.Max(x=>x.Position.Y+session.Definitions.Buildings[x.Definition].Size/2)+BorderPadding;
            minX=Math.Clamp(minX,0,s.Map.Width-1);
            minY=Math.Clamp(minY,0,s.Map.Height-1);
            maxX=Math.Clamp(maxX,0,s.Map.Width-1);
            maxY=Math.Clamp(maxY,0,s.Map.Height-1);

            var members=LivingPeople(session)
                .Where(id=>
                {
                    var family=e.Get<FamilyComponent>(id);
                    if (family.HomeProject!=0)return homeIds.Contains(family.HomeProject);
                    var position=e.Get<PositionComponent>(id).Tile;
                    return cluster.Min(h=>h.Position.Distance(position))<=ResidentReach;
                })
                .Distinct()
                .OrderBy(id=>id)
                .ToArray();
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
                NameFor(s.Seed,anchor),
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
                foreach (var other in unvisited.Where(other=>homes[index].Position.Distance(homes[other].Position)<=HomeLinkDistance).ToArray())
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

    private sealed record Home(int Id,GridPoint Position,string Definition);
}
