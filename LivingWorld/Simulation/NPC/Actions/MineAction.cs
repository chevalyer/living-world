namespace LivingWorld.Simulation;
public sealed class MineAction : SimAction
{
    public override string Id=>"mine";
    public override string Label=>"добывает камень";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("resource").Where(o=>o.Quantity>0))
        {
            var units=Math.Min(3, o.Quantity);
            var efficiency=c.Inventory.Where(x=>x.Item.Durability>0).Select(x=>c.Definitions.Items[x.Item.Definition].Tools.GetValueOrDefault("mine")*x.Item.Quality*x.Item.Sharpness).DefaultIfEmpty(.5f).Max();
            var op=Option(o.Position, o.Entity, o.Product, 18/Math.Max(.3f, efficiency+c.Skills.Level("mining")*.1f));
            op.Requires=[new("tool:mine", 1), new(Source(o.Entity), units)];
            op.Effects=[new(Source(o.Entity), -units), new("ground:"+o.Entity+":"+o.Product, units)];
            op.Skill="mining";
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        if (s.Inventory.Tool(actor, "mine")<=0)return false;
        var resource=s.State.Entities.Try<ResourceComponent>(step.Target);
        if (resource is null||resource.Units<1)return false;
        var units=Math.Min(3, resource.Units);
        resource.Units-=units;
        var product=resource.Product;
        if (resource.Units==0)
        {
            s.State.Entities.Remove(step.Target);
            s.Spatial.Remove(step.Target);
        }
        for (var i=0; i<units; i++)s.Inventory.Spawn(product, step.Position);
        s.Inventory.WearTool(actor, "mine", 2);
        s.Events.Publish(new SkillUsedEvent(actor, "mining", 4));
        return true;
    }
}
