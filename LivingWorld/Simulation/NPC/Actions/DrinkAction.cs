namespace LivingWorld.Simulation;
public sealed class DrinkAction : SimAction
{
    public override string Id=>"drink";
    public override string Label=>"пьет";
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach(var facility in c.Facilities("water"))
        {
            var op=Option(facility.Position,facility.Entity,duration:2);
            op.Effects=[new("hydrated",1,true)];
            op.Cost=2;
            yield return op;
        }
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
        if(step.Target!=0)
        {
            reason="источник воды недоступен";
            return FacilityService.CanUse(s,actor,step.Target)&&FacilityService.HasCapability(s,step.Target,"water")&&
                s.State.Entities.Get<PositionComponent>(actor).Tile.Distance(s.State.Entities.Get<PositionComponent>(step.Target).Tile)<=1;
        }
        if(!s.State.Map.Contains(step.Position)){reason="вода исчезла";return false;}
        var t=s.State.Map[step.Position];
        if(t.Water is not(WaterKind.River or WaterKind.Lake)){reason="вода исчезла";return false;}
        if(t.Ice>=.15f){reason="вода замерзла";return false;}
        return true;
    }

    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!CanExecute(s,actor,step,out _))return false;
        var needs=s.State.Entities.Get<NeedsComponent>(actor);
        needs.Thirst=Math.Max(0,needs.Thirst-(step.Target!=0?.9f:.75f));
        return true;
    }
}
