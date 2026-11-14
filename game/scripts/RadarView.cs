using Godot;
using Greyhaven;
using System;
using System.Linq;
using Point = System.Drawing.PointF;

internal partial class RadarView : Control
{
    internal Campaign World;
    internal BattlefieldView Field;
    internal Action<Point, InputEventMouseButton> Order;
    private ImageTexture raster;
    private Campaign lastWorld;
    private int revision = -1;
    private const int Width = 148, Height = 100;
    private Rect2 Map => new(4, 4, Size.X - 8, Size.Y - 8);

    public override void _Draw()
    {
        if (World == null) return;
        RefreshTerrain();
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#101b20"));
        DrawTextureRect(raster, Map, false);
        foreach (var mineral in World.Ore)
            if (mineral.Stock > 0 && World.Explored(0, mineral.P))
                DrawRect(new Rect2(Project(mineral.P), new Vector2(1.5f, 1.5f)), new Color("#c5ae74"));
        foreach (var p in World.Units.Where(p => p.Alive && p.Carrier == null && World.Visible(p)))
            DrawRect(new Rect2(Project(p.P), Vector2.One * (p.T.Building ? 3 : 2)), BattleArt.Faction(p.Side));
        float span = World.Battlefield.Span;
        var reach = new Vector2(Field.Area.Size.X / BattlefieldView.Axis, Field.Area.Size.Y / BattlefieldView.Rise)
            / (Field.Zoom * span) * Map.Size;
        var box = new Rect2(Project(Field.CameraPosition) - reach / 2, reach).Intersection(Map);
        DrawRect(box, new Color("#e7e0c7"), false, 1);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } click ||
            click.ButtonIndex is not (MouseButton.Left or MouseButton.Right) || !Map.HasPoint(click.Position))
            return;
        Order?.Invoke(Unproject((click.Position - Map.Position) / Map.Size), click);
        QueueRedraw();
        AcceptEvent();
    }

    private Vector2 Project(Point p) => Map.Position + new Vector2(
        (p.X - p.Y + World.MapHeight) / World.Battlefield.Span,
        (p.X + p.Y) / World.Battlefield.Span) * Map.Size;

    private Point Unproject(Vector2 p)
    {
        return Unproject(p, World.MapWidth, World.MapHeight);
    }

    private static Point Unproject(Vector2 p, float width, float height)
    {
        float u = p.X * (width + height) - height;
        float v = p.Y * (width + height);
        return new Point((v + u) * .5f, (v - u) * .5f);
    }

    private void RefreshTerrain()
    {
        if (World == lastWorld && World.FogRevision == revision) return;
        using var pixels = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var point = Unproject(new Vector2((x + .5f) / Width, (y + .5f) / Height));
            var cell = World.Explored(0, point) ? World.River.At(
                (int)MathF.Floor(point.X / Campaign.Tile), (int)MathF.Floor(point.Y / Campaign.Tile)) : null;
            pixels.SetPixel(x, y, TerrainColor(cell));
        }
        if (raster == null) raster = ImageTexture.CreateFromImage(pixels);
        else raster.Update(pixels);
        lastWorld = World;
        revision = World.FogRevision;
    }

    public override void _ExitTree() => raster?.Dispose();

    internal static Texture2D Preview(RiverbendMap map)
    {
        const int width = 450, height = 240;
        using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var p = Unproject(new Vector2((x + .5f) / width, (y + .5f) / height), map.WidthCells, map.HeightCells);
            var cell = map.At((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
            var tint = TerrainColor(cell);
            float dither = cell == null ? 1 : .88f + (cell.Variation % 21) * .009f;
            image.SetPixel(x, y, new Color(tint.R * dither, tint.G * dither, tint.B * dither));
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static Color TerrainColor(RiverbendMap.Cell cell) => cell?.Ground switch
    {
        null => new Color("#101b20"),
        RiverbendMap.Surface.Water => new Color("#355e68"),
        RiverbendMap.Surface.Road or RiverbendMap.Surface.Bridge or RiverbendMap.Surface.Ramp => new Color("#96998a"),
        RiverbendMap.Surface.Sand or RiverbendMap.Surface.Soil => new Color("#a8a27c"),
        RiverbendMap.Surface.Square => new Color("#929385"),
        _ => cell.Tree ? new Color("#405a3d") : new Color("#758b61")
    };
}
