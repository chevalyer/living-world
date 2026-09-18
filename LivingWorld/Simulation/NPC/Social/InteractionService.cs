namespace LivingWorld.Simulation;
public sealed class InteractionService(WorldState state)
{
    public bool Waiting(int actor)
    {
        var interaction=state.Entities.Try<InteractionComponent>(actor);
        return interaction is not null&&interaction.Until>state.Clock.Tick&&interaction.Initiator!=actor;
    }
    public bool Begin(int actor, int target, float minutes, string action="")
    {
        var e=state.Entities;
        if (!e.Has<NeedsComponent>(target)||e.Try<HealthComponent>(target) is not { Alive:true })return false;
        var active=e.Try<InteractionComponent>(target);
        if (active is not null&&active.Until>state.Clock.Tick&&active.Partner!=actor)return false;
        var needs=e.Get<NeedsComponent>(target);
        var emergencyCare=action is "care" or "give";
        if (!emergencyCare&&(needs.Hunger>.9f||needs.Thirst>.9f))return false;
        var current=e.Get<DecisionComponent>(target).Plan.FirstOrDefault()?.Action;
        if (current=="sleep"&&e.Get<DecisionComponent>(target).RemainingMinutes>0&&!emergencyCare)return false;
        var until=state.Clock.Tick+(long)Math.Ceiling(minutes)+2;
        e.Set(actor,new InteractionComponent { Partner=target,Initiator=actor,Until=until });
        e.Set(target,new InteractionComponent { Partner=actor,Initiator=actor,Until=until });
        return true;
    }
    public void End(int actor)
    {
        var interaction=state.Entities.Try<InteractionComponent>(actor);
        if (interaction is null)return;
        var other=state.Entities.Try<InteractionComponent>(interaction.Partner);
        if (other?.Partner==actor)other.Until=0;
        interaction.Until=0;
    }
}
