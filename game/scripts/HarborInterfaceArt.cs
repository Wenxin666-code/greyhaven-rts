using Godot;

public partial class HarborGame
{
    private static TextureRect FrontShade(Control parent, int from, int to)
    {
        var shade = Gradient(parent, new Color(Ink, from / 255f), new Color(Ink, to / 255f));
        Position(shade, 0, 0, 1472, 819);
        return shade;
    }

    private static TextureRect Gradient(Control parent, Color from, Color to, bool vertical = false)
    {
        var colors = new Gradient { Colors = new[] { from, to } };
        var texture = new GradientTexture2D
        {
            Gradient = colors, Width = vertical ? 1 : 256, Height = vertical ? 256 : 1,
            FillTo = vertical ? Vector2.Down : Vector2.Right
        };
        var shade = new TextureRect
        {
            Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(shade);
        return shade;
    }

    private static void AddEmblem(Control header)
    {
        var glyph = new Control { Position = new Vector2(42, 26), MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(glyph);
        glyph.Draw += () =>
        {
            var left = new Vector2(-13, -2.6f);
            var right = new Vector2(13, -2.6f);
            var tip = new Vector2(0, 5.2f);
            glyph.DrawPolyline(new[] { left, new Vector2(0, -10.4f), right, tip, left }, Pale, 2, true);
            for (int i = 1; i <= 2; i++)
            {
                var offset = new Vector2(0, i * 4);
                glyph.DrawPolyline(new[] { left + offset, tip + offset, right + offset }, Pale, 2, true);
            }
        };
    }

    private static void AddPauseGlyph(Button button)
    {
        Accent(button, 12, 10, 3, 11, Teal);
        Accent(button, 19, 10, 3, 11, Teal);
    }
}
