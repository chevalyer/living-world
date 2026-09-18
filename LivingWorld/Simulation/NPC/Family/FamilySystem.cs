namespace LivingWorld.Simulation;
public sealed class FamilySystem : ISimulationSystem
{
    public string Name=>"pregnancy and births";
    public int Interval=>60;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var count=0;
        foreach (var mother in e.Store<FamilyComponent>().Ids())
        {
            var family=e.Get<FamilyComponent>(mother);
            if (!e.Get<HealthComponent>(mother).Alive||family.PregnancyDueTick is not
            {
            }
            due||s.Clock.Tick<due)continue;
            var father=family.PregnancyFather;
            var random=s.Random.Stream("birth");
            var sex=random.Chance(.5)?"male":"female";
            var p=e.Get<PositionComponent>(mother).Tile;
            var child=new NpcFactory(session.Definitions).Spawn(s, p, sex, s.Clock.Now);
            var cf=e.Get<FamilyComponent>(child);
            cf.Mother=mother;
            cf.Father=father;
            cf.HomeProject=family.HomeProject;
            e.Get<IdentityComponent>(child).Surname=e.Get<IdentityComponent>(mother).Surname;
            var fatherStrength=e.Try<BodyComponent>(father)?.Strength??.7f;
            e.Get<BodyComponent>(child).Strength=Math.Clamp((e.Get<BodyComponent>(mother).Strength+fatherStrength)*.5f+random.Range(-.1f, .1f), .1f, 1.3f);
            e.Get<BodyComponent>(child).Mass=3.5f;
            family.Children.Add(child);
            if (e.Try<FamilyComponent>(father) is { } ff)ff.Children.Add(child);
            family.PregnancyDueTick=null;
            family.LastBirthTick=s.Clock.Tick;
            if (e.Try<FamilyComponent>(father) is { } dad)dad.LastBirthTick=s.Clock.Tick;
            foreach (var parent in new[]
            {
                mother, father
            }.Where(e.Exists))
            {
                var r=RelationshipSystem.Get(s, parent, child);
                r.Attachment=1;
                r.Affection=.9f;
                r.Trust=.9f;
            }
            session.Spatial.Add(child,p);
            foreach(var parent in new[]{mother,father}.Where(id=>e.Exists(id)&&e.Has<MemoryComponent>(id)))
            {
                var needs=e.Get<NeedsComponent>(child);
                e.Get<MemoryComponent>(parent).Observations.Add(new Observation
                {
                    Kind="npc",Entity=child,Position=p,Need=needs.Hunger,Thirst=needs.Thirst,Fatigue=needs.Fatigue,
                    Health=e.Get<HealthComponent>(child).Value,Age=0,Sex=e.Get<IdentityComponent>(child).Sex,
                    Partner=0,Pregnant=false,Room=s.Map[p].Room,Sheltered=EnvironmentQueries.Sheltered(s,p),SocialAvailable=true,SeenTick=s.Clock.Tick
                });
            }
            s.Log($"Родился ребенок: {e.Get<IdentityComponent>(child).FullName}.");
            session.Events.Publish(new ChildBornEvent(child, mother, father));
            count++;
        }
        return count;
    }
}
