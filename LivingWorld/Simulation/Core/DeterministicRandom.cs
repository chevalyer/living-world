namespace LivingWorld.Simulation;
public sealed class DeterministicRandom(ulong state)
{
    public ulong State { get; set; } = state;
    public ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15UL;
        var z = State;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    public float Range(float min, float max) => min + (max - min) * (float)NextDouble();
    public int Range(int min, int max) => min + (int)(NextUInt64() % (uint)(max - min));
    public bool Chance(double probability) => NextDouble() < probability;
}
