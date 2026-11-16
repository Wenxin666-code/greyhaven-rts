using System;
using System.Linq;
using Godot;
using Greyhaven;
using Point = System.Drawing.PointF;

public partial class BattlefieldView
{
    private FreightSprites freightSprites;

    private void DrawBasinFreight(BasinFreight train)
    {
        var cars = new[] { train.Engine, train.Wagon(0), train.Wagon(1) };
        bool Visible(BasinFreight.Car car) => train.Speed > 0
            ? Area.Grow(105).HasPoint(Screen(car.Position))
            : InShot(car.Position);
        var onCamera = cars.Select(Visible).ToArray();
        if (!onCamera.Any(value => value)) return;
        for (int i = 0; i < cars.Length; i++)
            if (onCamera[i]) DrawFreightShadow(cars[i]);
        for (int i = 0; i < cars.Length - 1; i++)
            if (onCamera[i] || onCamera[i + 1])
                DrawLine(Screen(FreightPoint(cars[i], -1.48f, 0), .20f),
                    Screen(FreightPoint(cars[i + 1], 1.48f, 0), .20f), new Color("#302e28"), 2.2f * Zoom);
        foreach (int i in Enumerable.Range(0, cars.Length).OrderBy(i => cars[i].Position.X + cars[i].Position.Y))
        {
            if (!onCamera[i]) continue;
            var car = cars[i];
            var at = Screen(car.Position);
            var heading = Screen(FreightPoint(car, 1, 0)) - at;
            int cargo = i == 0 ? -1 : train.Loads >= i ? i : 0;
            var frame = freightSprites.At(heading, Zoom, cargo);
            DrawSetTransform(at, frame.Turn, Vector2.One);
            DrawTextureRectRegion(frame.Body, frame.Destination, frame.Source);
            if (frame.Paint != null) DrawTextureRectRegion(frame.Paint, frame.Destination, frame.Source, BattleArt.Faction(0));
            DrawSetTransform(Vector2.Zero);
        }
        if (train.Speed < 4 || !onCamera[0]) return;
        var exhaust = Screen(FreightPoint(train.Engine, -.64f, 0), 1.3f);
        var forward = (Screen(FreightPoint(train.Engine, 1, 0)) - Screen(train.Engine.Position)).Normalized();
        for (int i = 0; i < 8; i++)
        {
            float age = (World.VisualTime * .72f + i / 8f) % 1;
            var position = exhaust - forward * (14 + train.Speed * .25f) * age * Zoom - Vector2.Down * age * 18 * Zoom;
            DrawCircle(position, (1.4f + age * 4.7f) * Zoom, new Color("#a49f91", .23f * (1 - age)));
        }
    }

    private void DrawFreightShadow(BasinFreight.Car car)
    {
        Vector2 At(float along, float across) => Screen(FreightPoint(car, along, across));
        var shadow = new[] { At(-1.67f, -.61f), At(1.67f, -.61f), At(1.67f, .61f), At(-1.67f, .61f) };
        DrawColoredPolygon(shadow.Select(point => point + new Vector2(4, 3) * Zoom).ToArray(), new Color("#11191432"));
        DrawColoredPolygon(shadow, new Color("#15191457"));
    }

    private static Point FreightPoint(BasinFreight.Car car, float along, float across) =>
        new(car.Position.X + Campaign.Tile * (car.Direction.X * along - car.Direction.Y * across),
            car.Position.Y + Campaign.Tile * (car.Direction.Y * along + car.Direction.X * across));

}
