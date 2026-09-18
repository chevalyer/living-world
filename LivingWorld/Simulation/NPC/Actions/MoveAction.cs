namespace LivingWorld.Simulation;
public sealed class MoveAction : SimAction
{
    public override string Id=>"move";
    public override string Label=>"идет";
    public override IEnumerable<ActionOption> Options(PlanningContext c)=>[];
    public override bool Execute(SimulationSession s, int actor, ActionStep step)=>true;
}
