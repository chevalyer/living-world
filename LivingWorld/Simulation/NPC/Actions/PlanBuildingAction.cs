namespace LivingWorld.Simulation;
public sealed class PlanBuildingAction : SimAction
{
    public override string Id=>"plan_building";
    public override string Label=>"размечает дом";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (c.BuildSite is not
        {
        }
        site||c.Family.HomeProject!=0||!c.Knowledge.Facts.Contains("building"))yield break;
        foreach (var d in c.Definitions.Buildings.Values)
        {
            var op=Option(site, argument:d.Id, duration:5);
            op.Requires=[new("site", 1)];
            op.Effects=[new("site", -1), new("project:"+d.Id, 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)=>BuildingService.Start(s, actor, step.Position, step.Argument)!=0;
}
