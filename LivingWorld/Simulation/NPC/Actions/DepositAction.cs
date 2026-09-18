namespace LivingWorld.Simulation;
public sealed class DepositAction : SimAction
{
    public override string Id=>"deposit";
    public override string Label=>"пополняет общий склад";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (c.Personality.Generosity<.4f)yield break;
        foreach (var storage in c.OfKind("storage").Take(2)) foreach (var group in c.Inventory.GroupBy(x=>x.Item.Definition).Where(g=>g.Count()>5))
        {
            var op=Option(storage.Position, storage.Entity, group.Key, 3);
            op.Requires=[new(Item(group.Key), 6)];
            op.Effects=[new(Item(group.Key), -1), new("distributed", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var id=FindItem(s, actor, step.Argument);
        return id!=0&&StorageService.Deposit(s, actor, step.Target, id);
    }
}
