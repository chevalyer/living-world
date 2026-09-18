namespace LivingWorld.Simulation;
public sealed class LightFireAction : SimAction
{
    public override string Id=>"light_fire";
    public override string Label=>"разводит костер";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.Knowledge.Facts.Contains("fire"))yield break;
        foreach (var fuel in c.Definitions.Items.Values.Where(d=>d.Tags.Contains("fuel", StringComparer.Ordinal)))
        {
            var op=Option(c.Position, argument:fuel.Id, duration:8, local:true);
            op.Requires=[new(Item(fuel.Id), 1)];
            op.Effects=[new(Item(fuel.Id), -1), new("warm", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var d=s.Definitions.Items[step.Argument];
        if (!d.Tags.Contains("fuel", StringComparer.Ordinal)||!s.Inventory.Consume(actor, d.Id, 1))return false;
        var p=s.State.Entities.Get<PositionComponent>(actor).Tile;
        var id=s.State.Entities.Create();
        s.State.Entities.Set(id, new PositionComponent
        {
            Tile=p
        });
        s.State.Entities.Set(id, new FireComponent
        {
            FuelMinutes=d.Mass*s.Definitions.Materials[d.Material].Flammability*240
        });
        s.Spatial.Add(id, p);
        return true;
    }
}
