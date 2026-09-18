namespace LivingWorld.Simulation;
public sealed class TalkAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"talk";
    public override string Label=>"разговаривает";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(!c.SocialReady)yield break;
        foreach(var o in c.OfKind("npc").Where(o=>o.Age>=3))
        {
            var op=Option(o.Position,o.Entity,duration:15);
            op.Effects=[new("socialized",1,true)];
            op.Risk=(c.Relationships.People.GetValueOrDefault(o.Entity)?.Irritation??0)*30;
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="собеседник ушел";
        return s.State.Entities.Try<HealthComponent>(step.Target) is { Alive:true }&&
            s.State.Entities.Get<PositionComponent>(actor).Tile.Distance(s.State.Entities.Get<PositionComponent>(step.Target).Tile)<=1;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        var e=s.State.Entities;
        e.Get<NeedsComponent>(actor).Loneliness=Math.Max(0,e.Get<NeedsComponent>(actor).Loneliness-.45f);
        e.Get<NeedsComponent>(step.Target).Loneliness=Math.Max(0,e.Get<NeedsComponent>(step.Target).Loneliness-.2f);
        e.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        var receiver=e.Get<MemoryComponent>(actor);
        var other=e.Get<MemoryComponent>(step.Target);
        var shared=other.Observations
            .Where(o=>o.Kind is "water" or "plant" or "project")
            .OrderByDescending(o=>o.SeenTick)
            .GroupBy(o=>o.Kind=="water"?"water":o.Kind+":"+o.Entity,StringComparer.Ordinal)
            .Select(g=>g.First())
            .Take(5);
        foreach(var memory in shared)
        {
            var existing=receiver.Observations.FirstOrDefault(o=>o.Kind==memory.Kind&&o.Position==memory.Position);
            var copy=memory with { Confidence=memory.Confidence*.7f,Skills=new(memory.Skills),Items=new(memory.Items) };
            if(existing is null)receiver.Observations.Add(copy);
            else if(memory.SeenTick>existing.SeenTick)
            {
                var index=receiver.Observations.IndexOf(existing);
                receiver.Observations[index]=copy;
            }
        }
        s.Events.Publish(new SocialEvent(actor,step.Target,"conversation",.065f));
        s.Events.Publish(new SkillUsedEvent(actor,"social",2));
        return true;
    }
}
