namespace LivingWorld.Simulation;
public sealed class PlanBuildingAction : SimAction
{
    public override string Id=>"plan_building";
    public override string Label=>"размечает дом";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(c.BuildSite is not { } site||c.Family.HomeProject!=0||!c.Knowledge.Facts.Contains("building"))yield break;
        foreach(var d in c.Definitions.Buildings.Values)
        {
            var reservation=int.MinValue+site.Y*c.MapWidth+site.X;
            var op=Option(site,reservation,d.Id,5);
            op.Requires=[new("site",1)];
            op.Effects=[new("site",-1),new("project:"+d.Id,1,true)];
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="место больше не подходит";
        if(!base.CanExecute(s,actor,step,out _))return false;
        if(s.State.Entities.Get<FamilyComponent>(actor).HomeProject!=0)return false;
        return s.Definitions.Buildings.TryGetValue(step.Argument,out var definition)&&
            BuildingService.CanPlace(s,step.Position,definition.Size);
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)=>
        CanExecute(s,actor,step,out _)&&BuildingService.Start(s,actor,step.Position,step.Argument)!=0;
}
