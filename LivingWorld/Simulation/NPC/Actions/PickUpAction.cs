namespace LivingWorld.Simulation;
public sealed class PickUpAction : SimAction
{
    public override string Id=>"pickup";
    public override string Label=>"поднимает предмет";
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("item").Where(o=>o.Quantity>0&&(o.Owner==0||o.Owner==c.Actor)))
        {
            var op=Option(o.Position, o.Entity, o.Product);
            op.Requires=[new(Source(o.Entity), 1)];
            op.Effects=[new(Source(o.Entity), -1), new(Item(o.Product), 1)];
            AddCapabilities(c,op,o.Product,o,1);
            yield return op;
        }
        foreach (var o in c.Known.Where(o=>o.Kind is "plant" or "resource").OrderBy(o=>o.Position.Distance(c.Position)).Take(30))
        {
            var units=Math.Min(3,Math.Max(1,o.Quantity));
            var op=Option(o.Position, o.Entity, o.Product);
            op.Requires=[new("ground:"+o.Entity+":"+o.Product, units)];
            op.Effects=[new("ground:"+o.Entity+":"+o.Product, -units), new(Item(o.Product), units)];
            AddCapabilities(c,op,o.Product,null,units);
            yield return op;
        }
    }
    private static void AddCapabilities(PlanningContext c,ActionOption op,string product,Observation? observed,int units)
    {
        var d=c.Definitions.Items[product];
        var durability=observed?.Durability??d.Durability;
        foreach(var tool in d.Tools)
            if(durability>0)op.Effects.Add(new("tool:"+tool.Key,Math.Max(1,(int)MathF.Floor(durability/2)),true));
        if(d.Calories>0)
        {
            var freshness=observed?.Freshness??1;
            op.Effects.Add(new("food.reserve",(int)MathF.Max(1,d.Calories*freshness*units)));
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="предмет уже забрали";
        var e=s.State.Entities;
        if(e.Try<ItemComponent>(step.Target) is { Holder:0 } direct&&e.Try<PositionComponent>(step.Target) is { } directPosition)
        {
            var owner=e.Get<OwnershipComponent>(step.Target).Owner;
            return directPosition.Tile.Distance(e.Get<PositionComponent>(actor).Tile)<=1&&(owner==0||owner==actor)&&s.Inventory.CanCarry(actor,direct.Definition);
        }
        return s.Spatial.Query(step.Position,1).Any(id=>
        {
            var item=e.Try<ItemComponent>(id);
            var position=e.Try<PositionComponent>(id);
            if(item is null||item.Holder!=0||position?.Tile!=step.Position||item.Definition!=step.Argument)return false;
            var owner=e.Get<OwnershipComponent>(id).Owner;
            return (owner==0||owner==actor)&&s.Inventory.CanCarry(actor,item.Definition);
        });
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (s.State.Entities.Has<ItemComponent>(step.Target))return s.Inventory.PickUp(actor, step.Target);
        var picked=0;
        foreach (var id in s.Spatial.Query(step.Position, 1).ToArray())
        {
            if(picked>=3)break;
            var item=s.State.Entities.Try<ItemComponent>(id);
            var p=s.State.Entities.Try<PositionComponent>(id);
            if(item is null||item.Definition!=step.Argument||p?.Tile!=step.Position)continue;
            if(!s.Inventory.PickUp(actor,id))continue;
            picked++;
        }
        return picked>0;
    }
}
