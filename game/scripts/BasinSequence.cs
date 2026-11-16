using System;
using Greyhaven;
using Point = System.Drawing.PointF;

internal sealed class BasinSequence : ChapterSequence
{
    private enum Stage { Opening, Battle, Closing, Result }
    private const float HoldPerCheckpoint = 3.65f;
    private static readonly float[] CheckpointZoom = { 1.19f, .99f, 1.21f, .75f, 1.05f };
    private readonly Campaign world;
    private readonly BasinOperation operation;
    private Stage stage;
    private float elapsed, runningTime;
    private float firstCheckpointAt = float.PositiveInfinity;
    private int revealedCheckpoint;
    private Point closingCamera, lastConvoyReveal;
    private float LookToBaseAt => firstCheckpointAt + HoldPerCheckpoint * CheckpointZoom.Length;
    private float HandoffAt => LookToBaseAt + 3.6f;
    internal float Elapsed => elapsed;
    internal int ReconSide => Opening && elapsed >= firstCheckpointAt && elapsed < LookToBaseAt
        ? 1 + (int)((elapsed - firstCheckpointAt) / HoldPerCheckpoint) : 0;
    internal float CameraZoom => ReconSide > 0 ? CheckpointZoom[ReconSide - 1] : BattlefieldView.DefaultZoom;
    internal float OpeningShade
    {
        get
        {
            if (!Opening || float.IsPositiveInfinity(firstCheckpointAt)) return 0;
            float closestCut = Math.Clamp(MathF.Round((elapsed - firstCheckpointAt) / HoldPerCheckpoint),
                0, CheckpointZoom.Length);
            float timeFromCut = MathF.Abs(elapsed - (firstCheckpointAt + closestCut * HoldPerCheckpoint));
            return HarborOperation.Blend(0, 1, 1 - timeFromCut / .23f);
        }
    }

    internal BasinSequence(Campaign world)
    {
        this.world = world;
        operation = world.BasinOperation;
        operation.BeginArrival();
        world.RevealBasinScene(RiverbendMap.PortPoint(2, 115), Campaign.Tile * 20);
        world.RevealBasinScene(BasinOperation.Layby, Campaign.Tile * 17);
    }

    internal override bool Opening => stage == Stage.Opening;
    internal override bool Departing => stage == Stage.Closing;
    internal override bool Complete => stage == Stage.Result;
    internal override bool ResultReady => Complete || Departing && operation.Freight.DepartureProgress >= .5f;
    internal override bool CanTakeCommand => Opening && elapsed >= HandoffAt;
    internal override Piece CommandVehicle => operation.CommandVehicle;
    internal override float Fog => 1;
    internal string OpeningSpeaker => ReconSide > 0 ? "无线电 · 先遣侦察组"
        : elapsed >= 4.55f && elapsed < firstCheckpointAt ? "无线电 · 换装站值班员" : Speaker;
    internal string ClosingSpeaker => runningTime <= 0 ? Speaker
        : runningTime < 4.6f ? "无线电 · 换装站值班员"
        : runningTime < 11.8f ? "无线电 · 周砚 / 工程官" : Speaker;
    internal override string Caption => Opening
        ? elapsed < 4.55f ? BasinOperation.OpeningRadio : elapsed < firstCheckpointAt ? BasinOperation.LoadingRadio
            : ReconSide is int side && side > 0 ? CheckpointCaption(side) : BasinOperation.StagingRadio
        : runningTime <= 0 ? BasinOperation.ClearRailRadio : runningTime < 4.6f ? BasinOperation.EndRadio
            : runningTime < 11.8f ? BasinOperation.LeadRadio : runningTime < 16.8f ? BasinOperation.GoToRelayRadio : "";
    internal override string FieldRadio => operation.Guidance;
    internal override string Failure => "先遣队失去作战能力，补给列车仍停在换装站。";
    internal override string[] Debrief => new[] { "铁路已恢复通行，首列补给车正开往石门。", "五份调度令归档，先遣队转赴雾脊中继站查证。" };

    private string CheckpointCaption(int side) => side switch
    {
        1 => "北线驻防军守着北坡道口。墙砌在钢轨上，列车到这儿就得停。",
        2 => "白石警卫队守着装矿道口。出站的铁轨也堵了，矿车还在跑。",
        3 => "铁轨卫队封了折返道岔。去南堤的列车都得打这儿过。",
        4 => "南岸防卫队的碉堡贴着正线。过南堤得从他们眼皮底下走。",
        _ => "边境独立旅守着石门东道口。过了这道卡，前面就是石门。"
    };

    internal Point CheckpointGate(int side)
    {
        var barrier = world.River.BasinRailBarrier(side);
        return Campaign.Middle(new System.Drawing.RectangleF(barrier.X * Campaign.Tile,
            barrier.Y * Campaign.Tile, barrier.Width * Campaign.Tile, barrier.Height * Campaign.Tile));
    }

    internal override bool TakeCommand()
    {
        if (!CanTakeCommand) return false;
        stage = Stage.Battle;
        operation.HandOff();
        return true;
    }

    internal override bool BeginDeparture(Point from)
    {
        if (stage != Stage.Battle || !world.Won || !world.OutcomeReady) return false;
        stage = Stage.Closing;
        operation.BeginFreightDeparture();
        closingCamera = lastConvoyReveal = operation.Freight.Engine.Position;
        world.RevealBasinScene(closingCamera, Campaign.Tile * 24);
        elapsed = 0;
        return true;
    }

    internal override void Tick(float step)
    {
        if (Complete) return;
        if (Opening)
        {
            if (CanTakeCommand) return;
            elapsed += step / MatchTempo.DefaultRate;
            int side = ReconSide;
            if (side > revealedCheckpoint)
            {
                revealedCheckpoint = side;
                world.RevealBasinScene(CheckpointGate(side), Campaign.Tile * 19);
                world.RevealBasinScene(world.Home(side), Campaign.Tile * 12);
                float midpointRadius = side == 4 ? 30 * Campaign.Tile : 16 * Campaign.Tile;
                world.RevealBasinScene(HarborOperation.Glide(CheckpointGate(side), world.Home(side), .5f), midpointRadius);
            }
            world.Tick(step);
            operation.AdvanceArrival(step);
            if (float.IsPositiveInfinity(firstCheckpointAt) && operation.Freight.Loads == 2)
                firstCheckpointAt = Math.Max(9.2f, elapsed + 1.55f);
            return;
        }
        elapsed += step / MatchTempo.DefaultRate;
        if (Departing)
        {
            world.TickVisuals(step);
            if (!operation.Freight.RouteClear) operation.Freight.ClearTrack(step);
            if (operation.Freight.RouteClear && elapsed >= .9f)
            {
                operation.Freight.Roll(step);
                runningTime += step / MatchTempo.DefaultRate;
            }
            var engine = operation.Freight.Engine;
            var convoy = engine.Position;
            var lead = new Point(convoy.X + engine.Direction.X * Campaign.Tile * 2.5f,
                convoy.Y + engine.Direction.Y * Campaign.Tile * 2.5f);
            closingCamera = HarborOperation.Glide(closingCamera, lead, step * 7);
            if (Campaign.Dist(lastConvoyReveal, convoy) >= Campaign.Tile * 3)
            {
                world.RevealBasinScene(convoy, Campaign.Tile * 23);
                lastConvoyReveal = convoy;
            }
            if (operation.Freight.AtEnd)
                stage = Stage.Result;
        }
    }

    internal override Point OpeningPanTarget => BasinOperation.Layby;

    internal override Point Camera()
    {
        if (Departing || Complete) return closingCamera;
        if (elapsed < firstCheckpointAt)
            return HarborOperation.Glide(operation.ConvoyFocus, OpeningPanTarget, elapsed / 4.7f);
        if (ReconSide is int side && side > 0)
        {
            float progress = (elapsed - firstCheckpointAt - (side - 1) * HoldPerCheckpoint) / HoldPerCheckpoint;
            float aim = (side == 4 ? .39f : .48f) + .075f * progress;
            return HarborOperation.Glide(CheckpointGate(side), world.Home(side), aim);
        }
        return CommandVehicle.P;
    }
}
