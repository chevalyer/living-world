namespace LivingWorld.Simulation;
public sealed class TakeAction : SimAction
{
    public override string Id=>"take";
    public override string Label=>"берет со склада";
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var storage in c.OfKind("storage"))foreach (var item in storage.Items.Where(x=>x.Value>0))
        {
            var op=Option(storage.Position, storage.Entity, item.Key, 2);
            op.Requires=[new("stock:"+storage.Entity+":"+item.Key, 1)];
            op.Effects=[new("stock:"+storage.Entity+":"+item.Key, -1), new(Item(item.Key), 1)];
            foreach (var tool in c.Definitions.Items[item.Key].Tools)op.Effects.Add(new("tool:"+tool.Key, 1, true));
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)=>StorageService.Take(s, actor, step.Target, step.Argument);
}
