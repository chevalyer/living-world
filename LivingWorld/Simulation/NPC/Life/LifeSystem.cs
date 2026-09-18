namespace LivingWorld.Simulation;
public sealed class LifeSystem : ISimulationSystem
{
    public string Name=>"life and death";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var count=0;
        foreach (var id in e.Store<HealthComponent>().Ids())
        {
            var h=e.Get<HealthComponent>(id);
            if (!h.Alive)continue;
            count++;
            var identity=e.Get<IdentityComponent>(id);
            var age=s.Clock.Age(identity.BirthDate);
            if (age>=identity.LifespanYears)h.Value=0;
            if (age<18)e.Get<BodyComponent>(id).Mass=3.5f+age*3.1f;
            if (h.Value>0)continue;
            var needs=e.Get<NeedsComponent>(id);
            var temp=e.Get<ThermalComponent>(id).Temperature;
            var reason=age>=identity.LifespanYears?"старость":needs.Thirst>.98f?"обезвоживание":needs.Hunger>.98f?"голод":temp<35?"переохлаждение":"истощение";
            h.Value=0;
            h.Alive=false;
            h.DeathReason=reason;
            session.CancelPlan(id);
            foreach (var item in e.Get<InventoryComponent>(id).Items.ToArray())
            {
                session.Inventory.Drop(id, item);
                e.Get<OwnershipComponent>(item).Owner=0;
            }
            var family=e.Get<FamilyComponent>(id);
            if (e.Try<FamilyComponent>(family.Partner) is { } partner)partner.Partner=0;
            family.Partner=0;
            family.PregnancyDueTick=null;
            foreach (var owned in e.Store<OwnershipComponent>().All.Where(x=>x.Value.Owner==id))owned.Value.Owner=family.Children.FirstOrDefault(child=>e.Get<HealthComponent>(child).Alive);
            s.Log(identity.FullName+": "+reason+".");
            session.Events.Publish(new NpcDiedEvent(id, reason));
        }
        return count;
    }
}
