namespace LivingWorld.Simulation;
[Component("Plant")]
public sealed class PlantComponent
{
    public string Definition { get; set; } = "";
    public float Growth { get; set; } = .8f;
    public float Health { get; set; } = 1;
    public float Yield { get; set; }
    public float AgeDays { get; set; }
    public float ReproductionProgress { get; set; }
    public int Cultivator { get; set; }
}
