namespace LivingWorld.Simulation;
public sealed class EntityRegistry
{
    private readonly Dictionary<Type, IComponentStore> _stores = [];
    private readonly SortedSet<int> _alive = [];
    public int NextId { get; set; } = 1;
    public IEnumerable<int> All => _alive;
    public IEnumerable<IComponentStore> Stores => _stores.Values.OrderBy(x => x.ComponentType.Name, StringComparer.Ordinal);
    public int Count => _alive.Count;
    public int Create()
    {
        var id = NextId++;
        _alive.Add(id);
        return id;
    }
    public void RestoreIdentity(int id)
    {
        _alive.Add(id);
        NextId = Math.Max(NextId, id + 1);
    }
    public bool Exists(int id) => _alive.Contains(id);
    public ComponentStore<T> Store<T>() where T : class
    {
        if (!_stores.TryGetValue(typeof(T), out var store)) _stores[typeof(T)] = store = new ComponentStore<T>();
        return (ComponentStore<T>)store;
    }
    public IComponentStore Store(Type type)
    {
        if (!_stores.TryGetValue(type, out var store)) _stores[type] = store = (IComponentStore)Activator.CreateInstance(typeof(ComponentStore<>).MakeGenericType(type))!;
        return store;
    }
    public void Set<T>(int id, T value) where T : class
    {
        if (!Exists(id)) throw new InvalidOperationException($"Missing entity {id}");
        Store<T>().Set(id, value);
    }
    public T Get<T>(int id) where T : class => Store<T>().Get(id);
    public T? Try<T>(int id) where T : class => Store<T>().Try(id);
    public bool Has<T>(int id) where T : class => Store<T>().Has(id);
    public void Remove(int id)
    {
        _alive.Remove(id);
        foreach (var store in _stores.Values) store.Remove(id);
    }
}
