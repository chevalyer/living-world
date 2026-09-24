namespace LivingWorld.Simulation;
public sealed class TakeAction : SimAction
{
    public override string Id=>"take";
    public override string Label=>"берет из семейного запаса";
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var storage in c.Storages())
        foreach(var item in storage.Items.Where(x=>x.Value>0))
        {
            var op=Option(storage.Position,storage.Entity,item.Key,2);
            op.Requires=[new("stock:"+storage.Entity+":"+item.Key,1)];
            op.Effects=[new("stock:"+storage.Entity+":"+item.Key,-1),new(Item(item.Key),1)];
            var definition=c.Definitions.Items[item.Key];
            if(definition.Calories>0)op.Effects.Add(new("food.reserve",(int)MathF.Max(1,definition.Calories)));
            foreach(var tool in definition.Tools)
                op.Effects.Add(new("tool:"+tool.Key,Math.Max(1,(int)MathF.Floor(definition.Durability/2)),true));
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        if(!base.CanExecute(s,actor,step,out reason))return false;
        var e=s.State.Entities;
        if(!e.Has<StorageComponent>(step.Target)||!StorageService.CanUse(s,actor,step.Target))
        {
            reason="предмет уже забрали";
            return false;
        }
        var definition=s.Definitions.Items[step.Argument];
        var available=s.Inventory.Items(step.Target).Any(id=>
        {
            var item=e.Get<ItemComponent>(id);
            var owner=e.Get<OwnershipComponent>(id).Owner;
            return item.Definition==step.Argument&&(owner==0||owner==actor)&&
                (definition.Calories<=0||item.Freshness>=.1f);
        });
        if(available)return true;
        reason=definition.Calories>0?"еда больше недоступна":"предмет уже забрали";
        return false;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)=>StorageService.Take(s,actor,step.Target,step.Argument);
}
