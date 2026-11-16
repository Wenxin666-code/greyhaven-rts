using Greyhaven;
using System;
using Point = System.Drawing.PointF;

internal sealed class RiverbendSequence : ChapterSequence
{
    private readonly Campaign world;
    private readonly RiverbendOperation operation;
    private Point departureFrom, departureCamera;
    private static readonly string[] Departure =
    {
        "指挥官，桥上可以放行了。河湾的善后工作交给保障队。",
        "头两辆货车已经过桥。让司机沿公路把补给送到盆地换装站。",
        "再往石门得换铁路。指挥官，我们沿公路跟上。"
    };

    internal RiverbendSequence(Campaign world)
    {
        this.world = world;
        operation = world.RiverbendOperation;
        operation.BeginOpening();
    }

    internal override bool Opening => operation.Opening;
    internal override bool Departing => operation.Departing;
    internal override bool Complete => operation.Result;
    internal override bool CanTakeCommand => operation.Ready;
    internal override Piece CommandVehicle => operation.CommandVehicle;
    internal override float Fog => Opening ? HarborOperation.Blend(.67f, 1, (operation.Elapsed - 12.2f) / 4.9f) : .67f;
    internal override string FieldRadio => operation.Guidance;
    internal override string Caption => Departing ? Departure[operation.Elapsed < 4.1f ? 0 : operation.Elapsed < 8.4f ? 1 : 2] : "";
    internal override string[] Debrief => new[] { "河湾大桥恢复通行，保障队接管桥头。",
        operation.HasDepartureTroops ? "先遣队沿公路前往盆地换装站。" : "司令部将另派分队前往盆地换装站。" };
    internal override string Failure => "先遣队未能打通河湾大桥。";

    internal override bool TakeCommand() => operation.TakeCommand();

    internal override bool BeginDeparture(Point from)
    {
        if (!operation.BeginDeparture()) return false;
        departureFrom = from;
        departureCamera = operation.DepartureSubject.P;
        return true;
    }

    internal override void Tick(float step)
    {
        operation.Advance(step);
        if (!Departing && !Complete) return;
        var subject = operation.DepartureFocus;
        var stop = RiverbendOperation.DepartureCameraStop;
        float lead = subject.X + 2.1f * Campaign.Tile;
        var roadAhead = HarborOperation.Glide(new Point(Math.Min(lead, stop.X), subject.Y), stop,
            (lead - stop.X + 5 * Campaign.Tile) / (5 * Campaign.Tile));
        float follow = 1 - MathF.Exp(-3.6f * step / MatchTempo.DefaultRate);
        departureCamera = new Point(departureCamera.X + (roadAhead.X - departureCamera.X) * follow,
            departureCamera.Y + (roadAhead.Y - departureCamera.Y) * follow);
    }

    internal override Point Camera()
    {
        float t = operation.Elapsed;
        if (Departing || Complete) return HarborOperation.Glide(departureFrom, departureCamera, t / 2.6f);
        var west = world.Home(0);
        var bridge = RiverbendMap.PortPoint(operation.Bridge.Right + .5f, 33);
        var junction = RiverbendMap.PortPoint(RiverbendMap.RiverJunction.X, RiverbendMap.RiverJunction.Y);
        if (t < 4.5f) return HarborOperation.Glide(RiverbendMap.PortPoint(22, 34), west, t / 4.5f);
        if (t < 8.8f) return HarborOperation.Glide(west, bridge, (t - 4.5f) / 4.3f);
        if (t < 12.2f) return HarborOperation.Glide(bridge, junction, (t - 8.8f) / 3.4f);
        return HarborOperation.Glide(junction, west, (t - 12.2f) / 4.9f);
    }
}
