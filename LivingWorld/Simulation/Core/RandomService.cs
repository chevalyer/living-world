namespace LivingWorld.Simulation;
public sealed class RandomService(int seed)
{
    private readonly SortedDictionary<string, DeterministicRandom> _streams = new(StringComparer.Ordinal);
    public DeterministicRandom Stream(string name)
    {
        if (!_streams.TryGetValue(name, out var value)) _streams[name] = value = new DeterministicRandom(Hash(seed, name));
        return value;
    }
    public Dictionary<string, ulong> Capture() => _streams.ToDictionary(x => x.Key, x => x.Value.State);
    public void Restore(Dictionary<string, ulong> states)
    {
        _streams.Clear();
        foreach (var (key, value) in states.OrderBy(x => x.Key, StringComparer.Ordinal)) _streams[key] = new(value);
    }
    public static ulong Hash(int seed, string name)
    {
        ulong hash = 14695981039346656037UL ^ (uint)seed;
        foreach (var c in name)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
