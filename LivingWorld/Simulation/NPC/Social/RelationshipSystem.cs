namespace LivingWorld.Simulation;
public sealed class RelationshipSystem
{
    public RelationshipSystem(SimulationSession session)
    {
        session.Events.Subscribe<SocialEvent>(ev=>Apply(session, ev));
        session.Events.Subscribe<ItemTransferredEvent>(ev=>
        {
            if (ev.Reason=="gift")session.Events.Publish(new SocialEvent(ev.From, ev.To, "gift", .09f));
        });
    }
    public static Relationship Get(WorldState s, int from, int to)
    {
        var relationships=s.Entities.Get<RelationshipComponent>(from).People;
        if (!relationships.TryGetValue(to, out var value))relationships[to]=value=new();
        return value;
    }
    private static void Apply(SimulationSession session, SocialEvent ev)
    {
        var s=session.State;
        if (!s.Entities.Has<RelationshipComponent>(ev.Actor)||!s.Entities.Has<RelationshipComponent>(ev.Target))return;
        var from=Get(s, ev.Actor, ev.Target);
        var to=Get(s, ev.Target, ev.Actor);
        var a=s.Entities.Get<PersonalityComponent>(ev.Actor);
        var b=s.Entities.Get<PersonalityComponent>(ev.Target);
        if (ev.Kind=="resource_conflict")
        {
            from.Irritation=Math.Min(1, from.Irritation+ev.Value*(1+a.Aggression));
            from.Trust=Math.Max(0, from.Trust-ev.Value);
            to.Irritation=Math.Min(1, to.Irritation+ev.Value*b.Aggression);
        }
        else
        {
            from.Trust=Math.Min(1, from.Trust+ev.Value*(.5f+a.Generosity));
            to.Trust=Math.Min(1, to.Trust+ev.Value*(1+b.Generosity));
            from.Affection=Math.Min(1, from.Affection+ev.Value*a.Sociability);
            to.Affection=Math.Min(1, to.Affection+ev.Value*b.Sociability);
            from.Familiarity=Math.Min(1, from.Familiarity+.04f);
            to.Familiarity=Math.Min(1, to.Familiarity+.04f);
            if (ev.Kind=="teaching")to.Respect=Math.Min(1, to.Respect+.06f);
            if (ev.Kind=="gift")to.Debt+=1;
            if (ActionRules.Adult(s, ev.Actor)&&ActionRules.Adult(s, ev.Target)&&!FamilyRules.Related(s, ev.Actor, ev.Target))
            {
                from.RomanticInterest=Math.Min(1, from.RomanticInterest+ev.Value*a.FamilyDesire);
                to.RomanticInterest=Math.Min(1, to.RomanticInterest+ev.Value*b.FamilyDesire);
            }
            from.Irritation=Math.Max(0, from.Irritation-.02f);
            to.Irritation=Math.Max(0, to.Irritation-.02f);
        }
        from.LastInteraction=to.LastInteraction=s.Clock.Tick;
        s.Entities.Get<MemoryComponent>(ev.Target).Events.Add(new(s.Clock.Tick, $"{s.Entities.Get<IdentityComponent>(ev.Actor).FullName}: {ev.Kind}"));
    }
}
