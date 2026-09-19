namespace LivingWorld.Simulation;
public sealed class PlanFarmAction : SimAction
{
    public override string Id=>"plan_farm";
    public override string Label=>"размечает грядки";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("farming")||c.FarmSite is not { } site||c.OfKind("farm_cell").Any())yield break;
        var op=Option(site,duration:10);
        op.Effects=[new("farm_plotted",1,true)];
        yield return op;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
        =>FarmService.Start(s,actor,step.Position)!=0;
}
