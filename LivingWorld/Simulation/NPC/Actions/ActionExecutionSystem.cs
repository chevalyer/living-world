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
                if(step.Target!=0&&session.Actions.Has(step.Argument))
                {
                    var intended=session.Actions[step.Argument];
                    GridPoint? dynamicTarget=null;
                    if(step.Argument=="build"&&e.Try<ConstructionComponent>(step.Target) is { Finished:false } project)
                        dynamicTarget=project.Elements[project.Completed].Position;
                    else if(intended.EngagesTarget&&e.Try<PositionComponent>(step.Target) is { } targetPosition)
                        dynamicTarget=targetPosition.Tile;
                    if(dynamicTarget.HasValue&&dynamicTarget.Value!=step.Position)
                    {
                        step.Position=dynamicTarget.Value;
                        movement.Path.Clear();
                        movement.Destination=null;
                    }
                    if((intended.Exclusive||intended.EngagesTarget)&&!session.Reservations.Claim(step.Target,actor,s.Clock.Tick))
                    {
                        session.Replan(actor,intended.EngagesTarget?"собеседник уже занят":"ресурс уже занят");
                        continue;
                    }
                }
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
            if(action.EngagesTarget&&session.Reservations.Entries.TryGetValue(actor,out var incoming)&&incoming.Actor!=actor&&incoming.Expires>s.Clock.Tick)
            {
                session.Replan(actor,"ожидает собеседника");
                continue;
            }
            if(step.Action=="build"&&e.Try<ConstructionComponent>(step.Target) is { } projectState)
            {
                if(projectState.Finished)
                {
                    session.Replan(actor,"стройка уже завершена");
                    continue;
                }
                var work=projectState.Elements[projectState.Completed].Position;
                step.Position=work;
                if(e.Get<PositionComponent>(actor).Tile.Distance(work)>step.Range)
                {
                    decision.Plan.Insert(0,new ActionStep { Action="move",Position=work,Range=step.Range,Target=step.Target,Argument="build",Duration=1 });
                    continue;
                }
            }
            else if(action.EngagesTarget&&step.Target!=0&&e.Try<PositionComponent>(step.Target) is { } liveTarget)
            {
                step.Position=liveTarget.Tile;
                if(e.Get<PositionComponent>(actor).Tile.Distance(step.Position)>step.Range)
                {
                    decision.Plan.Insert(0,new ActionStep { Action="move",Position=step.Position,Range=step.Range,Target=step.Target,Argument=step.Action,Duration=1 });
                    continue;
                }
            }
            if(!action.CanExecute(session,actor,step,out var reason))
            {
                if(action.EngagesTarget||reason is "предмет уже забрали" or "место больше не подходит")
                    session.Replan(actor,reason,12);
                else session.FailPlan(actor,reason);
                continue;
            }
            if (e.Get<PositionComponent>(actor).Tile.Distance(step.Position)>step.Range)
            {
                session.FailPlan(actor, "цель вне досягаемости");
                continue;
            }
            if((action.Exclusive||action.EngagesTarget)&&!session.Reservations.Claim(step.Target,actor,s.Clock.Tick))
            {
                var other=session.Reservations.Entries.GetValueOrDefault(step.Target)?.Actor??0;
                if(other!=0&&s.Clock.Tick-decision.LastFailureTick>120&&!action.EngagesTarget)
                    session.Events.Publish(new SocialEvent(actor,other,"resource_conflict",.03f));
                session.Replan(actor,action.EngagesTarget?"собеседник уже занят":"ресурс уже занят",6);
                continue;
            }
            if (decision.RemainingMinutes<0)
            {
                if(action.EngagesTarget&&!session.Interactions.Begin(actor,step.Target,step.Duration,step.Action))
                {
                    session.Replan(actor,"собеседник занят или отказался",18);
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
        var completed=decision.Plan[0];
        session.Events.Publish(new ActionCompletedEvent(actor,completed with { }));
        decision.Plan.RemoveAt(0);
        decision.RemainingMinutes=-1;
        if(completed.Action!="move")session.Reservations.Release(actor);
        if (decision.Plan.Count==0)decision.NextDecision=session.State.Clock.Tick;
    }
}
