using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;

internal sealed class BattlefieldShroud : IDisposable
{
    private readonly Campaign world;
    private ArrayMesh mesh;
    private int revision = -1;
    private Rect2I window;

    internal BattlefieldShroud(Campaign world) => this.world = world;

    internal void Draw(Node2D canvas, Vector2 origin, float zoom, Rect2I requested, float strength)
    {
        if (revision != world.FogRevision || requested != window)
            Rebuild(requested);
        if (mesh == null) return;
        canvas.DrawMesh(mesh, null, new Transform2D(0, Vector2.One * zoom, 0, origin), new Color("#152323", strength));
    }

    private void Rebuild(Rect2I bounds)
    {
        mesh?.Dispose();
        mesh = null;
        window = bounds;
        revision = world.FogRevision;
        var vertices = new List<Vector2>();
        var strips = new List<Rect2I>();
        var previous = new Dictionary<(int Start, int End), int>();
        var next = new Dictionary<(int Start, int End), int>();
        for (int y = bounds.Position.Y; y < bounds.End.Y; y++)
        {
            next.Clear();
            for (int x = bounds.Position.X; x < bounds.End.X;)
            {
                if (world.Seen(0, x, y)) { x++; continue; }
                int left = x++;
                while (x < bounds.End.X && !world.Seen(0, x, y)) x++;
                var key = (left, x);
                if (previous.TryGetValue(key, out int index))
                {
                    var strip = strips[index];
                    strip.Size += Vector2I.Down;
                    strips[index] = strip;
                }
                else
                {
                    index = strips.Count;
                    strips.Add(new Rect2I(left, y, x - left, 1));
                }
                next.Add(key, index);
            }
            (previous, next) = (next, previous);
        }
        foreach (var strip in strips)
        {
            var a = Project(strip.Position.X, strip.Position.Y);
            var b = Project(strip.End.X, strip.Position.Y);
            var c = Project(strip.End.X, strip.End.Y);
            var d = Project(strip.Position.X, strip.End.Y);
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            vertices.Add(a); vertices.Add(c); vertices.Add(d);
        }
        if (vertices.Count == 0) return;
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    // Visibility cells share the same plane even when the terrain under them has height.
    private Vector2 Project(int x, int y) => TerrainMesh.Project(
        world.FogOriginX + x * Campaign.FogSize, world.FogOriginY + y * Campaign.FogSize);

    public void Dispose() => mesh?.Dispose();
}
