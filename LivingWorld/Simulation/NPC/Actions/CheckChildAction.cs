namespace LivingWorld.Simulation;
public sealed class CheckChildAction : SimAction
{
    public override string Id=>"check_child";
    public override string Label=>"проверяет ребенка";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var child in c.OfKind("npc").Where(o=>c.Family.Children.Contains(o.Entity)&&o.Age<3&&c.Tick-o.SeenTick>60))
        {
            var op=Option(child.Position,child.Entity,duration:3);
            op.Effects=[new($"checked:{child.Entity}",1,true)];
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="ребенок ушел";
        return s.State.Entities.Try<HealthComponent>(step.Target) is { Alive:true }&&
            s.State.Entities.Get<FamilyComponent>(actor).Children.Contains(step.Target)&&
            s.State.Entities.Get<PositionComponent>(actor).Tile.Distance(s.State.Entities.Get<PositionComponent>(step.Target).Tile)<=1;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        PerceptionSystem.Observe(s,actor,s.State.Entities.Get<MemoryComponent>(actor));
        return true;
    }
}
