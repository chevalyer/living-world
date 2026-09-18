namespace LivingWorld.Simulation;
public sealed class RemoveClothingAction : SimAction
{
    public override string Id=>"remove_clothing";
    public override string Label=>"снимает лишнюю одежду";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var (id, item) in c.Inventory.Where(x=>c.Worn.Contains(x.Id)))
        {
            var op=Option(c.Position, id, item.Definition, 2, local:true);
            op.Effects=[new("cooled", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)=>s.State.Entities.Get<EquipmentComponent>(actor).Items.Remove(step.Target);
}
