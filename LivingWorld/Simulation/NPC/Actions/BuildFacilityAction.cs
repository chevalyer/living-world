namespace LivingWorld.Simulation;
public sealed class BuildFacilityAction : SimAction
{
    public override string Id=>"build_facility";
    public override string Label=>"изготавливает мебель или рабочее место";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;

    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var (definitionId,site) in c.FacilitySites)
        {
            var definition=c.Definitions.Facilities[definitionId];
            if(!c.Knowledge.Facts.Contains(definition.Knowledge))continue;
            var reservation=-1_000_000_000-(site.Y*c.MapWidth+site.X);
            var op=Option(site,reservation,definition.Id,definition.WorkMinutes);
            op.Requires=definition.Inputs.Select(x=>new FactRequirement(Item(x.Key),x.Value)).ToList();
            op.Effects=definition.Inputs.Select(x=>new FactEffect(Item(x.Key),-x.Value)).ToList();
            op.Effects.Add(new("facility:"+definition.Id,1,true));
            foreach(var capability in definition.Capabilities)op.Effects.Add(new("capability:"+capability,1,true));
            op.Skill=definition.Skill;
            yield return op;
        }
    }

    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="место для объекта больше не подходит";
        return s.Definitions.Facilities.TryGetValue(step.Argument,out var definition)&&
            FacilityService.CanPlace(s,actor,definition,step.Position);
    }

    public override bool Execute(SimulationSession s,int actor,ActionStep step)=>
        FacilityService.Build(s,actor,step.Argument,step.Position)!=0;
}
