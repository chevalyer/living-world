namespace LivingWorld.Simulation;
public sealed class BreakIceAction : SimAction
{
    public override string Id=>"break_ice";
    public override string Label=>"делает прорубь и пьет";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var o in c.OfKind("water").Where(o=>o.Quantity==0).Take(4))
        {
            var op=Option(o.Position,duration:18);
            op.Requires=[new("tool:mine",1)];
            op.Effects=[new("hydrated",1,true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor))return false;
        if(!s.State.Map.Contains(step.Position))return false;
        var tile=s.State.Map[step.Position];
        if(tile.Water is not(WaterKind.River or WaterKind.Lake)||s.Inventory.Tool(actor,"mine")<=0)return false;
        tile.Ice=0;
        s.State.Map.NavigationRevision++;
        s.State.Map.MarkVisualDirty(step.Position);
        s.Inventory.WearTool(actor,"mine",2);
        s.State.Entities.Get<NeedsComponent>(actor).Thirst=.1f;
        return true;
    }
}
