namespace LivingWorld.Simulation;
public sealed class PlanningState
{
    public Dictionary<string, int> Facts { get; set; } = new(StringComparer.Ordinal);
    public GridPoint Position { get; set; }
    public int Get(string fact)=>Facts.GetValueOrDefault(fact);
    public PlanningState Clone()=>new()
    {
        Position=Position, Facts=new(Facts, StringComparer.Ordinal)
    };
    public string Key()=>Position.X+","+Position.Y+"|"+string.Join(";", Facts.Where(x=>x.Value!=0).OrderBy(x=>x.Key, StringComparer.Ordinal).Select(x=>x.Key+":"+x.Value));
}
public sealed record FactRequirement(string Fact, int Minimum);
public sealed record FactEffect(string Fact, int Amount, bool Set=false);
