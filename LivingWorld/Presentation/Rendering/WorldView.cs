namespace LivingWorld.Presentation;

using System.Diagnostics;

public partial class WorldView : Node2D
{
    public const int TileSize = 16;
    public GameRoot Game { get; set; } = null!;
    public int CurrentLod { get; private set; }
    public int VisibleChunks { get; private set; }
    public int VisibleObjects { get; private set; }
    private sealed record CachedTexture(ImageTexture Texture, long Revision, long Used);
    private readonly Dictionary<(int Chunk, int Lod), CachedTexture> _textures = new();
    private readonly Dictionary<int, Vector2> _previousPeople = new();
    private readonly List<RenderChunk> _visible = new();
    private readonly List<RenderEntity> _entities = new();
    private readonly List<RenderPerson> _people = new();
    private RenderSnapshot? _previous;
    private long _frame, _publicationTime;
    private static readonly Color[] Outfits = [new("#e2d5b5"), new("#8da2a0"), new("#a9937e"), new("#cec6aa"), new("#c1a48e"), new("#97a080"), new("#b2a1a6"), new("#a8b8a0")];

    public void ResetTerrain()
    {
        foreach (var cached in _textures.Values) cached.Texture.Dispose();
        _textures.Clear();
        _previousPeople.Clear();
        _previous = null;
    }
    public override void _ExitTree() => ResetTerrain();

    public override void _Draw()
    {
        if (Game.Snapshot is not { } world) return;
        _frame++;
        if (_previous is null || _previous.Sequence != world.Sequence || _previous.Generation != world.Generation)
        {
            _previousPeople.Clear();
            if (_previous?.Generation == world.Generation)
                foreach (var person in _previous.People) _previousPeople[person.Id] = ToVector(person.Tile);
            _previous = world;
            _publicationTime = Stopwatch.GetTimestamp();
        }
        // Use the actual canvas transform, including camera zoom and viewport stretching.
        var inverse = GetGlobalTransformWithCanvas().AffineInverse();
        var viewport = GetViewportRect();
        var from = inverse * viewport.Position;
        var to = inverse * viewport.End;
        var rect = new Rect2(from, to - from).Abs();
        var margin = rect.Grow(32);
        var pixelsPerTile = TileSize * Game.Camera.Zoom.X;
        // Hysteresis avoids rebuilding both levels when the wheel rests near a boundary.
        if (CurrentLod == 0 && pixelsPerTile < 11) CurrentLod = 1;
        else if (CurrentLod == 1 && pixelsPerTile > 13) CurrentLod = 0;
        else if (CurrentLod == 1 && pixelsPerTile < 5.5f) CurrentLod = 2;
        else if (CurrentLod == 2 && pixelsPerTile > 6.5f) CurrentLod = 1;
        if (_frame == 1) CurrentLod = RenderSnapshot.DetailLevel(pixelsPerTile);
        var minX = Math.Max(0, (int)MathF.Floor(margin.Position.X / 256));
        var maxX = Math.Min(world.Columns - 1, (int)MathF.Floor(margin.End.X / 256));
        var minY = Math.Max(0, (int)MathF.Floor(margin.Position.Y / 256));
        var maxY = Math.Min((world.Height - 1) / 16, (int)MathF.Floor(margin.End.Y / 256));
        _visible.Clear();
        for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++) _visible.Add(world.Chunks[y * world.Columns + x]);
        var center = rect.GetCenter();
        _visible.Sort((a, b) => new Vector2(a.X * 256 + 128, a.Y * 256 + 128).DistanceSquaredTo(center)
            .CompareTo(new Vector2(b.X * 256 + 128, b.Y * 256 + 128).DistanceSquaredTo(center)));
        var buildTimer = Stopwatch.StartNew();
        var built = 0;
        foreach (var chunk in _visible)
        {
            var key = (chunk.Key, CurrentLod);
            _textures.TryGetValue(key, out var cached);
            if ((cached is null || cached.Revision != chunk.Revision) && built < 4 && (built == 0 || buildTimer.Elapsed.TotalMilliseconds < 2))
            {
                var texture = TerrainTexture.Build(chunk, world.Seed, CurrentLod, cached?.Texture);
                cached = new(texture, chunk.Revision, _frame);
                _textures[key] = cached;
                built++;
            }
            else if (cached is not null) _textures[key] = cached = cached with { Used = _frame };
            if (cached is null)
            {
                // Show an already cached level until the new texture fits the upload budget.
                for (var lod = 2; lod >= 0; lod--)
                    if (_textures.TryGetValue((chunk.Key, lod), out cached))
                    {
                        _textures[(chunk.Key, lod)] = cached = cached with { Used = _frame };
                        break;
                    }
            }
            var area = new Rect2(chunk.X * 256, chunk.Y * 256, 256, 256);
            if (cached is not null) DrawTextureRect(cached.Texture, area, false);
            else DrawRect(area, TerrainTexture.ColorFor(chunk.Tiles[136]));
        }
        VisibleChunks = _visible.Count;
        VisibleObjects = 0;
        if (CurrentLod == 0)
        {
            _entities.Clear();
            foreach (var chunk in _visible)
                foreach (var entity in chunk.Entities) if (margin.HasPoint(ToVector(entity.Tile))) _entities.Add(entity);
            _entities.Sort((a, b) => { var order = a.Tile.Y.CompareTo(b.Tile.Y); return order != 0 ? order : a.Id.CompareTo(b.Id); });
            foreach (var entity in _entities) DrawEntity(entity);
            VisibleObjects += _entities.Count;
        }
        _people.Clear();
        foreach (var person in world.People) if (margin.HasPoint(ToVector(person.Tile))) _people.Add(person);
        _people.Sort((a, b) => a.Tile.Y.CompareTo(b.Tile.Y));
        var alpha = world.Paused ? 1 : (float)Math.Clamp(Stopwatch.GetElapsedTime(_publicationTime).TotalSeconds * SimulationRunner.PublicationsPerSecond, 0, 1);
        foreach (var person in _people) DrawPerson(person, world.Tick, alpha);
        VisibleObjects += _people.Count;
        if (Game.DebugView) DrawDebug(world, margin);
        var night = Math.Clamp((.25f - world.Sunlight) * .65f, 0, .16f);
        if (night > 0) DrawRect(rect, new Color(.04f, .07f, .18f, night));
        // Unseen textures may be freed without touching simulation entities or their updates.
        if (_frame % 60 == 0 || _textures.Count > 192)
        {
            var excess = Math.Max(0, _textures.Count - 192);
            foreach (var key in _textures.Where(x => x.Value.Used < _frame).OrderBy(x => x.Value.Used).Where((x, index) => index < excess || _frame - x.Value.Used > 180).Select(x => x.Key).ToArray())
            {
                _textures[key].Texture.Dispose();
                _textures.Remove(key);
            }
        }
    }

    private static Vector2 ToVector(GridPoint p) => new((p.X + .5f) * 16, (p.Y + .5f) * 16);
    private void Pixel(Vector2 at, float x, float y, float w, float h, string color) => DrawRect(new Rect2(at + new Vector2(x, y), new Vector2(w, h)), new Color(color));

    private void DrawEntity(RenderEntity entity)
    {
        var p = ToVector(entity.Tile);
        var color = new Color(entity.Color);
        var growth = Math.Max(.25f, entity.Growth);
        switch (entity.Kind)
        {
            case "tree":
                DrawRect(new Rect2(p + new Vector2(-7, 4), new Vector2(16, 5)), new Color(0, 0, 0, .16f));
                Pixel(p, -2, -3, 4, 10, "#70553d");
                if (entity.Shape == "conifer")
                {
                    DrawRect(new Rect2(p + new Vector2(-7, -12) * growth, new Vector2(14, 11) * growth), color.Darkened(.12f));
                    DrawRect(new Rect2(p + new Vector2(-5, -17) * growth, new Vector2(10, 11) * growth), color);
                    DrawRect(new Rect2(p + new Vector2(-2, -21) * growth, new Vector2(4, 7) * growth), color.Lightened(.08f));
                }
                else
                {
                    DrawRect(new Rect2(p + new Vector2(-10, -15) * growth, new Vector2(20, 14) * growth), color);
                    DrawRect(new Rect2(p + new Vector2(-6, -20) * growth, new Vector2(13, 7) * growth), color.Lightened(.07f));
                    DrawRect(new Rect2(p + new Vector2(-8, -14) * growth, new Vector2(7, 6) * growth), color.Lightened(.12f));
                }
                break;
            case "bush":
                DrawRect(new Rect2(p + new Vector2(-5, -4), new Vector2(11, 9) * growth), color.Darkened(.1f));
                DrawRect(new Rect2(p + new Vector2(-3, -6), new Vector2(7, 8) * growth), color);
                if (entity.Yield >= 1) { Pixel(p, -3, -3, 2, 2, entity.Accent); Pixel(p, 2, -1, 2, 2, entity.Accent); }
                if (entity.Yield > 5) Pixel(p, 0, -5, 2, 2, entity.Accent);
                break;
            case "grass": case "flower": case "crop":
                DrawLine(p + new Vector2(-3, 3), p + new Vector2(-4, -3), color, 1);
                DrawLine(p + new Vector2(1, 3), p + new Vector2(2, -5), color, 1);
                if (entity.Yield > 1) { Pixel(p, -5, -4, 3, 2, entity.Accent); Pixel(p, 1, -6, 2, 2, entity.Accent); }
                break;
            case "resource": Pixel(p, -5, -2, 11, 6, entity.Color); Pixel(p, -3, -5, 7, 5, entity.Accent); Pixel(p, 0, -4, 3, 2, "#c2c2b3"); break;
            case "item": if (entity.Shape == "bulk") Pixel(p, -4, 0, 9, 4, entity.Accent); else Pixel(p, -2, -1, 4, 3, entity.Color); break;
            case "floor": Pixel(p, -8, -8, 16, 16, "#b59c72"); Pixel(p, -7, -1, 14, 1, "#a18a65"); break;
            case "wall": Pixel(p, -8, -11, 16, 15, "#685b46"); Pixel(p, -8, -11, 16, 5, entity.Color); Pixel(p, -7, -5, 14, 2, "#887454"); break;
            case "door": Pixel(p, -6, -8, 12, 16, "#695640"); Pixel(p, -3, -6, 6, 12, "#ac916a"); break;
            case "roof": if (Game.DebugView) DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color(1, 1, 1, .06f)); break;
            case "blueprint": DrawRect(new Rect2(p - new Vector2(7, 7), new Vector2(14, 14)), new Color(1, 1, 1, .18f), false, 1); break;
            case "storage": Pixel(p, -5, -4, 11, 9, entity.Color); Pixel(p, -5, -4, 11, 2, entity.Accent); Pixel(p, -1, -3, 2, 8, "#b09468"); break;
            case "fire": Pixel(p, -4, 2, 9, 3, "#6a5745"); if (entity.Yield > 0) { Pixel(p, -2, -4, 5, 7, entity.Color); Pixel(p, 0, -7, 2, 8, entity.Accent); } break;
        }
    }

    private void DrawPerson(RenderPerson person, long tick, float alpha)
    {
        var target = ToVector(person.Tile);
        var p = _previousPeople.TryGetValue(person.Id, out var previous) ? previous.Lerp(target, alpha) : target;
        var selected = Game.Selected == person.Id;
        if (CurrentLod > 0)
        {
            var size = CurrentLod == 2 ? 2 / Game.Camera.Zoom.X : 5;
            DrawRect(new Rect2(p - Vector2.One * size * .5f, Vector2.One * size),
                selected ? new Color("#fff2b9") : person.Alive ? new Color("#ede5d1") : new Color("#776f62"));
            return;
        }
        if (!person.Alive) { Pixel(p, -5, 0, 10, 3, "#776f62"); return; }
        var scale = person.Child ? .7f : 1;
        DrawRect(new Rect2(p + new Vector2(-4, 3), new Vector2(9, 3)), new Color(0, 0, 0, .25f));
        if (selected) DrawRect(new Rect2(p + new Vector2(-7, -14), new Vector2(15, 22)), new Color("#f6eed7"), false, 1);
        DrawRect(new Rect2(p + new Vector2(-3, -6) * scale, new Vector2(7, 8) * scale), Outfits[Math.Abs(person.Appearance % 8)]);
        DrawRect(new Rect2(p + new Vector2(-3, -11) * scale, new Vector2(6, 5) * scale), new Color("#d9b18b"));
        DrawRect(new Rect2(p + new Vector2(-3, -12) * scale, new Vector2(6, 2) * scale), new Color(person.Appearance % 3 == 0 ? "#514437" : "#79604b"));
        var step = person.Moving ? (int)(tick % 2) : 0;
        DrawRect(new Rect2(p + new Vector2(-3, 2 + step) * scale, new Vector2(2, 4) * scale), new Color("#4c5048"));
        DrawRect(new Rect2(p + new Vector2(1, 3 - step) * scale, new Vector2(2, 3) * scale), new Color("#4c5048"));
        if (person.Sleeping) Pixel(p, 5, -13, 2, 2, "#d9e4e8");
        if (person.Pregnant) Pixel(p, 6, -5, 2, 2, "#e8c9b7");
    }
    private void DrawDebug(RenderSnapshot snapshot, Rect2 visible)
    {
        var selected = snapshot.People.FirstOrDefault(p => p.Id == Game.Selected);
        if (selected.Id == 0) return;
        var previous = ToVector(selected.Tile);
        foreach (var tile in snapshot.Path)
        {
            var next = ToVector(tile);
            if (visible.HasPoint(previous) || visible.HasPoint(next)) DrawLine(previous, next, new Color(1, 1, 1, .65f), 1);
            previous = next;
        }
        foreach (var memory in snapshot.Memories)
            if (visible.HasPoint(ToVector(memory.Tile))) DrawRect(new Rect2(ToVector(memory.Tile) - new Vector2(2, 2), new Vector2(4, 4)), new Color(1, 1, 1, memory.Confidence * .5f), false, 1);
    }
}
