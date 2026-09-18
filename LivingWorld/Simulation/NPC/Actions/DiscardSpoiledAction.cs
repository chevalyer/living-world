namespace LivingWorld.Simulation;
public sealed class DiscardSpoiledAction : SimAction
{
    public override string Id=>"discard_spoiled";
    public override string Label=>"убирает испорченную еду";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var (id, item) in c.Inventory.Where(x=>x.Item.Freshness<.1f&&c.Definitions.Items[x.Item.Definition].Calories>0))
        {
            var op=Option(c.Position, id, duration:1, local:true);
            op.Effects=[new("cleaned", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var item=s.State.Entities.Try<ItemComponent>(step.Target);
        return item is not null&&item.Freshness<.1f&&s.Inventory.Drop(actor, step.Target);
    }
}
