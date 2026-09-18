namespace LivingWorld.Simulation;
public sealed class PhysiologyEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        yield return new("fed", "голод", MathF.Pow(c.Needs.Hunger, 3)*12);
        yield return new("hydrated", "жажда", MathF.Pow(c.Needs.Thirst, 3)*15);
        yield return new("rested", "усталость", MathF.Pow(c.Needs.Fatigue, 3)*10);
        var cold=Math.Max(0, 36.6f-c.Temperature)+Math.Max(0, 12-c.Air-c.Insulation*12)*.035f;
        yield return new("warm", "потеря тепла", cold*cold*8);
        yield return new("cooled", "перегрев", MathF.Pow(Math.Max(0, c.Temperature-37.6f), 2)*8);
        if (c.CanWork&&((!c.Sheltered&&c.Family.HomeProject==0)||c.Known.Any(o=>o.Kind=="project"&&o.Entity==c.Family.HomeProject&&o.Quantity>0))) yield return new("housing_progress", "защита от холода и непогоды", .5f+c.Personality.Caution*.8f+Math.Max(0, 15-c.Air)*.07f);
    }
}
