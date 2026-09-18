namespace LivingWorld.Simulation;
public sealed class RepairAction : SimAction
{
    public override string Id=>"repair";
    public override string Label=>"чинит инструмент";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var (id,item) in c.Inventory.Where(x=>x.Item.Durability<15))
        {
            var d=c.Definitions.Items[item.Definition];
            if(d.Tools.Count==0)continue;
            var material=c.Definitions.Items.Values
                .Where(x=>x.Material==d.Material&&x.Tools.Count==0)
                .OrderByDescending(x=>x.Tags.Contains("construction",StringComparer.Ordinal))
                .ThenByDescending(x=>x.Tags.Contains("metal",StringComparer.Ordinal))
                .FirstOrDefault(x=>x.Tags.Contains("construction",StringComparer.Ordinal)||x.Tags.Contains("metal",StringComparer.Ordinal));
            if(material is null)continue;
            var op=Option(c.Position,id,material.Id,20,local:true);
            op.Requires=[new(Item(material.Id),1)];
            op.Effects=[new(Item(material.Id),-1)];
            foreach(var tool in d.Tools)
                op.Effects.Add(new("tool:"+tool.Key,Math.Max(1,(int)MathF.Floor(d.Durability*.8f/2)),true));
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor))return false;
        var item=s.State.Entities.Try<ItemComponent>(step.Target);
        if(item is null||item.Holder!=actor||!s.Inventory.Consume(actor,step.Argument,1))return false;
        item.Durability=s.Definitions.Items[item.Definition].Durability*.8f;
        item.Sharpness=.9f;
        s.Events.Publish(new SkillUsedEvent(actor,"crafting",3));
        return true;
    }
}
