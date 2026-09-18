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
        if(!c.SocialReady)yield break;
        var usable=c.Inventory.Where(x=>c.Definitions.Items[x.Item.Definition].Calories<=0||x.Item.Freshness>=.1f);
        foreach(var other in c.OfKind("npc").Where(o=>o.Age>=18).Take(4))
        foreach(var offer in usable.GroupBy(x=>x.Item.Definition).Where(g=>g.Count()>1).Take(4))
        foreach(var wanted in other.Items.Where(x=>c.Definitions.Items[x.Key].Calories>100&&x.Value>1).Take(2))
        {
            if(wanted.Key==offer.Key)continue;
            var otherNeeds=new NeedsComponent { Hunger=other.Need,Thirst=other.Thirst,Fatigue=other.Fatigue };
            var actorWants=PersonalValuation.Value(c.Definitions.Items[wanted.Key],c.Needs,c.Inventory.Count(x=>x.Item.Definition==wanted.Key));
            var actorGives=PersonalValuation.Value(c.Definitions.Items[offer.Key],c.Needs,offer.Count());
            var targetWants=PersonalValuation.Value(c.Definitions.Items[offer.Key],otherNeeds,other.Items.GetValueOrDefault(offer.Key));
            var targetGives=PersonalValuation.Value(c.Definitions.Items[wanted.Key],otherNeeds,wanted.Value);
            if(actorWants<actorGives*.9f||targetWants<targetGives*.9f)continue;
            var op=Option(other.Position,other.Entity,offer.Key+"|"+wanted.Key,8);
            op.Requires=[new(Item(offer.Key),1)];
            op.Effects=[new(Item(offer.Key),-1),new(Item(wanted.Key),1),new("traded",1,true)];
            yield return op;
        }
    }
    public override bool Execute(SimulationSession s,int actor,ActionStep step)
    {
        if(!ActionRules.CanWork(s.State,actor))return false;
        var parts=step.Argument.Split('|');
        if(parts.Length!=2||!ActionRules.Adult(s.State,step.Target))return false;
        var give=FindItem(s,actor,parts[0]);
        var receive=FindItem(s,step.Target,parts[1]);
        if(give==0||receive==0)return false;
        var e=s.State.Entities;
        var giveItem=e.Get<ItemComponent>(give);
        var receiveItem=e.Get<ItemComponent>(receive);
        if((s.Definitions.Items[giveItem.Definition].Calories>0&&giveItem.Freshness<.1f)||
           (s.Definitions.Items[receiveItem.Definition].Calories>0&&receiveItem.Freshness<.1f))return false;
        var a=e.Get<NeedsComponent>(actor);
        var b=e.Get<NeedsComponent>(step.Target);
        var actorWants=PersonalValuation.Value(s.Definitions.Items[parts[1]],receiveItem,a,s.Inventory.Count(actor,parts[1]));
        var actorGives=PersonalValuation.Value(s.Definitions.Items[parts[0]],giveItem,a,s.Inventory.Count(actor,parts[0]));
        var targetWants=PersonalValuation.Value(s.Definitions.Items[parts[0]],giveItem,b,s.Inventory.Count(step.Target,parts[0]));
        var targetGives=PersonalValuation.Value(s.Definitions.Items[parts[1]],receiveItem,b,s.Inventory.Count(step.Target,parts[1]));
        if(actorWants<actorGives*.9f||targetWants<targetGives*.9f)return false;
        if(!s.Inventory.CanCarry(actor,parts[1])||!s.Inventory.CanCarry(step.Target,parts[0]))return false;
        if(e.Get<PositionComponent>(actor).Tile.Distance(e.Get<PositionComponent>(step.Target).Tile)>1)return false;
        if(!s.Inventory.Transfer(actor,step.Target,give,"trade"))return false;
        if(!s.Inventory.Transfer(step.Target,actor,receive,"trade"))throw new InvalidOperationException("Atomic trade preflight failed.");
        e.Get<DecisionComponent>(actor).LastSocialTick=s.State.Clock.Tick;
        s.Events.Publish(new SocialEvent(actor,step.Target,"trade",.04f));
        return true;
    }
}
