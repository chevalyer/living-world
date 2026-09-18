namespace LivingWorld.Simulation;
public sealed class ObservationLearningSystem
{
    public ObservationLearningSystem(SimulationSession session)
    {
        session.Events.Subscribe<ActionCompletedEvent>(ev=>Learn(session, ev));
    }
    private static void Learn(SimulationSession session, ActionCompletedEvent ev)
    {
        if (ev.Step.Action is not("eat" or "build" or "craft" or "sow" or "chop"))return;
        var e=session.State.Entities;
        var position=e.Get<PositionComponent>(ev.Actor).Tile;
        foreach (var observer in session.Spatial.Query(position, 5).ToArray())
        {
            if (observer==ev.Actor||e.Try<HealthComponent>(observer) is not
            {
                Alive:true
            }
            ||!e.Has<KnowledgeComponent>(observer))continue;
            var p=e.Get<PositionComponent>(observer).Tile;
            if (p.Distance(position)>5||!PerceptionSystem.LineOfSight(session.State.Map, p, position)||session.State.Clock.Age(e.Get<IdentityComponent>(observer).BirthDate)<3)continue;
            var knowledge=e.Get<KnowledgeComponent>(observer);
            if (ev.Step.Action=="eat")
            {
                foreach (var plant in session.Definitions.Plants.Values.Where(d=>d.Product==ev.Step.Argument))knowledge.EdiblePlants.Add(plant.Id);
                knowledge.Facts.Add("forage");
            }
            else
            {
                var fact=ev.Step.Action switch
                {
                    "build"=>"building", "sow"=>"farming", "chop"=>"woodworking", _=>session.Definitions.Recipes[ev.Step.Argument].Knowledge
                };
                var skills=e.Get<SkillsComponent>(observer);
                skills.Experience[fact]=skills.Experience.GetValueOrDefault(fact)+.5f;
                if (skills.Experience[fact]>=5)knowledge.Facts.Add(fact);
            }
        }
    }
}
