namespace LivingWorld.Simulation;
public sealed class DepositAction : SimAction
{
    public override string Id=>"deposit";
    public override string Label=>"складывает семейные запасы";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        var storages=c.Storages().Take(3).ToArray();
        if(storages.Length==0)yield break;

        // Food storage is survival behavior, not generosity. Offer only foods the NPC can
        // physically obtain from its inventory or current observations so planner branching stays bounded.
        var foodCandidates=c.Inventory.Select(x=>x.Item.Definition)
            .Concat(c.Known.Where(x=>x.UnreachableUntil<=c.Tick&&x.Quantity>0&&x.Kind is "item" or "plant")
                .Select(x=>x.Kind=="plant"?x.Product:x.Product))
            .Where(c.Definitions.Items.ContainsKey)
            .Where(id=>c.Definitions.Items[id].Calories>0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id=>id,StringComparer.Ordinal)
            .Take(8)
            .ToArray();
        foreach(var storage in storages)
        foreach(var definition in foodCandidates)
        {
            var op=Option(storage.Position,storage.Entity,definition,3);
            op.Requires=[new(Item(definition),1)];
            op.Effects=[new(Item(definition),-1),new("food.stocked",1,true)];
            yield return op;
        }

        if(c.Personality.Generosity<.3f)yield break;
        foreach(var storage in storages)
        foreach(var group in c.Inventory.GroupBy(x=>x.Item.Definition).Where(g=>g.Count()>3&&c.Definitions.Items[g.Key].Calories<=0))
        {
            var op=Option(storage.Position,storage.Entity,group.Key,3);
            op.Requires=[new(Item(group.Key),4)];
            op.Effects=[new(Item(group.Key),-1),new("distributed",1,true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor))return false;
        var id=FindItem(s,actor,step.Argument);
        return id!=0&&StorageService.Deposit(s,actor,step.Target,id);
    }
}
