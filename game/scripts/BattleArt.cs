using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Greyhaven;

internal sealed class BattleArt : IDisposable
{
    private readonly Dictionary<string, Texture2D> staticTextures = new();
    private readonly Dictionary<string, Texture2D> portraits = new();
    private readonly Dictionary<(int Mask, Color Tint), Texture2D> walls = new();
    private readonly Dictionary<(int Frame, int Side), Texture2D> oilFrames = new();
    private Image wallBase, wallPaint;
    private Image oilBase, oilPaint;
    private const int OilColumns = 12, OilCell = 192, OilFrames = 144, OilFps = 24;
    private readonly Dictionary<Texture2D, (int Width, byte[] Bits)> hitMasks = new();
    private readonly Dictionary<(Texture2D Texture, Rect2 Cell), Rect2> inkBounds = new();
    private readonly Dictionary<string, (ImageTexture Texture, LinkedListNode<string> Age)> frames = new();
    private readonly LinkedList<string> age = new();
    private readonly SpriteArchive harbor = new("harbor-v01.pak");
    private readonly SpriteArchive infantry = new("infantry-v12.pak");
    private readonly SpriteArchive structures = new("structures-v10.pak");
    private readonly SpriteArchive buildings = new("buildings-v09.pak");
    private readonly SpriteArchive gait = new("gait-v09.pak");
    internal static readonly Color[] Factions = {
        new("#3e85be"), new("#b84233"), new("#4a9252"), new("#d1a133"), new("#8955b1"), new("#27aca6")
    };

    internal static Color Faction(int side) => side < 0 ? new Color("#dee1dd") : Factions[side];

    internal Texture2D Portrait(TypeInfo type)
    {
        if (portraits.TryGetValue(type.Key, out var old)) return old;
        string key = type.Key == "at" ? "at-mobile" : type.Key;
        Texture2D body, paint;
        Rect2I cell;
        if (type.IsWall)
        {
            body = Wall(0, Faction(0));
            paint = null;
            cell = new Rect2I(0, 0, 320, 320);
        }
        else if (type.Building)
        {
            body = Frame("iso-" + key);
            paint = Frame("iso-" + key + "-paint", false);
            cell = new Rect2I(0, 0, 320, 320);
        }
        else if (key is "tank" or "engineer" || !type.Foot)
        {
            int frame = key == "tank" ? 56 : 14;
            body = Static(key + "-base.png");
            paint = Static(key + "-paint.png");
            cell = type.Foot ? new Rect2I(7 * 512, 0, 512, 512)
                : new Rect2I(frame % 8 * 512, frame / 8 * 512, 512, 512);
        }
        else
        {
            body = Frame(key + "-base");
            paint = Frame(key + "-paint");
            cell = new Rect2I(7 * 512, 0, 512, 512);
        }
        using var image = body.GetImage();
        using var composed = image.GetRegion(cell);
        composed.Convert(Image.Format.Rgba8);
        if (paint != null)
        {
            using var paintImage = paint.GetImage();
            using var layer = paintImage.GetRegion(cell);
            layer.Convert(Image.Format.Rgba8);
            var pixels = composed.GetData();
            var color = layer.GetData();
            var faction = Faction(0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                float alpha = color[i + 3] / 255f;
                if (type.Building)
                {
                    TintContribution(pixels, color, i, faction);
                }
                else
                {
                    float coverage = alpha + pixels[i + 3] / 255f * (1 - alpha);
                    if (coverage <= 0) continue;
                    float under = pixels[i + 3] / 255f * (1 - alpha);
                    pixels[i] = (byte)Math.Clamp((pixels[i] * under + color[i] * faction.R * alpha) / coverage, 0, 255);
                    pixels[i + 1] = (byte)Math.Clamp((pixels[i + 1] * under + color[i + 1] * faction.G * alpha) / coverage, 0, 255);
                    pixels[i + 2] = (byte)Math.Clamp((pixels[i + 2] * under + color[i + 2] * faction.B * alpha) / coverage, 0, 255);
                    pixels[i + 3] = (byte)(coverage * 255);
                }
            }
            composed.SetData(composed.GetWidth(), composed.GetHeight(), false, Image.Format.Rgba8, pixels);
        }
        var used = composed.GetUsedRect().Grow(2).Intersection(new Rect2I(0, 0, cell.Size.X, cell.Size.Y));
        using var crop = composed.GetRegion(used);
        float scale = Math.Min(112f / crop.GetWidth(), 64f / crop.GetHeight());
        crop.Resize(Math.Max(1, (int)(crop.GetWidth() * scale)), Math.Max(1, (int)(crop.GetHeight() * scale)), Image.Interpolation.Lanczos);
        var texture = ImageTexture.CreateFromImage(crop);
        portraits.Add(type.Key, texture);
        return texture;
    }

    private static void TintContribution(byte[] pixels, byte[] paint, int i, Color tint)
    {
        float weight = pixels[i + 3] == 0 ? 0 : paint[i + 3] / (float)pixels[i + 3];
        pixels[i] = (byte)Math.Clamp(pixels[i] - paint[i] * weight * (1 - tint.R), 0, 255);
        pixels[i + 1] = (byte)Math.Clamp(pixels[i + 1] - paint[i + 1] * weight * (1 - tint.G), 0, 255);
        pixels[i + 2] = (byte)Math.Clamp(pixels[i + 2] - paint[i + 2] * weight * (1 - tint.B), 0, 255);
    }

    internal Texture2D Wall(int mask, Color tint)
    {
        if (walls.TryGetValue((mask, tint), out var old)) return old;
        wallBase ??= Static("wall-base.png").GetImage();
        wallPaint ??= Static("wall-paint.png").GetImage();
        var bounds = new Rect2I(mask % 4 * 320, mask / 4 * 320, 320, 320);
        var texture = TintRegion(wallBase, wallPaint, bounds, tint);
        walls.Add((mask, tint), texture);
        return texture;
    }

    internal Texture2D OilDerrick(int side, float time = 0)
    {
        int frame = (int)(time * OilFps) % OilFrames;
        if (oilFrames.TryGetValue((frame, side), out var old)) return old;
        oilBase ??= LoadOilSheet("oilwell-front-base.png");
        oilPaint ??= LoadOilSheet("oilwell-front-paint.png");
        var bounds = new Rect2I(frame % OilColumns * OilCell, frame / OilColumns * OilCell, OilCell, OilCell);
        var texture = TintRegion(oilBase, oilPaint, bounds, Faction(side));
        oilFrames.Add((frame, side), texture);
        return texture;
    }

    private static Image LoadOilSheet(string name)
    {
        var image = new Image();
        if (image.LoadPngFromBuffer(EmbeddedArt(name)) != Error.Ok)
        {
            image.Dispose();
            throw new InvalidDataException("Invalid PNG: " + name);
        }
        image.Resize(OilColumns * OilCell, OilFrames / OilColumns * OilCell, Image.Interpolation.Bilinear);
        return image;
    }

    private static Texture2D TintRegion(Image baseLayer, Image paintLayer, Rect2I bounds, Color tint)
    {
        using var body = baseLayer.GetRegion(bounds);
        using var layer = paintLayer.GetRegion(bounds);
        body.Convert(Image.Format.Rgba8);
        layer.Convert(Image.Format.Rgba8);
        var pixels = body.GetData();
        var paint = layer.GetData();
        for (int i = 0; i < pixels.Length; i += 4)
            TintContribution(pixels, paint, i, tint);
        body.SetData(bounds.Size.X, bounds.Size.Y, false, Image.Format.Rgba8, pixels);
        return ImageTexture.CreateFromImage(body);
    }

    internal Texture2D Static(string name)
    {
        if (staticTextures.TryGetValue(name, out var texture)) return texture;
        var loaded = Decode(name, EmbeddedArt(name));
        staticTextures.Add(name, loaded);
        return loaded;
    }

    private static byte[] EmbeddedArt(string name)
    {
        using var stream = typeof(BattleArt).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("Missing art: " + name);
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal Texture2D Frame(string name, bool required = true)
    {
        if (frames.TryGetValue(name, out var cached))
        {
            age.Remove(cached.Age);
            age.AddLast(cached.Age);
            return cached.Texture;
        }

        var bytes = harbor.Read(name) ?? infantry.Read(name) ?? structures.Read(name) ?? buildings.Read(name) ?? gait.Read(name);
        if (bytes == null)
        {
            if (!required) return null;
            throw new InvalidDataException("Missing frame: " + name);
        }

        var texture = Decode(name, bytes);
        frames.Add(name, (texture, age.AddLast(name)));
        // Draw commands borrow textures until the frame has been submitted.
        return texture;
    }

    internal bool Opaque(Texture2D texture, Rect2 box, Rect2 cell, Vector2 point)
    {
        if (!box.HasPoint(point)) return false;
        var mask = Mask(texture);
        var pixel = cell.Position + (point - box.Position) / box.Size * cell.Size;
        return OpaqueAt(mask, (int)pixel.X, (int)pixel.Y);
    }

    internal Rect2 Ink(Texture2D texture, Rect2 box, Rect2 cell)
    {
        if (!inkBounds.TryGetValue((texture, cell), out var bounds))
        {
            var mask = Mask(texture);
            int left = (int)cell.End.X, top = (int)cell.End.Y, right = -1, bottom = -1;
            for (int y = (int)cell.Position.Y; y < (int)cell.End.Y; y++)
            for (int x = (int)cell.Position.X; x < (int)cell.End.X; x++)
            {
                if (!OpaqueAt(mask, x, y)) continue;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            bounds = right < 0 ? new Rect2(Vector2.Zero, Vector2.One) : new Rect2(
                (new Vector2(left, top) - cell.Position) / cell.Size, new Vector2(right - left + 1, bottom - top + 1) / cell.Size);
            inkBounds.Add((texture, cell), bounds);
        }
        return new Rect2(box.Position + bounds.Position * box.Size, bounds.Size * box.Size);
    }

    private static bool OpaqueAt((int Width, byte[] Bits) mask, int x, int y)
    {
        int index = y * mask.Width + x;
        return index >= 0 && index / 8 < mask.Bits.Length && (mask.Bits[index / 8] & (1 << (index % 8))) != 0;
    }

    private (int Width, byte[] Bits) Mask(Texture2D texture)
    {
        if (!hitMasks.TryGetValue(texture, out var mask))
        {
            using var image = texture.GetImage();
            image.Convert(Image.Format.Rgba8);
            var pixels = image.GetData();
            int count = pixels.Length / 4;
            var bits = new byte[(count + 7) / 8];
            for (int i = 0; i < count; i++)
                if (pixels[i * 4 + 3] >= 64) bits[i / 8] |= (byte)(1 << (i % 8));
            mask = (image.GetWidth(), bits);
            hitMasks.Add(texture, mask);
        }
        return mask;
    }

    internal void Trim()
    {
        while (frames.Count > 112)
        {
            var key = age.First.Value;
            age.RemoveFirst();
            var texture = frames[key].Texture;
            hitMasks.Remove(texture);
            foreach (var entry in inkBounds.Keys.Where(entry => entry.Texture == texture).ToArray())
                inkBounds.Remove(entry);
            texture.Dispose();
            frames.Remove(key);
        }
    }

    private static ImageTexture Decode(string name, byte[] bytes)
    {
        using var image = new Image();
        if (image.LoadPngFromBuffer(bytes) != Error.Ok) throw new InvalidDataException("Invalid PNG: " + name);
        return ImageTexture.CreateFromImage(image);
    }

    public void Dispose()
    {
        foreach (var texture in staticTextures.Values) texture.Dispose();
        foreach (var texture in portraits.Values) texture.Dispose();
        foreach (var texture in walls.Values) texture.Dispose();
        foreach (var texture in oilFrames.Values) texture.Dispose();
        wallBase?.Dispose();
        wallPaint?.Dispose();
        oilBase?.Dispose();
        oilPaint?.Dispose();
        foreach (var entry in frames.Values) entry.Texture.Dispose();
        harbor.Dispose();
        infantry.Dispose();
        structures.Dispose();
        buildings.Dispose();
        gait.Dispose();
    }

    private sealed class SpriteArchive : IDisposable
    {
        private readonly Stream stream;
        private readonly long dataStart;
        private readonly Dictionary<string, (long Offset, int Size)> entries = new();

        internal SpriteArchive(string name)
        {
            stream = typeof(BattleArt).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidDataException("Missing archive: " + name);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "GHBLD008") throw new InvalidDataException(name);
            int count = reader.ReadInt32();
            if (count is < 1 or > 20000) throw new InvalidDataException(name);
            for (int i = 0; i < count; i++)
            {
                var key = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
                var offset = reader.ReadInt64();
                var size = reader.ReadInt32();
                if (offset < 0 || size < 0) throw new InvalidDataException(key);
                entries.Add(key, (offset, size));
            }
            dataStart = stream.Position;
        }

        internal byte[] Read(string name)
        {
            if (!entries.TryGetValue(name, out var entry)) return null;
            stream.Position = dataStart + entry.Offset;
            var bytes = new byte[entry.Size];
            stream.ReadExactly(bytes);
            return bytes;
        }

        public void Dispose() => stream.Dispose();
    }
}

namespace Greyhaven
{
    // The canonical animation bounds are shared with WinForms.
    internal sealed class HarborSprite
    {
        internal readonly string Name;
        internal readonly int Frames;
        internal readonly System.Drawing.Rectangle Bounds;

        internal HarborSprite(string name, int frames, System.Drawing.Rectangle bounds)
        {
            Name = name;
            Frames = frames;
            Bounds = bounds;
        }
    }
}
