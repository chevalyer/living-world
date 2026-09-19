namespace LivingWorld.Simulation;
public sealed class DiscardSpoiledAction : SimAction
{
    public override string Id=>"discard_spoiled";
    public override string Label=>"убирает испорченную еду";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var (id,item) in c.Inventory.Where(x=>x.Item.Freshness<.1f&&c.Definitions.Items[x.Item.Definition].Calories>0))
        {
            var op=Option(c.Position,id,duration:1,local:true);
            op.Effects=[new("cleaned",1,true)];
            yield return op;
        }
        foreach(var storage in c.Storages().Where(o=>o.Spoiled>0))
        {
            var op=Option(storage.Position,storage.Entity,duration:2);
            op.Effects=[new("cleaned",1,true)];
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        if(!base.CanExecute(s,actor,step,out reason))return false;
        var e=s.State.Entities;
        if(e.Try<ItemComponent>(step.Target) is { } item)
        {
            if(item.Holder==actor&&item.Freshness<.1f&&s.Definitions.Items[item.Definition].Calories>0)return true;
            reason="предмет уже забрали";
            return false;
        }
        if(!e.Has<StorageComponent>(step.Target)||!StorageService.CanUse(s,actor,step.Target))
        {
            reason="предмет уже забрали";
            return false;
        }
        var spoiled=s.Inventory.Items(step.Target).Any(id=>
        {
            var stored=e.Get<ItemComponent>(id);
            return s.Definitions.Items[stored.Definition].Calories>0&&stored.Freshness<.1f;
        });
        if(spoiled)return true;
        reason="предмет уже забрали";
        return false;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        var e=s.State.Entities;
        if(e.Try<ItemComponent>(step.Target) is { } item)
        {
            if(item.Holder!=actor||item.Freshness>=.1f||s.Definitions.Items[item.Definition].Calories<=0)return false;
            s.Inventory.Destroy(step.Target);
            return true;
        }
        if(!e.Has<StorageComponent>(step.Target))return false;
        var spoiled=s.Inventory.Items(step.Target).FirstOrDefault(id=>
        {
            var stored=e.Get<ItemComponent>(id);
            return s.Definitions.Items[stored.Definition].Calories>0&&stored.Freshness<.1f;
        });
        if(spoiled==0)return false;
        s.Inventory.Destroy(spoiled);
        return true;
    }
}
