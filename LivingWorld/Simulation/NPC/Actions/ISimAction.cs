namespace LivingWorld.Simulation;
public interface ISimAction
{
    string Id
    { get; }
    string Label
    { get; }
    bool RequiresWork
    { get; }
    bool Exclusive
    { get; }
    bool EngagesTarget
    { get; }
    IEnumerable<ActionOption> Options(PlanningContext context);
    bool CanExecute(SimulationSession session, int actor, ActionStep step, out string reason);
    bool Execute(SimulationSession session, int actor, ActionStep step);
}
public abstract class SimAction : ISimAction
{
    public abstract string Id
    { get; }
    public abstract string Label
    { get; }
    public virtual bool RequiresWork=>false;
    public virtual bool Exclusive=>false;
    public virtual bool EngagesTarget=>false;
    public abstract IEnumerable<ActionOption> Options(PlanningContext c);
    public virtual bool CanExecute(SimulationSession s, int actor, ActionStep step, out string reason)
    {
        reason="";
        if (RequiresWork&&!ActionRules.CanWork(s.State, actor))
        {
            reason="недостаточно физических возможностей или возраста";
            return false;
        }
        return true;
    }
    public abstract bool Execute(SimulationSession s, int actor, ActionStep step);
    protected ActionOption Option(GridPoint p, int target=0, string argument="", float duration=1, int range=1, bool local=false) =>new()
    {
        Step=new()
        {
            Action=Id, Position=p, Target=target, Argument=argument, Duration=duration, Range=range, Local=local
        }, Cost=duration, GroundedAtActor=local
    };
    protected static string Item(string id)=>PlanningContext.ItemFact(id);
    protected static string Source(int id)=>"source:"+id;
    protected static int FindItem(SimulationSession s,int actor,string definition)=>s.Inventory.Items(actor)
        .Where(id=>s.State.Entities.Get<ItemComponent>(id).Definition==definition)
        .OrderByDescending(id=>s.State.Entities.Get<ItemComponent>(id).Freshness)
        .ThenByDescending(id=>s.State.Entities.Get<ItemComponent>(id).Durability)
        .FirstOrDefault();
}
