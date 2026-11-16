using System;
using System.Drawing;
using Greyhaven;

internal sealed class HarborSequence : ChapterSequence
{
    internal static readonly string[] CompletionText = {
        "保障队接管旧码头，补给船恢复靠泊。",
        "先遣队正沿公路前往河湾，准备夺回大桥。"
    };
    private readonly Campaign world;
    private readonly HarborOperation operation;
    private PointF cameraFrom;
    private double captionTime, holdTime;
    private static readonly int[] Pages = { 0, 0, 1, 2, 3, 3, 4 };

    internal static readonly string[] OpeningText =
    {
        "多年战事结束后，沿岸各部仍各有武装，靠一纸停火协议共用灰港和河湾公路。\n内陆矿石从这里出海，海运来的粮食、燃料和设备则沿公路送往各地。",
        "这天夜里，长途通信和港务调度相继中断，互相矛盾的封路令陆续传来。\n沿线分部相继自行设卡，停火协议在这一夜被撕毁。",
        "进港的货车被当地驻军扣留，司令部的放行令沦为废纸。\n驻军随后占住旧码头，原本靠泊的补给船因此被迫退到外海。",
        "封港的消息传到外港，司令部当即下令，命先遣队夺回旧码头。",
        "部队从码头西侧的浅滩登陆，作为指挥官，你需要带领你的部队，击退驻军，夺回码头。"
    };

    internal static readonly string[] DepartureText =
    {
        "干得漂亮！旧码头的善后工作就交给保障队了。",
        "指挥官，河湾西岸来报，北线和白石都拒绝撤卡。两边正在争夺东岸桥头。",
        "司令部命令我们夺回大桥，肃清东岸叛军，把公路重新打开。",
        "让我们收拢先遣队，向河湾进发。"
    };

    internal HarborSequence(Campaign campaign)
    {
        world = campaign;
        operation = campaign.HarborOperation;
        cameraFrom = RiverbendMap.PortPoint(66.4f, 31.25f);
    }

    internal override bool Opening => operation.Opening;
    internal override bool Departing => operation.Departing;
    internal override bool Complete => operation.Scene == HarborScene.Complete;
    internal override Piece CommandVehicle => operation.CommandVehicle;
    internal override float Fog => operation.FogBlend;
    internal override string[] Debrief => CompletionText;
    internal override string Failure => "先遣队未能夺回旧码头。";
    internal override string Caption => operation.Opening
        ? OpeningText[Pages[(int)operation.Scene]]
        : operation.Departing ? DepartureText[(int)operation.Scene - (int)HarborScene.ReopenGate] : "";

    internal override bool CanTakeCommand => operation.Scene == HarborScene.LandParty && operation.Ready;

    internal override bool TakeCommand() => CanTakeCommand && operation.Continue();

    internal override bool BeginDeparture(PointF from)
    {
        if (!operation.BeginDeparture()) return false;
        cameraFrom = from;
        captionTime = holdTime = 0;
        return true;
    }

    internal override void Tick(float step)
    {
        bool wasReady = operation.Ready;
        operation.Advance(step);
        if (!operation.Playing || operation.Scene == HarborScene.LandParty) return;
        double seconds = step / MatchTempo.DefaultRate;
        captionTime += seconds;
        if (!wasReady) return;

        holdTime += seconds;
        bool sameCaption = operation.Opening && Pages[(int)operation.Scene] == Pages[(int)operation.Scene + 1];
        bool canTransition = operation.Scene >= HarborScene.Muster || holdTime >= .8;
        if (!sameCaption && (!canTransition || captionTime < Math.Max(3, .95 + Caption.Length / 8.5))) return;

        var from = Camera();
        if (!operation.Continue()) return;
        cameraFrom = from;
        holdTime = 0;
        if (!sameCaption) captionTime = 0;
    }

    internal override PointF Camera()
    {
        float t = operation.Elapsed;
        return operation.Scene switch
        {
            HarborScene.SupplyRoad => Pan(63.45f, 31.25f, t / 3.2f),
            HarborScene.QuayWork => Pan(49, 22.1f, t / 2.4f),
            HarborScene.Checkpoint => Pan(61.55f, 31.65f, t / 2.65f),
            HarborScene.ApproachShore => PanShore(t / 6.7f),
            HarborScene.LandParty => HarborOperation.Glide(cameraFrom, world.Home(0), (t - .85f) / 3.4f),
            HarborScene.ReopenGate => Pan(59, 31.65f, t / 2.1f),
            HarborScene.ReturnShip => Pan(49, 22.1f, t / 2.7f),
            HarborScene.Muster => MusterCamera(t),
            HarborScene.Dispatch => HarborOperation.Glide(cameraFrom, HarborOperation.DispatchCamera, t / 2.3f),
            HarborScene.Complete => HarborOperation.DispatchCamera,
            _ => cameraFrom
        };
    }

    private PointF Pan(float x, float y, float amount) =>
        HarborOperation.Glide(cameraFrom, RiverbendMap.PortPoint(x, y), amount);

    private PointF MusterCamera(float time)
    {
        var focus = operation.DepartureBase?.P ?? operation.DepartureBaseSite;
        var gate = RiverbendMap.HarborGateCenter;
        float lead = HarborOperation.Blend(0, 3 * Campaign.Tile, (time - 3.65f) / 1.8f);
        var target = new PointF(Math.Min(focus.X + lead, gate.X + Campaign.Tile), focus.Y);
        return HarborOperation.Glide(cameraFrom, target, time / 3f);
    }

    private PointF PanShore(float amount)
    {
        float t = HarborOperation.Blend(0, 1, amount);
        float inv = 1 - t;
        var a = cameraFrom;
        var b = RiverbendMap.PortPoint(47, 28);
        var c = RiverbendMap.PortPoint(30, 37);
        var d = RiverbendMap.PortPoint(16.6f, 45.4f);
        return new PointF(
            a.X * inv * inv * inv + 3 * b.X * inv * inv * t + 3 * c.X * inv * t * t + d.X * t * t * t,
            a.Y * inv * inv * inv + 3 * b.Y * inv * inv * t + 3 * c.Y * inv * t * t + d.Y * t * t * t);
    }
}
