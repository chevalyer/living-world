namespace LivingWorld.Simulation;
public sealed class DrinkAction : SimAction
{
    public override string Id=>"drink";
    public override string Label=>"пьет";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var o in c.OfKind("water").Where(o=>o.Quantity>0).Take(4))
        {
            var op=Option(o.Position,duration:3);
            op.Effects=[new("hydrated",1,true)];
            yield return op;
        }
    }
    public override bool CanExecute(SimulationSession s,int actor,ActionStep step,out string reason)
    {
        reason="";
        if(!s.State.Map.Contains(step.Position)){reason="вода исчезла";return false;}
        var t=s.State.Map[step.Position];
        if(t.Water is not(WaterKind.River or WaterKind.Lake)){reason="вода исчезла";return false;}
        if(t.Ice>=.15f){reason="вода замерзла";return false;}
        return true;
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        var t=s.State.Map[step.Position];
        if(t.Water is not(WaterKind.River or WaterKind.Lake)||t.Ice>=.15f)return false;
        var needs=s.State.Entities.Get<NeedsComponent>(actor);
        needs.Thirst=Math.Max(0,needs.Thirst-.75f);
        return true;
    }
}
