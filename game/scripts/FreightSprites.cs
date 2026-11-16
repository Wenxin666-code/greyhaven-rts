using System;
using Godot;
using Greyhaven;

internal sealed class FreightSprites
{
    private const int Cell = 256, Views = 16;
    private const float GroundAnchor = 188f / 320;
    private const float ModelLength = 8.84f, CameraWidth = 10.3f, RailLength = 3.28f;
    private const string Assets = "res://assets/freight-model/";
    private readonly Texture2D engine = GD.Load<Texture2D>(Assets + "engine-base.png");
    private readonly Texture2D paint = GD.Load<Texture2D>(Assets + "engine-paint.png");
    private readonly Texture2D[] wagons =
    {
        GD.Load<Texture2D>(Assets + "empty-base.png"),
        GD.Load<Texture2D>(Assets + "crates-base.png"),
        GD.Load<Texture2D>(Assets + "tarp-base.png")
    };

    internal readonly record struct Frame(Texture2D Body, Texture2D Paint, Rect2 Source, Rect2 Destination, float Turn);

    internal Frame At(Vector2 heading, float zoom, int cargo)
    {
        float yaw = MathF.Atan2(heading.X, 2 * heading.Y);
        int direction = (Mathf.RoundToInt(yaw * Views / MathF.Tau) + Views) % Views;
        float bakedYaw = direction * MathF.Tau / Views;
        float bakedBearing = MathF.Atan2(.5f * MathF.Cos(bakedYaw), MathF.Sin(bakedYaw));
        float difference = heading.Angle() - bakedBearing;
        float turn = MathF.Atan2(MathF.Sin(difference), MathF.Cos(difference));
        float span = CameraWidth * Campaign.Tile * RailLength / ModelLength * BattlefieldView.Axis * MathF.Sqrt(2) * zoom;
        var source = new Rect2((direction % 8) * Cell, (direction / 8) * Cell, Cell, Cell);
        var target = new Rect2(-span * .5f, -span * GroundAnchor, span, span);
        return cargo < 0 ? new Frame(engine, paint, source, target, turn)
            : new Frame(wagons[cargo], null, source, target, turn);
    }
}
