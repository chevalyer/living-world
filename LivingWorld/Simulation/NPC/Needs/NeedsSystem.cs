namespace LivingWorld.Simulation;
public sealed class NeedsSystem : ISimulationSystem
{
    public string Name=>"needs";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var count=0;
        foreach (var (id, n) in s.Entities.Store<NeedsComponent>().All)
        {
            if (!s.Entities.Get<HealthComponent>(id).Alive)continue;
            count++;
            var active=s.Entities.Get<MovementComponent>(id).Path.Count>0;
            var pregnant=s.Entities.Get<FamilyComponent>(id).PregnancyDueTick.HasValue;
            n.Hunger=Math.Min(1, n.Hunger+(active?1.15f:1)*(pregnant?1.15f:1)/3600);
            n.Thirst=Math.Min(1, n.Thirst+1/2100f);
            n.Fatigue=Math.Min(1, n.Fatigue+1/1600f);
            n.Loneliness=Math.Min(1, n.Loneliness+1/4000f);
            var health=s.Entities.Get<HealthComponent>(id);
            if (n.Hunger>.995f)health.Value-=.025f;
            if (n.Thirst>.995f)health.Value-=.08f;
            if (n.Fatigue<.5f && n.Hunger<.6f && n.Thirst<.6f)health.Value=Math.Min(100, health.Value+.008f);
        }
        return count;
    }
}
