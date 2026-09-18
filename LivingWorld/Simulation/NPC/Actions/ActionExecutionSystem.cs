namespace LivingWorld.Simulation;
public sealed class ActionExecutionSystem : ISimulationSystem
{
    public string Name=>"actions";
    public int Interval=>1;
    public int Update(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var count=0;
        foreach (var actor in e.Store<DecisionComponent>().Ids())
        {
            var decision=e.Get<DecisionComponent>(actor);
            if (session.Interactions.Waiting(actor)||!e.Get<HealthComponent>(actor).Alive||decision.Plan.Count==0)continue;
            count++;
            var step=decision.Plan[0];
            if (!session.Actions.Has(step.Action))
            {
                session.FailPlan(actor, "неизвестное действие");
                continue;
            }
            var action=session.Actions[step.Action];
            if (step.Local)step.Position=e.Get<PositionComponent>(actor).Tile;
            if (step.Action=="move")
            {
                var movement=e.Get<MovementComponent>(actor);
                var p=e.Get<PositionComponent>(actor).Tile;
                // A moving NPC may have left its observed location. We learn that only on arrival.
                if (p.Distance(step.Position)<=step.Range)
                {
                    Complete(session, actor);
                    continue;
                }
                if (movement.Path.Count==0||movement.NavigationRevision!=s.Map.NavigationRevision)
                {
                    if (session.PathsThisTick>=session.MaxPathsPerTick)continue;
                    session.PathsThisTick++;
                    var path=session.Pathfinder.Find(p, step.Position, step.Range);
                    if (path is null)
                    {
                        session.FailPlan(actor, "маршрут недоступен");
                        continue;
                    }
                    movement.Path=path;
                    movement.Destination=step.Position;
                    movement.NavigationRevision=s.Map.NavigationRevision;
                }
                continue;
            }
            if (!action.CanExecute(session, actor, step, out var reason))
            {
                session.FailPlan(actor, reason);
                continue;
            }
            if (e.Get<PositionComponent>(actor).Tile.Distance(step.Position)>step.Range)
            {
                session.FailPlan(actor, "цель вне досягаемости");
                continue;
            }
            if (action.Exclusive&&!session.Reservations.Claim(step.Target, actor, s.Clock.Tick))
            {
                var other=session.Reservations.Entries.GetValueOrDefault(step.Target)?.Actor??0;
                if (other!=0&&s.Clock.Tick-decision.LastFailureTick>120)session.Events.Publish(new SocialEvent(actor, other, "resource_conflict", .03f));
                session.FailPlan(actor, "ресурс уже занят");
                continue;
            }
            if (decision.RemainingMinutes<0)
            {
                if (action.EngagesTarget&&!session.Interactions.Begin(actor, step.Target, step.Duration))
                {
                    session.FailPlan(actor, "собеседник занят или отказался");
                    continue;
                }
                decision.RemainingMinutes=Math.Max(1, step.Duration);
            }
            decision.RemainingMinutes-=1;
            if (decision.RemainingMinutes>0)continue;
            // Validate again immediately before committing effects: no delayed duplicate harvests or trades.
            if (action.CanExecute(session, actor, step, out reason)&&action.Execute(session, actor, step))Complete(session, actor);
            else session.FailPlan(actor, string.IsNullOrEmpty(reason)?"обстановка изменилась":reason);
        }
        return count;
    }
    private static void Complete(SimulationSession session, int actor)
    {
        session.Interactions.End(actor);
        var decision=session.State.Entities.Get<DecisionComponent>(actor);
        session.Events.Publish(new ActionCompletedEvent(actor, decision.Plan[0] with
        {
        }));
        decision.Plan.RemoveAt(0);
        decision.RemainingMinutes=-1;
        session.Reservations.Release(actor);
        if (decision.Plan.Count==0)decision.NextDecision=session.State.Clock.Tick;
    }
}
