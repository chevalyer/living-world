namespace LivingWorld.Simulation;
public sealed class StartFamilyAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"start_family";
    public override string Label=>"проводит время с партнером";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(c.Family.Partner==0||c.Family.PregnancyDueTick.HasValue||c.Needs.Hunger>.4f||c.Needs.Thirst>.4f||c.Needs.Fatigue>.7f||!c.Sheltered||c.Room==0)yield break;
        var partner=c.OfKind("npc").FirstOrDefault(o=>o.Entity==c.Family.Partner&&o.Age>=18);
        if(partner is null||partner.Pregnant||!partner.Sheltered||partner.Room!=c.Room||
           partner.Need>.45f||partner.Thirst>.45f||partner.Fatigue>.7f||partner.Health<65)yield break;
        var relationship=c.Relationships.People.GetValueOrDefault(partner.Entity);
        if(relationship is null||relationship.Trust<.6f||relationship.Affection<.55f||relationship.Irritation>.2f)yield break;
        var op=Option(partner.Position,partner.Entity,duration:45);
        op.Effects=[new("family",1,true)];
        yield return op;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor)||!FamilyRules.CanConceive(s.State,actor,step.Target))return false;
        var e=s.State.Entities;
        var mother=e.Get<IdentityComponent>(actor).Sex=="female"?actor:step.Target;
        var father=mother==actor?step.Target:actor;
        var family=e.Get<FamilyComponent>(mother);
        family.PregnancyDueTick=s.State.Clock.Tick+270*1440;
        family.PregnancyFather=father;
        e.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        s.State.Log(e.Get<IdentityComponent>(mother).FullName+" ждет ребенка.");
        return true;
    }
}
