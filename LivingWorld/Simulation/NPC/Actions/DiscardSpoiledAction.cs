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
        foreach(var storage in c.OfKind("storage").Where(o=>o.Spoiled>0))
        {
            var op=Option(storage.Position,storage.Entity,duration:2);
            op.Effects=[new("cleaned",1,true)];
            yield return op;
        }
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
