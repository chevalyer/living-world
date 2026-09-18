namespace LivingWorld.Simulation;
public sealed record SkillUsedEvent(int Actor, string Skill, float Experience);
public sealed record SocialEvent(int Actor, int Target, string Kind, float Value);
public sealed record ItemTransferredEvent(int Item, int From, int To, string Reason);
public sealed record NpcDiedEvent(int Npc, string Reason);
public sealed record ChildBornEvent(int Child, int Mother, int Father);
public sealed record ActionFailedEvent(int Actor, string Action, string Reason);
public sealed record ActionCompletedEvent(int Actor, ActionStep Step);
