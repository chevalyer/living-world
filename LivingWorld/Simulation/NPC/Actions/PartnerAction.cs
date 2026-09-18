namespace LivingWorld.Simulation;
public sealed class PartnerAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"partner";
    public override string Label=>"предлагает жить вместе";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (c.Family.Partner!=0||!c.SocialReady)yield break;
        foreach (var other in c.OfKind("npc").Where(o=>o.Age>=18&&o.Partner==0&&o.TrustBack>.55f&&o.AffectionBack>.5f))
        {
            var r=c.Relationships.People.GetValueOrDefault(other.Entity);
            if (r is null||r.Trust<=.55f||r.Affection<=.5f)continue;
            var op=Option(other.Position, other.Entity, duration:20);
            op.Effects=[new("partnered", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        if (!FamilyRules.CanPartner(s.State, actor, step.Target))return false;
        if (s.State.Entities.Get<PositionComponent>(actor).Tile.Distance(s.State.Entities.Get<PositionComponent>(step.Target).Tile)>1)return false;
        var a=s.State.Entities.Get<FamilyComponent>(actor);
        var b=s.State.Entities.Get<FamilyComponent>(step.Target);
        a.Partner=step.Target;
        b.Partner=actor;
        if (a.HomeProject==0)a.HomeProject=b.HomeProject;
        if (b.HomeProject==0)b.HomeProject=a.HomeProject;
        s.State.Log(s.State.Entities.Get<IdentityComponent>(actor).FullName+" и "+s.State.Entities.Get<IdentityComponent>(step.Target).FullName+" стали партнерами.");
        s.State.Entities.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        return true;
    }
}
