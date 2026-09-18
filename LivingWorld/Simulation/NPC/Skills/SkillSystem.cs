namespace LivingWorld.Simulation;
public sealed class SkillSystem
{
    public SkillSystem(SimulationSession session)
    {
        session.Events.Subscribe<SkillUsedEvent>(ev=>
        {
            var skills=session.State.Entities.Try<SkillsComponent>(ev.Actor); if (skills is null)return; var age=session.State.Clock.Age(session.State.Entities.Get<IdentityComponent>(ev.Actor).BirthDate); skills.Experience[ev.Skill]=skills.Experience.GetValueOrDefault(ev.Skill)+ev.Experience*(age<18?1.3f:1);
        });
    }
}
