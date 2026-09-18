namespace LivingWorld.Simulation;
public sealed class DropAction : SimAction
{
    public override string Id=>"drop";
    public override string Label=>"складывает предмет";
    public override IEnumerable<ActionOption> Options(PlanningContext c)=>[];
    public override bool Execute(SimulationSession s, int actor, ActionStep step)=>s.Inventory.Drop(actor, step.Target);
}
