namespace LivingWorld.Simulation;
public sealed record SettlementSummary(int Homes, int Members, float FoodCalories, string[] Specializations);
public static class SettlementAnalyzer
{
    public static SettlementSummary Describe(SimulationSession session)
    {
        var s=session.State;
        var e=s.Entities;
        var skills=e.Store<SkillsComponent>().All.Where(x=>e.Get<HealthComponent>(x.Key).Alive).Select(x=>x.Value.Experience.OrderByDescending(y=>y.Value).FirstOrDefault().Key).Where(x=>x is not null).Distinct().ToArray();
        var food=e.Store<ItemComponent>().All.Sum(x=>session.Definitions.Items[x.Value.Definition].Calories*x.Value.Freshness);
        return new(e.Store<ConstructionComponent>().All.Count(x=>x.Value.Finished), s.Population, food, skills!);
    }
}
