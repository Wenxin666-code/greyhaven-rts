using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using DrawingRect = System.Drawing.Rectangle;

public partial class BattlefieldView
{
    private void DrawInfantry(Piece unit, Vector2 anchor, int direction, Color tint)
    {
        string key = unit.T.Key;
        string baseKey = key == "at" ? "at-mobile" : key;
        float width = unit.T.Size * .54f * Zoom;
        var standing = new Rect2(anchor - new Vector2(.5f, .91f) * width, Vector2.One * width);
        float gaitWidth = width * 3 / 2.7f;
        var gaitBox = new Rect2(anchor - new Vector2(.5f, .91f) * gaitWidth, Vector2.One * gaitWidth);
        var imageCell = new Rect2(direction * 512, 0, 512, 512);
        float age = World.VisualTime - unit.MovedAt;

        bool combat = Campaign.CombatInfantry(unit) && (!Campaign.ChangingStance(unit) ||
            unit.WeaponAim > .001f && (unit.Stance <= .001f || unit.Stance >= .999f)) &&
            (unit.Stance > 0 || unit.WeaponAim > 0 || age >= .065f);
        if (combat)
        {
            string family = "combat/" + key + (unit.Stance >= .999f ? "-deployed" : "");
            float fireAge = World.VisualTime - unit.FiredAt;
            int pose = fireAge >= 0 && fireAge < .23f ? 9 + Math.Min(7, (int)(fireAge / .23f * 8)) :
                Math.Clamp((int)MathF.Round(unit.WeaponAim * 8), 0, 8);
            DrawFrame($"{family}/{direction}/{pose}", CropGait(gaitBox, InfantryV12.Crops[family][direction]), tint);
            return;
        }
        if (unit.Stance > 0)
        {
            int frame = Math.Clamp((int)MathF.Round(unit.Stance * 32), 0, 32);
            DrawFrame($"stance/{key}/{direction}/{frame}", gaitBox, tint);
            return;
        }
        if (age < .13f)
        {
            int frame = (int)(unit.Travel / 24 * GaitV09.Frames) % GaitV09.Frames;
            string family = "gait/" + baseKey;
            string name = $"{family}/{direction}/{frame}";
            if (art.Frame(name, false) != null)
            {
                var crop = key == "engineer" ? GaitV09.Crop(key, direction) : InfantryV12.Crops[family][direction];
                DrawFrame(name, CropGait(gaitBox, crop), tint);
                return;
            }
        }
        if (key == "engineer") DrawSheet(key, standing, imageCell, tint);
        else DrawLayered(baseKey + "-base", standing, tint, imageCell, baseKey + "-paint");
    }

    private static Rect2 CropGait(Rect2 box, DrawingRect crop) => new(
        box.Position + new Vector2(crop.X, crop.Y) * box.Size / 384,
        new Vector2(crop.Width, crop.Height) * box.Size / 384);

    private void DrawDamageEffects(Piece piece, Vector2 anchor)
    {
        if (piece.T.IsWall)
        {
            if (!piece.Alive && !piece.Selling && piece.Dead < .32f)
                DrawCircle(anchor - new Vector2(0, 3 * Zoom), (2 + piece.Dead * 24) * Zoom,
                    new Color("#a49b7b", .42f * (1 - piece.Dead / .32f)));
            return;
        }
        if (piece.T.Foot)
        {
            if (!piece.Alive && piece.Dead < .66f)
                DrawCircle(anchor - new Vector2(0, 2 * Zoom), (1 + piece.Dead * 8) * Zoom,
                    new Color("#a49b7b", .30f * (1 - piece.Dead / .66f)));
            return;
        }

        if (piece.Alive)
        {
            DrawRepairSparks(piece);
            if (piece.Hp < piece.T.Hp * (piece.T.Building ? .75f : .45f))
                Smoke(anchor - new Vector2(0, (piece.T.Building ? 20 : 10) * Zoom), World.VisualTime * .43f + piece.Id * .31f, 12 * Zoom);
            if (piece.Hp < piece.T.Hp * .25f)
                Flame(anchor - new Vector2(1, 7) * Zoom, World.VisualTime + piece.Id * .1f, (piece.T.Building ? 32 : 21) * Zoom);
            if (piece.Deployed && piece.T.Key == "power" && World.Power(piece.Side) >= 0 &&
                BuildingV08.Specs["power"].Sockets.TryGetValue("smoke", out var stacks))
            {
                var box = BuildingBox(piece, anchor);
                foreach (var stack in stacks)
                    Smoke(box.Position + new Vector2(stack.X, stack.Y) * box.Size / 320,
                        World.VisualTime * .5f + stack.X, 11 * Zoom);
            }
            return;
        }

        float age = piece.Dead;
        float blastAt = piece.T.Building ? 1.28f : 0;
        float size = Zoom * (piece.T.Building ? 62 + piece.T.FoundationWidth * 10 : 40);
        if (age < blastAt)
        {
            Flame(anchor - new Vector2(-3, 15) * Zoom, age, size * .55f);
            Flame(anchor - new Vector2(8, 22) * Zoom, age + .27f, size * .44f);
            Smoke(anchor - new Vector2(3, 21) * Zoom, age, size * .42f);
            return;
        }

        float after = age - blastAt;
        if (after > 2.6f) return;
        Scorch(anchor, size, after);
        if (after < .91f)
        {
            int frame = (int)(after / .91f * 95);
            var box = new Rect2(anchor - new Vector2(.5f, .83f) * size, Vector2.One * size);
            DrawTextureRectRegion(art.Static("blast-cloud-v11.png"), box, FrameRect(frame, 192));
        }
        if (after < 1.14f) DrawDebris(piece.Id, anchor, after, size);
        if (after > .19f) Smoke(anchor - new Vector2(0, 10) * Zoom, after * .51f, size * .3f);
    }

    private void DrawRepairSparks(Piece piece)
    {
        float age = (World.VisualTime - piece.RepairAt) / .40f;
        if (age < 0 || age >= 1) return;
        var target = World.RepairVehicle(piece) ? piece.RepairPoint : piece.P;
        if (!World.Explored(0, target) && !FullSceneVisibility) return;
        var center = Screen(target) - new Vector2(0, (piece.T.Building ? 19 : 4) * Zoom);
        var color = new Color("#c1e2ee", 1 - age);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8 + piece.Id * .43f;
            var ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float reach = (3 + (i % 3 + 1) * 2 * age) * Zoom;
            DrawLine(center + ray * reach * .35f, center + ray * reach, color, MathF.Max(1, Zoom * .6f), true);
        }
    }

    private void Flame(Vector2 anchor, float time, float size)
    {
        var box = new Rect2(anchor - new Vector2(.5f, .72f) * size, Vector2.One * size);
        int frame = (int)(MathF.Abs(time) * 28) % 96;
        DrawTextureRectRegion(art.Static("hazard-flame-v10.png"), box, FrameRect(frame, 128));
    }

    private void Smoke(Vector2 anchor, float time, float size)
    {
        for (int i = 0; i < 4; i++)
        {
            float phase = (time + i * .25f) % 1;
            var p = anchor + new Vector2((phase * phase * .8f + .09f * MathF.Sin(i * 11 + phase * 5)) * size, -phase * size * 2);
            float radius = size * (.16f + phase * .38f);
            float opacity = MathF.Sin(phase * MathF.PI) * .13f;
            DrawCircle(p, radius, new Color("#42433d", opacity), true, -1, true);
            DrawCircle(p - new Vector2(radius * .23f, radius * .32f), radius * .78f, new Color("#898475", opacity * .51f), true, -1, true);
        }
    }

    private void Scorch(Vector2 at, float size, float fade)
    {
        if (fade < .08f) return;
        DrawSetTransform(at, 0, new Vector2(size * .68f, size * .24f));
        for (int i = 0; i < 3; i++)
            DrawCircle(Vector2.Zero, .44f - i * .095f, new Color("#292822", (.13f - .028f * i) * Math.Max(0, 1 - fade / 3)));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawDebris(int id, Vector2 anchor, float time, float size)
    {
        float age = time / 1.14f;
        for (int i = 0; i < 15; i++)
        {
            float angle = id * .73f + i * 2.399963f;
            float distance = (age * .95f + .06f) * size * (.34f + (i * 7 % 17) / 31f);
            float height = (2.1f + i % 3) / 6 * MathF.Sin(age * MathF.PI) * size;
            var offset = new Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance * .52f);
            var center = anchor + offset - new Vector2(0, height);
            float opacity = 1 - age;
            if (i % 3 == 0)
            {
                var tail = center - new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .5f - .22f) * size * .075f;
                DrawLine(center, tail, new Color("#ffb451", opacity), Math.Max(1, Zoom));
            }
            else
            {
                var sprite = art.Static("debris-base-v11.png");
                float cellSize = sprite.GetWidth() / 8f;
                int frame = ((int)(time * 38) + i * 3) % 8;
                float edge = size * (.12f + (i % 3) * .017f);
                DrawTextureRectRegion(sprite, new Rect2(center - Vector2.One * edge * .5f, Vector2.One * edge),
                    new Rect2(frame * cellSize, 0, cellSize, cellSize), new Color(Colors.White, opacity));
            }
        }
    }
}
