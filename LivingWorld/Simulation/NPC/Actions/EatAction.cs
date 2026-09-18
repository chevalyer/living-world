namespace LivingWorld.Simulation;
public sealed class EatAction : SimAction
{
    public override string Id=>"eat";
    public override string Label=>"ест";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var d in c.Definitions.Items.Values.Where(d=>d.Calories>0&&d.Toxicity<.1f))
        {
            var op=Option(c.Position, argument:d.Id, duration:3, local:true);
            op.Requires=[new(Item(d.Id), 1)];
            op.Effects=[new(Item(d.Id), -1), new("fed", 1, true)];
            if (d.Water>.2f)op.Effects.Add(new("hydrated", 1, true));
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var id=s.Inventory.Items(actor).Where(i=>s.State.Entities.Get<ItemComponent>(i).Definition==step.Argument&&s.State.Entities.Get<ItemComponent>(i).Freshness>=.1f).OrderByDescending(i=>s.State.Entities.Get<ItemComponent>(i).Freshness).FirstOrDefault();
        if (id==0)return false;
        var item=s.State.Entities.Get<ItemComponent>(id);
        var d=s.Definitions.Items[item.Definition];
        if (d.Calories<=0)return false;
        var needs=s.State.Entities.Get<NeedsComponent>(actor);
        needs.Hunger=Math.Max(0, needs.Hunger-d.Calories*item.Freshness/2400);
        needs.Thirst=Math.Max(0, needs.Thirst-d.Water/2);
        s.State.Entities.Get<HealthComponent>(actor).Value-=d.Toxicity*20+(item.Freshness<.1f?2:0);
        s.Inventory.Destroy(id);
        return true;
    }
}
