using Godot;
using System;
using System.Collections.Generic;

internal sealed class HarborSkin
{
    internal static readonly Color Ink = new("#101b20"), Pale = new("#f3eee0"), Gold = new("#ecb54e");
    internal static readonly Color Dim = new("#abb8b5"), Teal = new("#82d0b4");
    private readonly Dictionary<(string Fill, string Edge, int Height, bool Marker), StyleBoxTexture> plates = new();

    internal StyleBoxTexture Plate(string fill, string edge, int height = 48, bool marker = false)
    {
        var key = (fill, edge, height, marker);
        if (plates.TryGetValue(key, out var cached)) return cached;

        const int width = 48;
        const int cut = 8;
        var top = new Color(fill);
        var bottom = new Color(top.Darkened(.29f), top.A);
        var rim = new Color(edge);
        using var art = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        {
            int left = Math.Max(0, cut - y);
            int right = width - 1 - Math.Max(0, cut - (height - 1 - y));
            var face = top.Lerp(bottom, y / (float)(height - 1));
            for (int x = left; x <= right; x++)
            {
                var color = y == 0 || y == height - 1 || x == left || x == right ? rim : face;
                if (y == 2 && x >= 9 && x < width - 2) color = Blend(color, Pale, 48 / 255f);
                if (y == height - 3 && x >= 2 && x < width - 9) color = Blend(color, Colors.Black, 115 / 255f);
                if (marker && x is 3 or 4 && y >= 10 && y < height - 10) color = rim;
                art.SetPixel(x, y, color);
            }
        }

        var style = new StyleBoxTexture
        {
            Texture = ImageTexture.CreateFromImage(art),
            TextureMarginLeft = 11, TextureMarginRight = 11,
            TextureMarginTop = 10, TextureMarginBottom = 10,
            ContentMarginLeft = marker ? 8 : 1, ContentMarginRight = marker ? 2 : 1,
            ContentMarginTop = 0, ContentMarginBottom = 1
        };
        plates.Add(key, style);
        return style;
    }

    internal void Button(Theme theme)
    {
        theme.SetStylebox("normal", "Button", Plate("#29383e", "#627971", 32, true));
        theme.SetStylebox("hover", "Button", Plate("#425259", "#627971", 32, true));
        theme.SetStylebox("pressed", "Button", Plate("#253136", "#82958c", 32, true));
        theme.SetStylebox("disabled", "Button", Plate("#1b2428", "#465149", 32, true));
    }

    private static Color Blend(Color background, Color layer, float alpha) =>
        new Color(background.Lerp(layer, alpha), background.A);
}
