namespace LivingWorld.Simulation;
public static class ValueNoise
{
    private static float Hash(int x, int y, ulong seed)
    {
        ulong n = seed ^ ((ulong)(uint)x * 0x9E3779B185EBCA87UL) ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL);
        n ^= n >> 30;
        n *= 0xBF58476D1CE4E5B9UL;
        n ^= n >> 27;
        n *= 0x94D049BB133111EBUL;
        n ^= n >> 31;
        return (n >> 40) / 16777215f;
    }
    public static float Sample(float x, float y, ulong seed)
    {
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var sx = x - ix;
        var sy = y - iy;
        sx = sx * sx * (3 - 2 * sx);
        sy = sy * sy * (3 - 2 * sy);
        var a = Hash(ix, iy, seed);
        var b = Hash(ix+1, iy, seed);
        var c = Hash(ix, iy+1, seed);
        var d = Hash(ix+1, iy+1, seed);
        return (a+(b-a)*sx)*(1-sy) + (c+(d-c)*sx)*sy;
    }
    public static float Fractal(float x, float y, ulong seed)
    {
        float value=0, weight=0, amplitude=1;
        for (var i=0; i<5; i++)
        {
            value+=Sample(x, y, seed+(ulong)i*71)*amplitude;
            weight+=amplitude;
            amplitude*=.5f;
            x*=2;
            y*=2;
        }
        return value/weight;
    }
}
