namespace LivingWorld.Simulation;
public sealed class GiveAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"give";
    public override string Label=>"делится едой";
    public override bool RequiresWork=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (c.Needs.Hunger>.5f||c.Personality.Generosity<.35f||!c.SocialReady)yield break;
        foreach (var person in c.OfKind("npc").Where(o=>o.Need>.6f)) foreach (var d in c.Definitions.Items.Values.Where(d=>d.Calories>100))
        {
            var op=Option(person.Position, person.Entity, d.Id, 3);
            op.Requires=[new(Item(d.Id), 2)];
            op.Effects=[new(Item(d.Id), -1), new("helped", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var id=FindItem(s, actor, step.Argument);
        if (id==0||s.State.Entities.Get<NeedsComponent>(actor).Hunger>.5f)return false;
        if (!s.Inventory.Transfer(actor, step.Target, id))return false;
        s.State.Entities.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        return true;
    }
}
