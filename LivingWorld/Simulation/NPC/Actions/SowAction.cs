namespace LivingWorld.Simulation;
public sealed class SowAction : SimAction
{
    public override string Id=>"sow";
    public override string Label=>"сеет";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("farming"))yield break;
        foreach (var cell in c.OfKind("farm_cell").Where(x=>x.Definition=="tilled").Take(4))
            foreach (var plant in c.Definitions.Plants.Values.Where(x=>x.Kind=="crop"&&x.Seed.Length>0))
            {
                var op=Option(cell.Position, cell.Entity, plant.Id, 20);
                op.Requires=[new(Item(plant.Seed), 1)];
                op.Effects=[new(Item(plant.Seed), -1), new("sown", 1, true)];
                yield return op;
            }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        if (!base.CanExecute(s,actor,step,out reason))return false;
        if (FarmService.CanSow(s,actor,step.Target,step.Position,step.Argument))return true;
        reason="сеять можно только на вспаханной свободной грядке";
        return false;
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
        =>FarmService.Sow(s,actor,step.Target,step.Position,step.Argument);
}
