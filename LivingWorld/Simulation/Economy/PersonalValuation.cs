namespace LivingWorld.Simulation;
public static class PersonalValuation
{
    public static float Value(ItemDefinition d,NeedsComponent needs,int owned)=>
        (d.Mass*.2f+d.Calories/300f*(1+needs.Hunger*6)+d.Coverage*d.Thickness+d.Tools.Values.Sum()*3)/(1+owned*.3f);

    public static float Value(ItemDefinition d,ItemComponent item,NeedsComponent needs,int owned)
    {
        var value=Value(d,needs,owned);
        if(d.Calories>0)value*=Math.Clamp(item.Freshness,0,1);
        if(d.Tools.Count>0||d.Slots.Length>0)
            value*=.2f+.8f*Math.Clamp(item.Durability/Math.Max(1,d.Durability),0,1);
        return value*Math.Clamp(item.Quality,.4f,1.5f);
    }
}
