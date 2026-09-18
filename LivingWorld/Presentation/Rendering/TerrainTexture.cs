namespace LivingWorld.Presentation;

public static class TerrainTexture
{
    // Fine detail, a simplified tile, and one pixel per tile at overview zoom.
    public static int PixelsPerTile(int lod) => lod switch { 0 => 8, 1 => 2, _ => 1 };

    public static ImageTexture Build(RenderChunk chunk, int seed, int lod, ImageTexture? reuse = null)
    {
        var scale = PixelsPerTile(lod);
        var size = 16 * scale;
        var bytes = new byte[size * size * 4];
        void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            var offset = (y * size + x) * 4;
            bytes[offset] = (byte)(Math.Clamp(color.R, 0, 1) * 255);
            bytes[offset + 1] = (byte)(Math.Clamp(color.G, 0, 1) * 255);
            bytes[offset + 2] = (byte)(Math.Clamp(color.B, 0, 1) * 255);
            bytes[offset + 3] = 255;
        }
        for (var ty = 0; ty < 16; ty++) for (var tx = 0; tx < 16; tx++)
        {
            var tile = chunk.Tiles[ty * 16 + tx];
            var baseColor = ColorFor(tile);
            var gx = chunk.X * 16 + tx;
            var gy = chunk.Y * 16 + ty;
            for (var py = 0; py < scale; py++) for (var px = 0; px < scale; px++)
            {
                var color = baseColor;
                if (lod == 0)
                {
                    var noise = unchecked((uint)(gx * 1973 + gy * 9277 + px * 26699 + py * 31847 + seed * 1013));
                    noise ^= noise >> 13;
                    noise *= 1274126177;
                    noise ^= noise >> 16;
                    var grain = ((noise % 17) - 8f) / 255f;
                    color = new Color(color.R + grain, color.G + grain, color.B + grain);
                    if (tile.Water == WaterKind.None && tile.Traffic > 5 && Math.Abs(px - 4) + Math.Abs(py - 4) < Math.Clamp(tile.Traffic / 8, 1, 6))
                        color = color.Lerp(new Color("#b49a71"), Math.Min(.8f, tile.Traffic / 60));
                    if (tile.Water != WaterKind.None && tile.Ice < .15f && (ty * scale + py + gx * 3) % 17 == 0 && px < 4)
                        color = color.Lightened(.12f);
                }
                else if (tile.Water == WaterKind.None && tile.Traffic > 8)
                    color = color.Lerp(new Color("#b49a71"), Math.Min(.65f, tile.Traffic / 80));
                SetPixel(tx * scale + px, ty * scale + py, color);
            }
        }
        if (lod > 0)
        {
            // Stationary objects are baked into the chunk instead of issuing per-object draw calls.
            foreach (var entity in chunk.Entities)
            {
                if (entity.Kind is "roof" or "blueprint" || (lod == 2 && entity.Kind is "item" or "flower" or "grass")) continue;
                var x = (entity.Tile.X - chunk.X * 16) * scale;
                var y = (entity.Tile.Y - chunk.Y * 16) * scale;
                var color = new Color(entity.Color);
                if (entity.Kind == "fire" && entity.Yield == 0) color = new Color("#6a5745");
                var fill = entity.Kind is "tree" or "wall" or "floor" or "door";
                for (var py = 0; py < (fill ? scale : 1); py++) for (var px = 0; px < (fill ? scale : 1); px++)
                    SetPixel(x + px, y + py, color);
                if (lod == 1 && entity.Kind == "bush" && entity.Yield > 0) SetPixel(x + 1, y, new Color(entity.Accent));
            }
        }
        using var image = Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes);
        if (reuse is not null && reuse.GetWidth() == size && reuse.GetHeight() == size)
        {
            reuse.Update(image);
            return reuse;
        }
        return ImageTexture.CreateFromImage(image);
    }

    public static Color ColorFor(RenderTile tile)
    {
        var color = tile.Water switch
        {
            WaterKind.Ocean => new Color(tile.Height < .2f ? "#284b60" : "#35667a"),
            WaterKind.River => new Color("#4a8390"), WaterKind.Lake => new Color("#477b86"),
            _ => tile.Biome switch
            {
                Biome.Beach => new Color("#c6b486"), Biome.Forest => new Color("#66784b"),
                Biome.Meadow => new Color("#8d9d63"), Biome.Marsh => new Color("#647d60"),
                Biome.Mountain => new Color("#8c8b7f"), Biome.Alpine => new Color("#c3c4b9"), _ => new Color("#81915c")
            }
        };
        if (tile.Water != WaterKind.None && tile.Ice > .03f) color = color.Lerp(new Color("#b8d2d5"), Math.Min(1, tile.Ice / .2f));
        if (tile.Water == WaterKind.None) color = color.Lerp(new Color("#d3d8cb"), tile.Snow * .9f);
        return color;
    }
}
