namespace LivingWorld.Simulation;
public sealed class TillAction : SimAction
{
    public override string Id=>"till";
    public override string Label=>"вспахивает грядку";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("farming"))yield break;
        foreach (var cell in c.OfKind("farm_cell").Where(x=>x.Definition=="untilled").Take(3))
        {
            var op=Option(cell.Position,cell.Entity,duration:15);
            op.Requires=[new("tool:till",1)];
            op.Effects=[new("tilled",1,true)];
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        if (!base.CanExecute(s,actor,step,out reason))return false;
        if (FarmService.CanTill(s,actor,step.Target,step.Position))return true;
        reason="нужна свободная грядка и инструмент для вспашки";
        return false;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
        =>FarmService.Till(s,actor,step.Target,step.Position);
}
