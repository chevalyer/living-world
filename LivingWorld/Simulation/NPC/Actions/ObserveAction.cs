namespace LivingWorld.Simulation;
public sealed class ObserveAction : SimAction
{
    public override string Id=>"observe";
    public override string Label=>"осматривается";
    public override IEnumerable<ActionOption> Options(PlanningContext c)=>[];
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        PerceptionSystem.Observe(s, actor, s.State.Entities.Get<MemoryComponent>(actor));
        return true;
    }
}
