using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using Surface = Greyhaven.RiverbendMap.Surface;

internal sealed class TerrainMesh : IDisposable
{
    private readonly RiverbendMap map;
    private readonly List<(Rect2 Bounds, int Material, ArrayMesh Mesh)> chunks = new();
    private readonly Texture2D[] materials = new Texture2D[5];
    private static readonly Color[] Colors =
    {
        new("#f7faf0"), new("#dad2b9"), new("#b5b4a8"), new("#9cb49d"), new("#d5eeed"),
        new("#ebebe6"), new("#c9b39a"), new("#f7f6f1"), new("#d8d4c9"), new("#fff8e4")
    };

    internal TerrainMesh(RiverbendMap map, Texture2D atlas, Texture2D sand)
    {
        this.map = map;
        using var image = atlas.GetImage();
        var half = image.GetSize() / 2;
        var margin = Vector2I.One * 2;
        for (int i = 0; i < 4; i++)
        {
            var offset = new Vector2I(i % 2 * half.X, i / 2 * half.Y);
            using var sample = image.GetRegion(new Rect2I(offset + margin, half - 2 * margin));
            materials[i] = ImageTexture.CreateFromImage(sample);
        }
        materials[4] = sand;
        var area = map.TerrainBounds;
        const int chunkSize = 16;
        for (int y = area.Top; y < area.Bottom; y += chunkSize)
        for (int x = area.Left; x < area.Right; x += chunkSize)
            Bake(x, y, Math.Min(area.Right, x + chunkSize), Math.Min(area.Bottom, y + chunkSize));
    }

    internal static Vector2 Project(float x, float y, float height = 0) =>
        new((x - y) * BattlefieldView.Axis, (x + y) * BattlefieldView.Rise - height * 15);

    internal void Draw(Node2D view, Rect2 visible)
    {
        foreach (var chunk in chunks)
            if (visible.Intersects(chunk.Bounds))
                view.DrawMesh(chunk.Mesh, materials[chunk.Material]);
    }

    private void Bake(int left, int top, int right, int bottom)
    {
        var batches = new TerrainBatch[materials.Length];
        for (int y = top; y < bottom; y++)
        for (int x = left; x < right; x++)
        {
            var cell = map.At(x, y);
            if (cell == null) continue;
            float x0 = x * Campaign.Tile, y0 = y * Campaign.Tile;
            float x1 = x0 + Campaign.Tile, y1 = y0 + Campaign.Tile;
            var a = Project(x0, y0, cell.Height);
            var b = Project(x1, y0, cell.Height);
            var c = Project(x1, y1, cell.Height);
            var d = Project(x0, y1, cell.Height);
            var tint = Colors[(int)cell.Ground] * (.965f + cell.Variation % 9 * .004f);
            if (cell.Cliff)
                tint = new Color("#a09585") * (.74f + cell.Height * .065f + cell.Variation % 13 * .006f);
            if (cell.Railroad && cell.Ground == Surface.Square)
                tint = new Color("#a59a83") * (.94f + cell.Variation % 15 * .008f);
            tint.A = 1;
            int material = cell.Ground switch
            {
                Surface.Water => 2,
                Surface.Road or Surface.Ramp or Surface.Bridge => 3,
                Surface.Square or Surface.Rock => 1,
                Surface.Soil or Surface.Sand => 4,
                _ => 0
            };
            var batch = batches[material] ??= new TerrainBatch();
            var period = materials[material].GetSize() * new Vector2(.32f, .22f);
            batch.Quad(a, b, c, d, tint, period);

            float east = map.At(x + 1, y)?.Height ?? 0;
            float south = map.At(x, y + 1)?.Height ?? 0;
            if (cell.Height > east) batch.Quad(b, c, Project(x1, y1, east), Project(x1, y0, east), new Color("#666b56"), period);
            if (cell.Height > south) batch.Quad(c, d, Project(x0, y1, south), Project(x1, y1, south), new Color("#777965"), period);
        }

        for (int i = 0; i < batches.Length; i++)
        {
            if (batches[i] == null) continue;
            var mesh = batches[i].Build();
            var bounds = mesh.GetAabb();
            chunks.Add((new Rect2(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y), i, mesh));
        }
    }

    private sealed class TerrainBatch
    {
        private readonly List<Vector2> vertices = new();
        private readonly List<Vector2> uv = new();
        private readonly List<Color> colors = new();

        internal void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint, Vector2 period)
        {
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(a); vertices.Add(c); vertices.Add(d);
            uv.Add(a / period); uv.Add(b / period); uv.Add(c / period);
            uv.Add(a / period); uv.Add(c / period); uv.Add(d / period);
            for (int i = 0; i < 6; i++) colors.Add(tint);
        }

        internal ArrayMesh Build()
        {
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }

    public void Dispose()
    {
        foreach (var chunk in chunks) chunk.Mesh.Dispose();
        for (int i = 0; i < 4; i++) materials[i].Dispose();
    }
}
