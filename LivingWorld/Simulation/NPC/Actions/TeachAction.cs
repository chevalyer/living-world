namespace LivingWorld.Simulation;
public sealed class TeachAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"teach";
    public override string Label=>"передает опыт";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if(!c.SocialReady)yield break;
        foreach(var person in c.OfKind("npc").Where(o=>o.Age>=3&&o.Need<=.9f&&o.Thirst<=.9f))
        foreach(var skill in c.Skills.Experience.Where(x=>x.Value>person.Skills.GetValueOrDefault(x.Key)+15).Take(2))
        {
            var op=Option(person.Position,person.Entity,skill.Key,30);
            op.Effects=[new("taught",1,true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor))return false;
        var e=s.State.Entities;
        if(!e.Has<SkillsComponent>(step.Target)||!e.Get<HealthComponent>(step.Target).Alive)return false;
        if(e.Get<PositionComponent>(actor).Tile.Distance(e.Get<PositionComponent>(step.Target).Tile)>1)return false;
        var teacher=e.Get<SkillsComponent>(actor);
        var student=e.Get<SkillsComponent>(step.Target);
        var gap=teacher.Experience.GetValueOrDefault(step.Argument)-student.Experience.GetValueOrDefault(step.Argument);
        if(gap<=0)return false;
        var attention=1-e.Get<NeedsComponent>(step.Target).Fatigue;
        var trust=RelationshipSystem.Get(s.State,step.Target,actor).Trust;
        s.Events.Publish(new SkillUsedEvent(step.Target,step.Argument,Math.Min(gap,step.Duration*.25f*attention*(.5f+trust))));
        var knowledge=e.Get<KnowledgeComponent>(actor);
        var learned=e.Get<KnowledgeComponent>(step.Target);
        if(knowledge.Facts.Contains(step.Argument))learned.Facts.Add(step.Argument);
        if(step.Argument=="foraging")
        {
            var plant=knowledge.EdiblePlants.Where(p=>!learned.EdiblePlants.Contains(p)).OrderBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            if(plant is not null)learned.EdiblePlants.Add(plant);
        }
        e.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        s.Events.Publish(new SocialEvent(actor,step.Target,"teaching",.05f));
        return true;
    }
}
