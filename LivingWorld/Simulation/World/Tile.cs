namespace LivingWorld.Simulation;
public enum WaterKind
{
    None, Ocean, River, Lake
}
public enum Biome
{
    Ocean, Beach, Meadow, Forest, Marsh, Mountain, Alpine
}
public sealed class Tile
{
    public float Height { get; set; }
    public float DrainageHeight { get; set; }
    public int Downstream { get; set; } = -1;
    public float Flow { get; set; } = 1;
    public WaterKind Water { get; set; }
    public Biome Biome { get; set; }
    public float Moisture { get; set; }
    public float Fertility { get; set; }
    public float BaseTemperature { get; set; }
    public float Ore { get; set; }
    public float Snow { get; set; }
    public float Ice { get; set; }
    public float WaterTemperature { get; set; } = 8;
    public float Traffic { get; set; }
    public int Wall { get; set; }
    public int Door { get; set; }
    public int Roof { get; set; }
    public int Floor { get; set; }
    public int Room { get; set; }
    public int FarmPlot { get; set; }
    public bool Tilled { get; set; }
}
