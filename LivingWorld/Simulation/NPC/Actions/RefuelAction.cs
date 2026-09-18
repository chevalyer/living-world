namespace LivingWorld.Simulation;
public sealed class RefuelAction : SimAction
{
    public override string Id=>"refuel";
    public override string Label=>"подкладывает дрова";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var o in c.OfKind("fire").Where(o=>o.Quantity<120)) foreach (var fuel in c.Definitions.Items.Values.Where(d=>d.Tags.Contains("fuel", StringComparer.Ordinal)))
        {
            var op=Option(o.Position, o.Entity, fuel.Id, 4);
            op.Requires=[new(Item(fuel.Id), 1)];
            op.Effects=[new(Item(fuel.Id), -1), new("warm", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var fire=s.State.Entities.Try<FireComponent>(step.Target);
        if (fire is null||!s.Inventory.Consume(actor, step.Argument, 1))return false;
        var d=s.Definitions.Items[step.Argument];
        fire.FuelMinutes+=d.Mass*s.Definitions.Materials[d.Material].Flammability*240;
        return true;
    }
}
