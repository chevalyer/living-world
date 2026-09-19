namespace LivingWorld.Simulation;
// Action providers receive an NPC's beliefs and their own inventory; they cannot query hidden world entities.
public sealed class PlanningContext
{
    public int Actor { get; init; }
    public GridPoint Position { get; init; }
    public long Tick { get; init; }
    public int Age { get; init; }
    public bool CanWork { get; init; }
    public DefinitionCatalog Definitions { get; init; } = new();
    public List<Observation> Known { get; init; } = [];
    public List<(int Id, ItemComponent Item)> Inventory { get; init; } = [];
    public List<int> Worn { get; init; } = [];
    public KnowledgeComponent Knowledge { get; init; } = new();
    public NeedsComponent Needs { get; init; } = new();
    public PersonalityComponent Personality { get; init; } = new();
    public FamilyComponent Family { get; init; } = new();
    public RelationshipComponent Relationships { get; init; } = new();
    public SkillsComponent Skills { get; init; } = new();
    public float Air { get; init; }
    public float OutdoorAir { get; init; }
    public float Temperature { get; init; }
    public float Insulation { get; init; }
    public bool Sheltered { get; init; }
    public int Room { get; init; }
    public int MapWidth { get; init; }
    public GridPoint? BuildSite { get; init; }
    public GridPoint? FarmSite { get; init; }
    public GridPoint? SowSite { get; init; }
    public Dictionary<string,GridPoint> FacilitySites { get; init; } = new(StringComparer.Ordinal);
    public bool SocialReady { get; init; }
    public float MaxMass { get; init; } = 28;
    public float MaxVolume { get; init; } = 40;
    public static string ItemFact(string item)=>"item:"+item;
    public IEnumerable<Observation> OfKind(string kind)=>Known.Where(o=>o.Kind==kind&&o.UnreachableUntil<=Tick).OrderBy(o=>o.Position.Distance(Position)).ThenBy(o=>o.Entity).Take(10);
    private bool FacilityAccessible(Observation o)
    {
        if(o.Project==0||o.Project==Family.HomeProject)return true;
        return o.Kind=="facility"&&Definitions.Facilities.TryGetValue(o.Definition,out var definition)&&definition.Access=="community";
    }
    public IEnumerable<Observation> Facilities(string capability)=>Known.Where(o=>o.Kind=="facility"&&o.UnreachableUntil<=Tick&&
        o.Capabilities.Contains(capability,StringComparer.Ordinal)&&FacilityAccessible(o))
        .OrderBy(o=>o.Position.Distance(Position)).ThenBy(o=>o.Entity).Take(10);
    public IEnumerable<Observation> Storages()=>Known.Where(o=>o.UnreachableUntil<=Tick&&
        (o.Kind=="storage"||o.Kind=="facility"&&o.Capabilities.Contains("storage",StringComparer.Ordinal))&&FacilityAccessible(o))
        .OrderByDescending(o=>o.Kind=="facility").ThenBy(o=>o.Position.Distance(Position)).ThenBy(o=>o.Entity).Take(10);
    public PlanningState InitialState()
    {
        var state=new PlanningState
        {
            Position=Position
        };
        state.Facts["capacity.mass"]=(int)MathF.Floor((MaxMass-Inventory.Sum(x=>Definitions.Items[x.Item.Definition].Mass))*1000);
        state.Facts["capacity.volume"]=(int)MathF.Floor((MaxVolume-Inventory.Sum(x=>Definitions.Items[x.Item.Definition].Volume))*1000);
        var reserve=0;
        foreach (var (_, item) in Inventory)
        {
            var definition=Definitions.Items[item.Definition];
            var usable=definition.Calories<=0||item.Freshness>=.1f;
            if (usable)state.Facts[ItemFact(item.Definition)]=state.Get(ItemFact(item.Definition))+1;
            if (definition.Calories>0&&item.Freshness>=.1f)reserve+=(int)MathF.Round(definition.Calories*item.Freshness);
            foreach(var tool in definition.Tools)
                if(item.Durability>0)state.Facts["tool:"+tool.Key]=Math.Max(state.Get("tool:"+tool.Key),Math.Max(1,(int)MathF.Floor(item.Durability/2)));
        }
        state.Facts["food.reserve"]=reserve;
        foreach (var o in Known.Where(o=>o.Quantity>0))state.Facts["source:"+o.Entity]=o.Quantity;
        foreach (var storage in Storages())foreach (var item in storage.Items)state.Facts["stock:"+storage.Entity+":"+item.Key]=item.Value;
        foreach(var facility in Known.Where(o=>o.Kind=="facility"))
        {
            state.Facts["facility:"+facility.Definition]=1;
            foreach(var capability in facility.Capabilities)state.Facts["capability:"+capability]=1;
        }
        if (Known.Any(o=>o.Kind=="fire"&&o.Quantity>0&&o.Position.Distance(Position)<=2))state.Facts["heat"]=1;
        if (BuildSite.HasValue)state.Facts["site"]=1;
        if (Sheltered)state.Facts["indoors"]=1;
        return state;
    }
}
