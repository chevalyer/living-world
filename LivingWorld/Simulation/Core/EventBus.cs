namespace LivingWorld.Simulation;
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly Queue<object> _pending = [];
    public void Subscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers)) _handlers[typeof(T)] = handlers = [];
        handlers.Add(handler);
    }
    public void Publish<T>(T value) where T : notnull => _pending.Enqueue(value);
    public void Flush()
    {
        var count = 0;
        while (_pending.TryDequeue(out var value))
        {
            if (++count > 10000) throw new InvalidOperationException("Event loop exceeded its safety budget.");
            if (_handlers.TryGetValue(value.GetType(), out var handlers)) foreach (var handler in handlers) handler.DynamicInvoke(value);
        }
    }
}
