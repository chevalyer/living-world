namespace LivingWorld.Simulation;
public static class ActionRules
{
    public static bool Adult(WorldState s, int id)=>s.Entities.Try<IdentityComponent>(id) is { } identity&&s.Clock.Age(identity.BirthDate)>=18;
    public static bool CanWork(WorldState s, int id)
    {
        var body=s.Entities.Try<BodyComponent>(id);
        return Adult(s, id)&&body is
        {
            Strength:>0, Mobility:>0
        }
        &&s.Entities.Try<HealthComponent>(id) is
        {
            Alive:true, Value:>10
        };
    }
    public static bool CanMove(WorldState s, int id)=>s.Clock.Age(s.Entities.Get<IdentityComponent>(id).BirthDate)>=3&&s.Entities.Get<BodyComponent>(id).Mobility>0&&s.Entities.Get<HealthComponent>(id).Alive;
}
