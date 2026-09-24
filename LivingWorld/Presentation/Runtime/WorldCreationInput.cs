using System.Globalization;

namespace LivingWorld.Presentation;

public static class WorldCreationInput
{
    public static int Population(string text,int fallback)
    {
        var value=fallback;
        if(int.TryParse(text.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var parsed)||
           int.TryParse(text.Trim(),NumberStyles.Integer,CultureInfo.CurrentCulture,out parsed))
            value=parsed;
        return Math.Clamp(value,1,500);
    }
}
