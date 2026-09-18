namespace LivingWorld.Simulation;
public sealed class PlayAction : SimAction
{
    public override string Id=>"play";
    public override string Label=>"играет и наблюдает";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (c.Age<3||c.Age>=18)yield break;
        var op=Option(c.Position, duration:20, local:true);
        op.Effects=[new("played", 1, true), new("socialized", 1, true)];
        yield return op;
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var age=s.State.Clock.Age(s.State.Entities.Get<IdentityComponent>(actor).BirthDate);
        if (age<3||age>=18)return false;
        s.State.Entities.Get<NeedsComponent>(actor).Loneliness=Math.Max(0, s.State.Entities.Get<NeedsComponent>(actor).Loneliness-.15f);
        PerceptionSystem.Observe(s, actor, s.State.Entities.Get<MemoryComponent>(actor));
        s.Events.Publish(new SkillUsedEvent(actor, "observation", 1));
        return true;
    }
}
