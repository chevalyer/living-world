namespace LivingWorld.Simulation;
public sealed class ActionRegistry
{
    private readonly Dictionary<string, ISimAction> _actions=new(StringComparer.Ordinal);
    public IEnumerable<ISimAction> All=>_actions.Values;
    public ISimAction this[string id]=>_actions[id];
    public bool Has(string id)=>_actions.ContainsKey(id);
    public void Add(ISimAction action)
    {
        if (!_actions.TryAdd(action.Id, action))throw new InvalidOperationException($"Duplicate action {action.Id}");
    }
}
