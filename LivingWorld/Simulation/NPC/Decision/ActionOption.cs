namespace LivingWorld.Simulation;
public sealed class ActionOption
{
    public ActionStep Step { get; set; } = new();
    public List<FactRequirement> Requires { get; set; } = [];
    public List<FactEffect> Effects { get; set; } = [];
    public float Cost { get; set; } = 1;
    public float Risk { get; set; }
    public int MassDelta { get; set; }
    public int VolumeDelta { get; set; }
    public string Skill { get; set; } = "";
    public bool GroundedAtActor { get; set; }
    public bool Applies(PlanningState state)=>Requires.All(r=>state.Get(r.Fact)>=r.Minimum)&&state.Get("capacity.mass")>=Math.Max(0, MassDelta)&&state.Get("capacity.volume")>=Math.Max(0, VolumeDelta);
    public PlanningState Apply(PlanningState state)
    {
        var next=state.Clone();
        if (!GroundedAtActor)next.Position=Step.Position;
        next.Facts["capacity.mass"]=state.Get("capacity.mass")-MassDelta;
        next.Facts["capacity.volume"]=state.Get("capacity.volume")-VolumeDelta;
        foreach (var effect in Effects)next.Facts[effect.Fact]=effect.Set?effect.Amount:next.Get(effect.Fact)+effect.Amount;
        return next;
    }
}
