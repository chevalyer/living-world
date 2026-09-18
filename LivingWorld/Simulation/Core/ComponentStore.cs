namespace LivingWorld.Simulation;
public interface IComponentStore
{
    Type ComponentType
    { get; }
    IEnumerable<KeyValuePair<int, object>> Entries();
    void SetObject(int id, object component);
    void Remove(int id);
    bool Has(int id);
}
public sealed class ComponentStore<T> : IComponentStore where T : class
{
    private readonly SortedDictionary<int, T> _data = [];
    public Type ComponentType => typeof(T);
    public IEnumerable<KeyValuePair<int, T>> All => _data;
    public int Count => _data.Count;
    public bool Has(int id) => _data.ContainsKey(id);
    public T Get(int id) => _data[id];
    public T? Try(int id) => _data.GetValueOrDefault(id);
    public void Set(int id, T value) => _data[id] = value;
    public void Remove(int id) => _data.Remove(id);
    public int[] Ids() => _data.Keys.ToArray();
    public IEnumerable<KeyValuePair<int, object>> Entries() => _data.Select(x => new KeyValuePair<int, object>(x.Key, x.Value));
    public void SetObject(int id, object component) => Set(id, (T)component);
}
