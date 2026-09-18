namespace LivingWorld.Simulation;
public sealed class WorldMap
{
    public const int ChunkSize = 16;
    public int Width { get; set; }
    public int Height { get; set; }
    public Tile[] Tiles { get; set; } = [];
    public int NavigationRevision { get; set; }
    public int VisualRevision { get; set; }
    private int[] _chunkVisualRevisions = [];

    public int ChunkVisualRevision(int key)
    {
        EnsureVisualRevisions();
        return _chunkVisualRevisions[key];
    }

    public void MarkVisualDirty(GridPoint point)
    {
        EnsureVisualRevisions();
        _chunkVisualRevisions[ChunkKey(point)] = ++VisualRevision;
    }

    private void EnsureVisualRevisions()
    {
        var count = ((Width + ChunkSize - 1) / ChunkSize) * ((Height + ChunkSize - 1) / ChunkSize);
        if (_chunkVisualRevisions.Length != count) _chunkVisualRevisions = new int[count];
    }

    // Display precision only. Physical values and simulation frequency remain unchanged.
    public static int SurfaceVisualKey(Tile tile) =>
        (int)(tile.Snow * 31) | ((int)(Math.Min(tile.Ice, .2f) * 160) << 6) |
        ((int)(Math.Min(tile.Traffic, 64) / 4) << 12) | (tile.Ice >= .15f ? 1 << 18 : 0);
    public WorldMap()
    {
    }
    public WorldMap(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = Enumerable.Range(0, width * height).Select(_ => new Tile()).ToArray();
    }
    public int Index(GridPoint p) => p.Y * Width + p.X;
    public GridPoint Point(int index) => new(index % Width, index / Width);
    public bool Contains(GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
    public Tile this[GridPoint p] => Tiles[Index(p)];
    public IEnumerable<GridPoint> Neighbors(GridPoint p)
    {
        foreach (var d in GridPoint.Cardinal)
        {
            var next = p + d;
            if (Contains(next)) yield return next;
        }
    }
    public bool Walkable(GridPoint p) => Contains(p) && this[p].Wall == 0 && this[p].Biome != Biome.Alpine && (this[p].Water == WaterKind.None || (this[p].Water != WaterKind.Ocean && this[p].Ice >= .15f));
    public float Cost(GridPoint p)
    {
        var t = this[p];
        return Math.Max(.55f, 1.2f - Math.Min(t.Traffic, 60) * .01f) + t.Snow * 2 + (t.Biome == Biome.Marsh ? .8f : 0) + (t.Biome == Biome.Mountain ? 1 : 0);
    }
    public int ChunkKey(GridPoint p) => (p.Y / ChunkSize) * ((Width + ChunkSize - 1) / ChunkSize) + p.X / ChunkSize;
}
