namespace LivingWorld.Simulation;
public sealed class DecisionSystem : ISimulationSystem
{
    public string Name=>"utility and planner";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var processed=0;
        var all=e.Store<DecisionComponent>().Ids();
        foreach (var actor in all.Where(id=>id>s.DecisionCursor).Concat(all.Where(id=>id<=s.DecisionCursor)).ToArray())
        {
            var decision=e.Get<DecisionComponent>(actor);
            if (session.Interactions.Waiting(actor)||!e.Get<HealthComponent>(actor).Alive||s.Clock.Tick<decision.NextDecision||(s.Clock.Tick+actor)%3!=0)continue;
            if (s.Clock.Age(e.Get<IdentityComponent>(actor).BirthDate)<3)continue;
            if (processed>=session.MaxDecisionsPerTick)break;
            processed++;
            s.DecisionCursor=actor;
            var context=ContextBuilder.Create(session,actor,findBuildSite:false,findFarmSite:false);
            var desires=session.Evaluators.SelectMany(x=>x.Evaluate(context)).Where(x=>x.Urgency>.025f).OrderByDescending(x=>x.Urgency).Take(6).ToArray();
            var urgent=desires.FirstOrDefault();
            var emergency=urgent is not null&&urgent.Fact is "fed" or "hydrated"&&urgent.Urgency>=3;
            var urgentPhysiology=urgent is not null&&urgent.Fact is "fed" or "hydrated" or "warm" or "cooled"&&urgent.Urgency>=3;
            if(decision.Plan.Count>0&&(urgent is null||urgent.Fact==decision.DesiredFact||(!urgentPhysiology&&urgent.Urgency<5)))
            {
                decision.NextDecision=s.Clock.Tick+12;
                continue;
            }
            var needsFarmSite=desires.Any(x=>x.Fact=="farm_plotted");
            context=ContextBuilder.Create(session,actor,findBuildSite:true,findFarmSite:needsFarmSite);
            var options=session.Actions.All.Where(a=>!a.RequiresWork||context.CanWork).SelectMany(a=>a.Options(context)).ToList();
            foreach (var option in options)
            {
                var memory=context.Known.FirstOrDefault(o=>o.Entity==option.Step.Target&&option.Step.Target!=0);
                if (memory is not null)option.Risk+=(1-memory.Confidence)*25;
            }
            var candidates=new List<(DesiredState Desire, PlanResult Plan, float Score)>();
            var bestScore=float.NegativeInfinity;
            foreach (var desired in desires)
            {
                // Score cannot exceed urgency: all plan costs are nonnegative.
                // Lower-ranked needs cannot beat an already found candidate even with a free plan.
                if (desired.Urgency<bestScore)continue;
                var plan=session.Planner.Find(context, options, desired);
                if (plan is null||plan.Steps.Count==0)continue;
                var score=desired.Urgency/(1+plan.Cost*.012f);
                bestScore=Math.Max(bestScore,score);
                candidates.Add((desired, plan, score));
            }
            var ranked=candidates.OrderByDescending(x=>x.Score).ToArray();
            if(emergency&&urgent is not null&&!ranked.Any(x=>x.Desire.Fact==urgent.Fact))
            {
                session.CancelPlan(actor);
                Explore(session,actor,decision,"срочно ищет "+urgent.Reason,12);
                decision.NextDecision=s.Clock.Tick+6;
                continue;
            }
            decision.Alternatives=ranked.Take(6).Select(x=>new DecisionScore(x.Desire.Reason, session.Actions[x.Plan.Steps[0].Action].Label, x.Score, x.Plan.Cost, $"важность {x.Desire.Urgency:F2}; шагов {x.Plan.Steps.Count}")).ToList();
            if (ranked.Length>0)
            {
                var best=ranked[0];
                if (decision.Plan.Count>0&&best.Score<decision.ChosenScore*1.4f)
                {
                    decision.NextDecision=s.Clock.Tick+12;
                    continue;
                }
                session.CancelPlan(actor);
                decision.Plan=best.Plan.Steps;
                decision.Motive=best.Desire.Reason;
                decision.DesiredFact=best.Desire.Fact;
                decision.ChosenScore=best.Score;
            }
            else if(decision.Plan.Count==0)Explore(session,actor,decision);
            decision.NextDecision=s.Clock.Tick+12;
        }
        return processed;
    }
    private static void Explore(SimulationSession session,int actor,DecisionComponent decision,string motive="исследует известный край местности",int radius=7)
    {
        var s=session.State;
        var e=s.Entities;
        var center=e.Get<PositionComponent>(actor).Tile;
        var memory=e.Get<MemoryComponent>(actor);
        var random=s.Random.Stream("exploration:"+actor);
        var candidates=new List<GridPoint>();
        for (var i=0; i<12; i++)
        {
            var p=center+new GridPoint(random.Range(-radius,radius+1),random.Range(-radius,radius+1));
            // Exploration picks a visible frontier, never a hidden resource destination.
            if(s.Map.Walkable(p)&&PerceptionSystem.LineOfSight(s.Map,center,p)&&
                (center.Distance(p)<=1||session.Pathfinder.Find(center,p,0,256) is not null))candidates.Add(p);
        }
        var destination=candidates.OrderBy(p=>memory.Visited.Count(old=>old.Distance(p)<5)).ThenByDescending(p=>p.Distance(center)).FirstOrDefault(center);
        if (destination!=center&&ActionRules.CanMove(s, actor))decision.Plan=[new()
        {
            Action="move", Position=destination, Range=0
        }, new()
        {
            Action="observe", Position=destination, Duration=6
        }];
        else decision.Plan=[new()
        {
            Action="observe", Position=center, Duration=10
        }];
        decision.Motive=motive;
        decision.DesiredFact="explore";
        decision.ChosenScore=.01f;
    }
}
