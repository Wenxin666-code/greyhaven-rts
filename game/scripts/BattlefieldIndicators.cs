using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class BattlefieldView
{
    private static readonly Dictionary<Vector2I, Vector2I[]> SelectionPixels = new();

    private void DrawSelectionRing(Piece unit, Vector2 anchor)
    {
        float scale = MathF.Max(.5f, GetViewport().GetFinalTransform().Scale.X);
        float radius = (unit.T.Foot ? unit.T.Size * .13f : unit.T.VisualRadius * Axis * MathF.Sqrt(2) * .85f) * Zoom;
        var size = new Vector2I(Math.Max(2, Mathf.RoundToInt(radius * scale)),
            Math.Max(1, Mathf.RoundToInt(radius * scale * .44f)));
        if (!SelectionPixels.TryGetValue(size, out var pixels))
        {
            var perimeter = new HashSet<Vector2I>();
            int x = 0, y = size.Y;
            double rxSquared = size.X * size.X, rySquared = size.Y * size.Y;
            double dx = 0, dy = 2 * rxSquared * y;
            double error = rySquared - rxSquared * y + .25 * rxSquared;
            while (dx < dy)
            {
                Plot(x++, y);
                dx += 2 * rySquared;
                if (error < 0) error += dx + rySquared;
                else { y--; dy -= 2 * rxSquared; error += dx - dy + rySquared; }
            }
            error = rySquared * (x + .5) * (x + .5) + rxSquared * (y - 1) * (y - 1) - rxSquared * rySquared;
            while (y >= 0)
            {
                Plot(x, y--);
                dy -= 2 * rxSquared;
                if (error > 0) error += rxSquared - dy;
                else { x++; dx += 2 * rySquared; error += dx - dy + rxSquared; }
            }
            pixels = perimeter.ToArray();
            SelectionPixels.Add(size, pixels);

            void Plot(int px, int py)
            {
                perimeter.Add(new Vector2I(px, py));
                perimeter.Add(new Vector2I(-px, py));
                perimeter.Add(new Vector2I(px, -py));
                perimeter.Add(new Vector2I(-px, -py));
            }
        }
        var center = (anchor * scale).Round();
        var pixelSize = Vector2.One / scale;
        foreach (var pixel in pixels)
            DrawRect(new Rect2((center + (Vector2)pixel) / scale, pixelSize), HarborSkin.Teal);
    }

    private Rect2 VisibleBody(Piece unit, Vector2 anchor)
    {
        int direction = (unit.Dir + 14) % 16;
        if (unit.T.Building)
        {
            var texture = art.Frame("iso-" + unit.T.Key, false);
            return texture == null ? PickBody(unit) : art.Ink(texture, BuildingBox(unit, anchor), new Rect2(0, 0, 320, 320));
        }
        if (unit.T.Foot)
        {
            float size = unit.T.Size * .54f * Zoom;
            var box = new Rect2(anchor - new Vector2(.5f, .91f) * size, Vector2.One * size);
            string key = unit.T.Key == "at" ? "at-mobile" : unit.T.Key;
            var texture = key == "engineer" ? art.Static("engineer-base.png") : art.Frame(key + "-base");
            if (unit.Stance > 0 && art.Frame($"stance/{unit.T.Key}/{direction / 2}/{Math.Clamp((int)MathF.Round(unit.Stance * 32), 0, 32)}", false) is { } stance)
            {
                float pose = size * 3 / 2.7f;
                return art.Ink(stance, new Rect2(anchor - new Vector2(.5f, .91f) * pose, Vector2.One * pose), new Rect2(0, 0, 384, 384));
            }
            return art.Ink(texture, box, new Rect2(direction / 2 * 512, 0, 512, 512));
        }
        var vehicle = VehicleBox(unit.T, anchor);
        if (unit.T.Key == "tank")
        {
            int hull = TankFrame(unit.Turn);
            var turret = new Rect2(vehicle.Position + tankMounts[hull] * vehicle.Size, vehicle.Size);
            return art.Ink(art.Static("tank-hull-base.png"), vehicle, FrameRect(hull, 256))
                .Merge(art.Ink(art.Static("tank-turret-base.png"), turret, FrameRect(TankFrame(unit.TurretTurn), 256)));
        }
        string vehicleKey = unit.T.Key == "miner" && unit.Cargo > 10 ? "miner-full" :
            World.RepairVehicle(unit) ? "ifv-repair" : unit.T.Key;
        return art.Ink(art.Static(vehicleKey + "-base.png"), vehicle, FrameRect(direction, 512));
    }

    private void DrawIndicators(Piece unit, Vector2 anchor)
    {
        var body = VisibleBody(unit, anchor);
        float scale = MathF.Max(.5f, GetViewport().GetFinalTransform().Scale.X);
        int bodyWidth = Math.Max(1, (int)MathF.Round(body.Size.X * scale));
        int width = unit.T.Building ? Math.Clamp((int)(bodyWidth * .57f), 28, 108)
            : unit.T.Foot ? Math.Clamp(bodyWidth + 4, 9, 24) : Math.Clamp((int)(bodyWidth * .8f), 16, 56);
        int interior = unit.T.Building ? 3 : body.Size.Y * scale < 22 ? 1 : 2;
        var center = body.GetCenter();
        float x = (MathF.Round(center.X * scale) - width / 2) / scale;
        float y = (MathF.Round(body.Position.Y * scale) - interior - 5) / scale;
        DrawRect(new Rect2(x, y, width / scale, (interior + 2) / scale), new Color("#080c09"));
        DrawRect(new Rect2(x + 1 / scale, y + 1 / scale, (width - 2) / scale, interior / scale), new Color("#282f28"));
        float fraction = Math.Clamp(unit.Ready < 1 ? unit.Ready : unit.Hp / unit.T.Hp, 0, 1);
        var fill = unit.Ready < 1 ? new Color("#ecb54e") : fraction <= .25f ? new Color("#f0362a")
            : fraction <= .5f ? new Color("#f2cb2e") : new Color("#45db47");
        int length = (int)MathF.Ceiling((width - 2) * fraction);
        for (int i = 0; i < length; i += 3)
            DrawRect(new Rect2(x + (1 + i) / scale, y + 1 / scale, Math.Min(2, length - i) / scale, interior / scale), fill);
        if (unit.Repairing)
            DrawWrench(new Vector2(center.X, y - 11 / scale), scale);
    }

    private void DrawWrench(Vector2 center, float scale)
    {
        Vector2[] outline =
        {
            new(2, -6), new(0, -3), new(3, 0), new(6, -3), new(6, 1), new(2, 3),
            new(-4, 8), new(-7, 5), new(-2, 0), new(-3, -3), new(2, -6)
        };
        for (int i = 0; i < outline.Length; i++) outline[i] = center + outline[i] / scale;
        DrawPolyline(outline, new Color("#101b20"), 3 / scale, true);
        DrawPolyline(outline, new Color("#8de1bd"), 1 / scale, true);
    }

    private void DrawGarrisonCount(Piece house, Vector2 at)
    {
        if (CaptionFont == null) return;
        string text = house.Side < 0 ? "中立 · 可占领" : $"{World.HouseCount(house)} / {World.HouseCapacity(house)}";
        DrawString(CaptionFont, at + new Vector2(house.Side < 0 ? -33 : -14, 16), text,
            fontSize: 11, modulate: house.Side < 0 ? new Color("#f3eee0") : BattleArt.Faction(house.Side));
    }

    private void DrawCornerMarks(Rect2 box, Color color)
    {
        float arm = Math.Clamp(MathF.Min(box.Size.X, box.Size.Y) / 3, 2, 6);
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) == 0 ? box.Position.X : box.End.X;
            float y = (i & 2) == 0 ? box.Position.Y : box.End.Y;
            var point = new Vector2(x, y).Round();
            DrawLine(point, point + new Vector2((i & 1) == 0 ? arm : -arm, 0), color, 1);
            DrawLine(point, point + new Vector2(0, (i & 2) == 0 ? arm : -arm), color, 1);
        }
    }

    private void DrawFoundationPins(Piece unit)
    {
        var r = Campaign.Footprint(unit.T, unit.P);
        var corners = new[]
        {
            Screen(new Point(r.Left, r.Top)), Screen(new Point(r.Right, r.Top)),
            Screen(new Point(r.Right, r.Bottom)), Screen(new Point(r.Left, r.Bottom))
        };
        var color = BattleArt.Faction(unit.Side);
        for (int i = 1; i < 4; i++)
        {
            var at = corners[i];
            DrawLine(at, at.Lerp(corners[(i + 1) % 4], .13f), color, 1);
            DrawLine(at, at.Lerp(corners[(i + 3) % 4], .13f), color, 1);
        }
    }

    private void DrawSelectionPips(Piece unit, Vector2 at)
    {
        for (int i = 0; i < unit.Rank; i++)
        {
            var tip = at + new Vector2(-4, -8 - 5 * i);
            DrawLine(tip + new Vector2(-4, 3), tip, Gold, 1);
            DrawLine(tip, tip + new Vector2(4, 3), Gold, 1);
        }
        if (unit.T.Key != "miner") return;
        int filled = (int)MathF.Ceiling(unit.Cargo / Campaign.LoadValue * 5);
        for (int i = 0; i < 5; i++)
            DrawRect(new Rect2(at + new Vector2(-10 + i * 4, 5), new Vector2(3, 2)), i < filled ? Gold : new Color("#101b20"));
    }
}
