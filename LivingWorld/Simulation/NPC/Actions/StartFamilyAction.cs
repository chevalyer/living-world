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
        if (c.Family.Partner==0||c.Family.PregnancyDueTick.HasValue||c.Needs.Hunger>.4f||c.Needs.Thirst>.4f||!c.Sheltered)yield break;
        var partner=c.OfKind("npc").FirstOrDefault(o=>o.Entity==c.Family.Partner&&o.Age>=18);
        if (partner is null||partner.Pregnant)yield break;
        var op=Option(partner.Position, partner.Entity, duration:45);
        op.Effects=[new("family", 1, true)];
        yield return op;
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        if (!FamilyRules.CanConceive(s.State, actor, step.Target))return false;
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
