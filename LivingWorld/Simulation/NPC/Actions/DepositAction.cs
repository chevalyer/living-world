namespace LivingWorld.Simulation;
public sealed class DepositAction : SimAction
{
    public override string Id=>"deposit";
    public override string Label=>"складывает семейные запасы";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(c.Personality.Generosity<.3f)yield break;
        foreach(var storage in c.Storages().Take(3))
        foreach(var group in c.Inventory.GroupBy(x=>x.Item.Definition).Where(g=>g.Count()>3))
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
