namespace LivingWorld.Simulation;
public sealed class TradeAction : SimAction
{
    public override bool EngagesTarget=>true;
    public override string Id=>"trade";
    public override string Label=>"обменивается";
    public override bool RequiresWork=>true;
    public override bool Exclusive=>true;
    public override IEnumerable<ActionOption> Options(PlanningContext c)
    {
        if (!c.SocialReady)yield break;
        foreach (var other in c.OfKind("npc").Where(o=>o.Age>=18).Take(4)) foreach (var offer in c.Inventory.GroupBy(x=>x.Item.Definition).Where(g=>g.Count()>1).Take(4)) foreach (var wanted in other.Items.Where(x=>c.Definitions.Items[x.Key].Calories>100&&x.Value>1).Take(2))
        {
            if (wanted.Key==offer.Key)continue;
            var op=Option(other.Position, other.Entity, offer.Key+"|"+wanted.Key, 8);
            op.Requires=[new(Item(offer.Key), 1)];
            op.Effects=[new(Item(offer.Key), -1), new(Item(wanted.Key), 1), new("traded", 1, true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s, int actor, ActionStep step)
    {
        if (!ActionRules.CanWork(s.State, actor))return false;
        var parts=step.Argument.Split('|');
        if (parts.Length!=2||!ActionRules.Adult(s.State, step.Target))return false;
        var give=FindItem(s, actor, parts[0]);
        var receive=FindItem(s, step.Target, parts[1]);
        if (give==0||receive==0)return false;
        var a=s.State.Entities.Get<NeedsComponent>(actor);
        var b=s.State.Entities.Get<NeedsComponent>(step.Target);
        float Price(int who, NeedsComponent needs, string item)=>PersonalValuation.Value(s.Definitions.Items[item], needs, s.Inventory.Count(who, item));
        if (Price(actor, a, parts[1])<Price(actor, a, parts[0])*.9f||Price(step.Target, b, parts[0])<Price(step.Target, b, parts[1])*.9f)return false;
        // Preflight both capacities and range before either transfer; failed exchanges leave inventories intact.
        if (!s.Inventory.CanCarry(actor, parts[1])||!s.Inventory.CanCarry(step.Target, parts[0]))return false;
        if (s.State.Entities.Get<PositionComponent>(actor).Tile.Distance(s.State.Entities.Get<PositionComponent>(step.Target).Tile)>1)return false;
        if (!s.Inventory.Transfer(actor, step.Target, give, "trade"))return false;
        if (!s.Inventory.Transfer(step.Target, actor, receive, "trade"))throw new InvalidOperationException("Atomic trade preflight failed.");
        s.State.Entities.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        s.Events.Publish(new SocialEvent(actor, step.Target, "trade", .04f));
        return true;
    }
}
