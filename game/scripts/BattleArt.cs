using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Greyhaven;

internal sealed class BattleArt : IDisposable
{
    private readonly Dictionary<string, Texture2D> staticTextures = new();
    private readonly Dictionary<string, Texture2D> portraits = new();
    private Image oilBase, oilPaint;
    private const int OilColumns = 12, OilCell = 192, OilFrames = 144, OilFps = 24;
    private sealed record HitMask(int Width, byte[] Bits, Rect2 Bounds);
    private readonly Dictionary<Texture2D, HitMask> hitMasks = new();
    internal const long FrameBudget = 64L * 1024 * 1024;
    private enum FrameSource { Animation, Cell, Wall, Oil }
    private readonly record struct FrameKey(FrameSource Source, long Index);
    private sealed record CachedFrame(ImageTexture Texture, LinkedListNode<FrameKey> Age, long Bytes);
    private readonly Dictionary<FrameKey, CachedFrame> frames = new();
    private readonly LinkedList<FrameKey> age = new();
    private long frameBytes;
    internal long CachedFrameBytes => frameBytes;
    private readonly SpriteArchive sprites = new("runtime-sprites.pak");
    private readonly SpriteArchive cells = new("runtime-cells.pak");
    internal static readonly Color[] Factions = {
        new("#3e85be"), new("#b84233"), new("#4a9252"), new("#d1a133"), new("#8955b1"), new("#27aca6")
    };

    internal static Color Faction(int side) => side < 0 ? new Color("#dee1dd") : Factions[side];

    internal Texture2D Portrait(TypeInfo type)
    {
        if (portraits.TryGetValue(type.Key, out var old)) return old;
        string key = type.Key == "at" ? "at-mobile" : type.Key;
        Texture2D body, paint;
        if (type.IsWall)
        {
            body = Wall(0, 0);
            paint = null;
        }
        else if (type.Building)
        {
            body = Frame("iso-" + key);
            paint = Frame("iso-" + key + "-paint", false);
        }
        else if (key == "tank")
        {
            body = Frame("tank-portrait-base");
            paint = Frame("tank-portrait-paint");
        }
        else
        {
            int frame = type.Foot ? 7 : 14;
            body = Cell(key + "-base", frame);
            paint = Cell(key + "-paint", frame);
        }
        using var composed = body.GetImage();
        composed.Convert(Image.Format.Rgba8);
        if (paint != null)
        {
            using var layer = paint.GetImage();
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
        var used = composed.GetUsedRect().Grow(2).Intersection(new Rect2I(Vector2I.Zero, composed.GetSize()));
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

    internal Texture2D Wall(int mask, int side)
    {
        var key = new FrameKey(FrameSource.Wall, (side + 1) * 16 + mask);
        if (FindFrame(key) is { } old) return old;
        using var body = ReadCell("wall-base", mask);
        using var paint = ReadCell("wall-paint", mask);
        return RememberFrame(key, TintImages(body, paint, Faction(side)));
    }

    internal Texture2D OilDerrick(int side, float time = 0)
    {
        int frame = (int)(time * OilFps) % OilFrames;
        var key = new FrameKey(FrameSource.Oil, (side + 1) * OilFrames + frame);
        if (FindFrame(key) is { } old) return old;
        oilBase ??= LoadOilSheet("oilwell-front-base.png");
        oilPaint ??= LoadOilSheet("oilwell-front-paint.png");
        var bounds = new Rect2I(frame % OilColumns * OilCell, frame / OilColumns * OilCell, OilCell, OilCell);
        using var body = oilBase.GetRegion(bounds);
        using var paint = oilPaint.GetRegion(bounds);
        return RememberFrame(key, TintImages(body, paint, Faction(side)));
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

    private static ImageTexture TintImages(Image body, Image layer, Color tint)
    {
        body.Convert(Image.Format.Rgba8);
        layer.Convert(Image.Format.Rgba8);
        var pixels = body.GetData();
        var paint = layer.GetData();
        for (int i = 0; i < pixels.Length; i += 4)
            TintContribution(pixels, paint, i, tint);
        body.SetData(body.GetWidth(), body.GetHeight(), false, Image.Format.Rgba8, pixels);
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

    internal Texture2D Frame(string name, bool required = true) =>
        ArchiveFrame(sprites, FrameSource.Animation, name, required);

    internal Texture2D Cell(string atlas, int cell) =>
        ArchiveFrame(cells, FrameSource.Cell, $"{atlas}/{cell}", true);

    private Image ReadCell(string atlas, int cell)
    {
        string name = $"{atlas}/{cell}";
        if (!cells.TryGet(name, out var entry)) throw new InvalidDataException("Missing frame: " + name);
        return DecodeImage(name, cells.Read(entry));
    }

    private ImageTexture ArchiveFrame(SpriteArchive archive, FrameSource source, string name, bool required)
    {
        if (!archive.TryGet(name, out var entry))
        {
            if (!required) return null;
            throw new InvalidDataException("Missing frame: " + name);
        }
        var key = new FrameKey(source, entry.Offset);
        return FindFrame(key) ?? RememberFrame(key, Decode(name, archive.Read(entry)));
    }

    private ImageTexture FindFrame(FrameKey key)
    {
        if (frames.TryGetValue(key, out var cached))
        {
            age.Remove(cached.Age);
            age.AddLast(cached.Age);
            return cached.Texture;
        }

        return null;
    }

    private ImageTexture RememberFrame(FrameKey key, ImageTexture texture)
    {
        long bytes = (long)texture.GetWidth() * texture.GetHeight() * 4;
        frames.Add(key, new CachedFrame(texture, age.AddLast(key), bytes));
        frameBytes += bytes;
        return texture;
    }

    internal bool Opaque(Texture2D texture, Rect2 box, Vector2 point)
    {
        if (!box.HasPoint(point)) return false;
        var mask = Mask(texture);
        var pixel = (point - box.Position) / box.Size * texture.GetSize();
        int index = (int)pixel.Y * mask.Width + (int)pixel.X;
        return index >= 0 && index / 8 < mask.Bits.Length && (mask.Bits[index / 8] & (1 << (index % 8))) != 0;
    }

    internal Rect2 Ink(Texture2D texture, Rect2 box)
    {
        var bounds = Mask(texture).Bounds;
        return new Rect2(box.Position + bounds.Position * box.Size, bounds.Size * box.Size);
    }

    private HitMask Mask(Texture2D texture)
    {
        if (!hitMasks.TryGetValue(texture, out var mask))
        {
            using var image = texture.GetImage();
            image.Convert(Image.Format.Rgba8);
            var pixels = image.GetData();
            int width = image.GetWidth(), height = image.GetHeight(), count = width * height;
            var bits = new byte[(count + 7) / 8];
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (pixels[i * 4 + 3] < 64) continue;
                bits[i / 8] |= (byte)(1 << (i % 8));
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
            Vector2 size = image.GetSize();
            var bounds = right < 0 ? new Rect2(Vector2.Zero, Vector2.One) : new Rect2(
                new Vector2(left, top) / size, new Vector2(right - left + 1, bottom - top + 1) / size);
            mask = new HitMask(width, bits, bounds);
            hitMasks.Add(texture, mask);
        }
        return mask;
    }

    internal void Trim()
    {
        // The previous draw has finished. Keep all frames borrowed by the current draw alive.
        while (frameBytes > FrameBudget)
        {
            var key = age.First.Value;
            age.RemoveFirst();
            var entry = frames[key];
            frameBytes -= entry.Bytes;
            var texture = entry.Texture;
            hitMasks.Remove(texture);
            texture.Dispose();
            frames.Remove(key);
        }
    }

    private static ImageTexture Decode(string name, byte[] bytes)
    {
        using var image = DecodeImage(name, bytes);
        return ImageTexture.CreateFromImage(image);
    }

    private static Image DecodeImage(string name, byte[] bytes)
    {
        var image = new Image();
        bool webp = bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        var error = webp ? image.LoadWebpFromBuffer(bytes) : image.LoadPngFromBuffer(bytes);
        if (error == Error.Ok) return image;
        image.Dispose();
        throw new InvalidDataException("Invalid sprite frame: " + name);
    }

    internal void ReleaseBattleFrames()
    {
        foreach (var entry in frames.Values) entry.Texture.Dispose();
        frames.Clear();
        age.Clear();
        hitMasks.Clear();
        frameBytes = 0;
        oilBase?.Dispose();
        oilPaint?.Dispose();
        oilBase = oilPaint = null;
    }

    public void Dispose()
    {
        foreach (var texture in staticTextures.Values) texture.Dispose();
        foreach (var texture in portraits.Values) texture.Dispose();
        ReleaseBattleFrames();
        sprites.Dispose();
        cells.Dispose();
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

        internal bool TryGet(string name, out (long Offset, int Size) entry) => entries.TryGetValue(name, out entry);

        internal byte[] Read((long Offset, int Size) entry)
        {
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
