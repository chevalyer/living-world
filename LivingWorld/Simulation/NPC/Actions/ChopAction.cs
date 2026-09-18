namespace LivingWorld.Simulation;
public sealed class ChopAction : SimAction
{
    public override string Id=>"chop";
    public override string Label=>"рубит дерево";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("plant").Where(o=>o.Quantity>0))
        {
            var d=c.Definitions.Plants[o.Definition];
            if (d.Kind!="tree")continue;
            var units=Math.Min(3, o.Quantity);
            var efficiency=c.Inventory.Where(x=>x.Item.Durability>0).Select(x=>c.Definitions.Items[x.Item.Definition].Tools.GetValueOrDefault("chop")*x.Item.Quality*x.Item.Sharpness).DefaultIfEmpty(.5f).Max();
            var op=Option(o.Position, o.Entity, o.Product, 18/Math.Max(.3f, efficiency+c.Skills.Level("woodworking")*.1f));
            op.Requires=[new("tool:chop", 1), new(Source(o.Entity), units)];
            op.Effects=[new(Source(o.Entity), -units), new("ground:"+o.Entity+":"+o.Product, units)];
            op.Skill="woodworking";
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        if (s.Inventory.Tool(actor, "chop")<=0)return false;
        var resource=s.State.Entities.Try<PlantComponent>(step.Target);
        if (resource is null||resource.Yield<1)return false;
        var def=s.Definitions.Plants[resource.Definition];
        if (def.Kind!="tree")return false;
        var units=Math.Min(3, (int)resource.Yield);
        resource.Yield-=units;
        if (resource.Yield<1)
        {
            s.State.Entities.Remove(step.Target);
            s.Spatial.Remove(step.Target);
        }
        var product=def.Product;
        for (var i=0; i<units; i++)s.Inventory.Spawn(product, step.Position);
        s.Inventory.WearTool(actor, "chop", 2);
        s.Events.Publish(new SkillUsedEvent(actor, "woodworking", 4));
        return true;
    }
}
