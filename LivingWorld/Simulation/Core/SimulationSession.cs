namespace LivingWorld.Simulation;
public sealed class SimulationSession
{
    public WorldState State
    { get; }
    public DefinitionCatalog Definitions
    { get; }
    public EventBus Events
    { get; } = new();
    public SpatialIndex Spatial
    { get; }
    public Pathfinder Pathfinder
    { get; }
    public InventoryService Inventory
    { get; }
    public InteractionService Interactions
    { get; }
    public ReservationService Reservations
    { get; } = new();
    public ActionRegistry Actions
    { get; } = new();
    public ForwardPlanner Planner
    { get; } = new();
    public List<ISituationEvaluator> Evaluators
    { get; } = [new PhysiologyEvaluator(),new SocialEvaluator(),new ResourceEvaluator(),new FacilityEvaluator()];
    public List<ISimulationSystem> Systems
    { get; } = [];
    public Dictionary<string, SystemProfile> Profiles
    { get; } = new(StringComparer.Ordinal);
    public bool RoomsDirty { get; set; } = true;
    public int PathsThisTick { get; set; }
    public int MaxPathsPerTick { get; set; } = 6;
    public int MaxDecisionsPerTick { get; set; } = 12;
    public Dictionary<string,int> FailureReasons { get; } = new(StringComparer.Ordinal);
    public Dictionary<string,int> FailureActions { get; } = new(StringComparer.Ordinal);
    public double LastTickMilliseconds
    { get; private set; }
    public SimulationSession(WorldState state, DefinitionCatalog definitions)
    {
        State=state;
        Definitions=definitions;
        Interactions=new(state);
        Spatial=new(state.Map);
        Spatial.Rebuild(state.Entities);
        Pathfinder=new(state.Map);
        Inventory=new(state, definitions, Spatial, Events);
        ISimAction[] actions=[new MoveAction(), new ObserveAction(), new HarvestAction(), new PickUpAction(), new EatAction(), new DrinkAction(), new BreakIceAction(), new SleepAction(), new WearAction(), new RemoveClothingAction(), new CoolDownAction(), new ChopAction(), new MineAction(), new CraftAction(), new LightFireAction(), new WarmUpAction(), new RefuelAction(),new BuildFacilityAction(),new PlanBuildingAction(), new BuildAction(), new RepairAction(), new DropAction(), new PlanFarmAction(), new TillAction(), new SowAction(), new TalkAction(), new GiveAction(), new TradeAction(), new TeachAction(), new PartnerAction(), new StartFamilyAction(), new CareAction(), new CheckChildAction(), new PlayAction(), new TakeAction(), new DepositAction(), new DiscardSpoiledAction()];
        foreach (var action in actions)Actions.Add(action);
        _=new SkillSystem(this);
        _=new RelationshipSystem(this);
        _=new ObservationLearningSystem(this);
        // Fixed, explicit order. Simulation correctness never depends on Godot node order or wall-clock budgets.
        Systems.AddRange([new WeatherSystem(), new WaterSystem(), new RoomSystem(), new RoomTemperatureSystem(), new FireSystem(), new PlantSystem(), new ItemConditionSystem(), new NeedsSystem(), new TemperatureSystem(), new PerceptionSystem(), new DecisionSystem(), new ActionExecutionSystem(), new MovementSystem(), new FamilySystem(), new LifeSystem(), new TrafficSystem()]);
        foreach (var system in Systems)Profiles[system.Name]=new()
        {
            Name=system.Name
        };
    }
    public void Step(int ticks=1)
    {
        if (ticks<0)throw new ArgumentOutOfRangeException(nameof(ticks));
        for (var i=0; i<ticks; i++)
        {
            var started=System.Diagnostics.Stopwatch.GetTimestamp();
            State.Clock.Advance();
            PathsThisTick=0;
            Reservations.Expire(State.Clock.Tick);
            foreach (var system in Systems)
            {
                if (State.Clock.Tick%system.Interval!=0)continue;
                var before=System.Diagnostics.Stopwatch.GetTimestamp();
                var entities=system.Update(this);
                var elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(before).TotalMilliseconds;
                if (!Profiles.TryGetValue(system.Name, out var profile))Profiles[system.Name]=profile=new()
                {
                    Name=system.Name
                };
                profile.LastMilliseconds=elapsed;
                profile.MaxMilliseconds=Math.Max(profile.MaxMilliseconds, elapsed);
                profile.Calls++;
                profile.AverageMilliseconds+=(elapsed-profile.AverageMilliseconds)/profile.Calls;
                profile.Entities=entities;
            }
            Events.Flush();
            LastTickMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }
    public void CancelPlan(int actor)
    {
        Interactions.End(actor);
        var decision=State.Entities.Get<DecisionComponent>(actor);
        decision.Plan.Clear();
        decision.RemainingMinutes=-1;
        var movement=State.Entities.Get<MovementComponent>(actor);
        movement.Path.Clear();
        movement.Destination=null;
        movement.Progress=0;
        Reservations.Release(actor);
    }
    public void Replan(int actor,string reason,int delay=3)
    {
        CancelPlan(actor);
        var decision=State.Entities.Get<DecisionComponent>(actor);
        decision.LastFailure=reason;
        decision.LastFailureTick=State.Clock.Tick;
        decision.NextDecision=State.Clock.Tick+delay;
    }
    public void FailPlan(int actor,string reason)
    {
        var decision=State.Entities.Get<DecisionComponent>(actor);
        var step=decision.Plan.FirstOrDefault();
        if(step is not null)
        {
            if(reason is "маршрут недоступен" or "цель вне досягаемости")
            {
                var intended=step.Action=="move"?step.Argument:step.Action;
                var kind=intended switch
                {
                    "drink" or "break_ice"=>"water",
                    "warm_up" or "sleep"=>"shelter",
                    "harvest" or "chop"=>"plant",
                    "mine"=>"resource",
                    "pickup"=>"item",
                    _=>""
                };
                foreach(var memory in State.Entities.Get<MemoryComponent>(actor).Observations.Where(o=>
                    step.Target!=0?o.Entity==step.Target:o.Position==step.Position&&(kind.Length==0||o.Kind==kind)))
                    memory.UnreachableUntil=State.Clock.Tick+360;
            }
            Events.Publish(new ActionFailedEvent(actor,step.Action,reason));
            var failureAction=step.Action=="move"&&step.Argument.Length>0?"move:"+step.Argument:step.Action;
            FailureActions[failureAction]=FailureActions.GetValueOrDefault(failureAction)+1;
        }
        FailureReasons[reason]=FailureReasons.GetValueOrDefault(reason)+1;
        CancelPlan(actor);
        decision.LastFailure=reason;
        decision.LastFailureTick=State.Clock.Tick;
        decision.Failures++;
        decision.NextDecision=State.Clock.Tick+6;
    }
}
