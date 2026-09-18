namespace LivingWorld.Simulation;
public readonly record struct GridPoint(int X, int Y)
{
    public int Distance(GridPoint other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    public static GridPoint operator +(GridPoint a, GridPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static readonly GridPoint[] Cardinal = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
}
