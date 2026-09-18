namespace LivingWorld.Simulation;
public sealed class WearAction : SimAction
{
    public override string Id=>"wear";
    public override string Label=>"надевает одежду";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var d in c.Definitions.Items.Values.Where(d=>d.Slots.Length>0))
        {
            if (c.Inventory.Any(x=>x.Item.Definition==d.Id&&c.Worn.Contains(x.Id)))continue;
            var old=c.Inventory.Where(x=>c.Worn.Contains(x.Id)&&c.Definitions.Items[x.Item.Definition].Slots.Intersect(d.Slots, StringComparer.Ordinal).Any()).Sum(x=>ClothingPhysics.Insulation(x.Item, c.Definitions.Items[x.Item.Definition], c.Definitions.Materials[c.Definitions.Items[x.Item.Definition].Material]));
            var insulation=c.Definitions.Materials[d.Material].Insulation*d.Thickness*d.Coverage;
            if (insulation<=old+.03f)continue;
            var op=Option(c.Position, argument:d.Id, duration:2, local:true);
            op.Requires=[new(Item(d.Id), 1)];
            op.Effects=[new("warm", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        var id=FindItem(s, actor, step.Argument);
        return id!=0&&s.Inventory.Wear(actor, id);
    }
}
