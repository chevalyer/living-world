namespace LivingWorld.Simulation;
public sealed class SocialEvaluator : ISituationEvaluator
{
    public IEnumerable<DesiredState> Evaluate(PlanningContext c)
    {
        if (c.SocialReady)yield return new("socialized","общение",MathF.Pow(c.Needs.Loneliness,2)*c.Personality.Sociability*4);

        foreach (var child in c.OfKind("npc").Where(o=>c.Family.Children.Contains(o.Entity)&&o.Age<18))
        {
            var need=Math.Max(child.Need,Math.Max(child.Thirst,child.Fatigue*.7f));
            if(c.CanWork&&need>.25f)
                yield return new($"cared:{child.Entity}","забота о ребенке",need*need*12);
            if(child.Age<3&&c.Tick-child.SeenTick>60)
            {
                var stale=Math.Clamp((c.Tick-child.SeenTick)/180f,0,8);
                yield return new($"checked:{child.Entity}","проверить маленького ребенка",1+stale);
            }
        }

        if (c.CanWork&&c.SocialReady)
        {
            foreach(var friend in c.OfKind("npc").Where(o=>o.Need>.65f).OrderByDescending(o=>o.Need).Take(3))
                yield return new($"helped:{friend.Entity}","помочь голодному",friend.Need*c.Personality.Generosity*1.5f);
            yield return new("taught","передать опыт",c.Personality.Generosity*.22f);
            yield return new("traded","выгодный обмен",c.Needs.Hunger*.4f);
            if (c.Family.Partner==0)yield return new("partnered","взаимная привязанность",c.Personality.FamilyDesire*.5f);
            else if (!c.Family.PregnancyDueTick.HasValue&&c.Tick-c.Family.LastBirthTick>1440*500)
                yield return new("family","готовность к ребенку",c.Personality.FamilyDesire*.4f);
        }
        if (c.Age>=3&&c.Age<18)yield return new("played","интерес к окружению",c.Personality.Curiosity*.4f);
    }
}
