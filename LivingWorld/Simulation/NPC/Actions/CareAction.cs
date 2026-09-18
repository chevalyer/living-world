namespace LivingWorld.Simulation;
public sealed class CareAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"care";
    public override string Label=>"заботится о ребенке";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        foreach (var child in c.OfKind("npc").Where(o=>c.Family.Children.Contains(o.Entity)&&o.Age<18&&Math.Max(o.Need,Math.Max(o.Thirst,o.Fatigue*.7f))>.25f))
        foreach (var d in c.Definitions.Items.Values.Where(d=>d.Calories>0))
        {
            var op=Option(child.Position,child.Entity,d.Id,10);
            op.Requires=[new(Item(d.Id),1)];
            op.Effects=[new(Item(d.Id),-1),new($"cared:{child.Entity}",1,true)];
            op.Cost=10+Math.Max(0,300-d.Calories)/100f;
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if (!ActionRules.CanWork(s.State,actor))return false;
        var e=s.State.Entities;
        if (!e.Get<FamilyComponent>(actor).Children.Contains(step.Target)||!e.Get<HealthComponent>(step.Target).Alive)return false;
        if (e.Get<PositionComponent>(actor).Tile.Distance(e.Get<PositionComponent>(step.Target).Tile)>1)return false;
        var id=FindItem(s,actor,step.Argument);
        if (id==0)return false;
        var food=e.Get<ItemComponent>(id);
        if(food.Freshness<.1f)return false;
        var d=s.Definitions.Items[food.Definition];
        var child=e.Get<NeedsComponent>(step.Target);
        child.Hunger=Math.Max(0,child.Hunger-d.Calories*food.Freshness/1800);
        child.Thirst=Math.Max(0,child.Thirst-Math.Max(.18f,.35f+d.Water*.5f));
        child.Fatigue=Math.Max(0,child.Fatigue-.5f);
        if(s.State.Clock.Age(e.Get<IdentityComponent>(step.Target).BirthDate)<3)
            e.Get<NeedsComponent>(actor).Thirst=Math.Min(1,e.Get<NeedsComponent>(actor).Thirst+.18f);
        var infantThermal=e.Get<ThermalComponent>(step.Target);
        var parentThermal=e.Get<ThermalComponent>(actor);
        var heat=Math.Max(0,parentThermal.Temperature-infantThermal.Temperature)*.35f;
        infantThermal.Temperature+=heat;
        parentThermal.Temperature-=heat*.08f;
        s.Inventory.Destroy(id);
        s.Events.Publish(new SocialEvent(actor,step.Target,"care",.05f));
        return true;
    }
}
