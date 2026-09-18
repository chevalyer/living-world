namespace LivingWorld.Simulation;
public sealed class BuildAction : SimAction
{
    public override string Id=>"build";
    public override string Label=>"строит";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("building"))yield break;
        foreach (var o in c.OfKind("project").Where(o=>o.Quantity>0&&(o.Entity==c.Family.HomeProject||c.Family.HomeProject==0)))
        {
            var d=c.Definitions.Buildings[o.Definition];
            var op=Option(o.WorkPosition??o.Position, o.Entity, o.Definition, d.WorkMinutes/(1+c.Skills.Level("building")*.1f));
            op.Requires=[new(Item(d.Resource), d.UnitsPerElement)];
            op.Effects=[new(Item(d.Resource), -d.UnitsPerElement), new("housing_progress", 1, true)];
            yield return op;
        }
        if (c.BuildSite is not
        {
        }
        site||c.Family.HomeProject!=0)yield break;
        foreach (var d in c.Definitions.Buildings.Values)
        {
            var op=Option(site, argument:d.Id, duration:d.WorkMinutes);
            op.Requires=[new("project:"+d.Id, 1), new(Item(d.Resource), d.UnitsPerElement)];
            op.Effects=[new(Item(d.Resource), -d.UnitsPerElement), new("housing_progress", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var target=step.Target!=0?step.Target:s.State.Entities.Get<FamilyComponent>(actor).HomeProject;
        return BuildingService.BuildNext(s, actor, target);
    }
}
