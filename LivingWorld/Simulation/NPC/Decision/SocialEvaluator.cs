namespace LivingWorld.Simulation;
public sealed class SocialEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        if (c.SocialReady)yield return new("socialized", "общение", MathF.Pow(c.Needs.Loneliness, 2)*c.Personality.Sociability*4);
        var child=c.OfKind("npc").Where(o=>c.Family.Children.Contains(o.Entity)).Select(o=>o.Need).DefaultIfEmpty(0).Max();
        if (c.CanWork&&child>.3f)yield return new("cared", "забота о ребенке", child*child*10);
        if (c.CanWork&&c.SocialReady)
        {
            var friend=c.OfKind("npc").Where(o=>o.Need>.65f).Select(o=>o.Need).DefaultIfEmpty(0).Max();
            if (friend>0)yield return new("helped", "помочь голодному", friend*c.Personality.Generosity*1.5f);
            yield return new("taught", "передать опыт", c.Personality.Generosity*.22f);
            yield return new("traded", "выгодный обмен", c.Needs.Hunger*.4f);
            if (c.Family.Partner==0)yield return new("partnered", "взаимная привязанность", c.Personality.FamilyDesire*.5f);
            else if (!c.Family.PregnancyDueTick.HasValue&&c.Tick-c.Family.LastBirthTick>1440*500) yield return new("family", "готовность к ребенку", c.Personality.FamilyDesire*.4f);
        }
        if (c.Age>=3&&c.Age<18)yield return new("played", "интерес к окружению", c.Personality.Curiosity*.4f);
    }
}
