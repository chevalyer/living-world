namespace LivingWorld.Simulation;
public static class PersonalValuation
{
    public static float Value(ItemDefinition d, NeedsComponent needs, int owned) => (d.Mass*.2f+d.Calories/300f*(1+needs.Hunger*6)+d.Coverage*d.Thickness+d.Tools.Values.Sum()*3)/(1+owned*.3f);
}
