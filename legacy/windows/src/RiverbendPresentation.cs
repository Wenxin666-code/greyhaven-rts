using System;
using System.Drawing;

namespace Greyhaven {
partial class CommandWindow {
    static readonly string[] RiverbendDepartureRadio = {
        "指挥官，桥上可以放行了。河湾的善后工作交给保障队。",
        "北线和白石在矿业盆地还有驻军。刚收到消息，另外三路武装也在那里设了营。",
        "沿这条公路往内陆走。我们先去西北保障岛，趁他们还在争矿区，\n把通往岛上的堤道守住。"
    };
    static readonly string[] RiverbendCompletion = {
        "河湾大桥恢复通行，保障队接管桥头。",
        "先遣队沿内陆公路向西北保障岛进发。"
    };
    PointF riverbendDepartureCamera, riverbendDepartureFollow;

    bool RiverbendScenePlaying {
        get {
            var operation = world == null ? null : world.RiverbendOperation;
            return !briefing && operation != null && (operation.Opening || operation.Departing);
        }
    }
    bool RiverbendResultPlaying { get { return !briefing && world != null && world.RiverbendOperation != null && world.RiverbendOperation.Result; } }
    float RiverbendSceneFog {
        get {
            var operation = world.RiverbendOperation;
            if (!RiverbendScenePlaying && !RiverbendResultPlaying) return 1;
            return operation.Opening ? HarborOperation.Blend(.67f, 1, (operation.Elapsed - 12.2f) / 4.9f) : .67f;
        }
    }
    bool RiverbendDeparturePending {
        get { return !briefing && world != null && world.Won && world.RiverbendOperation != null && world.RiverbendOperation.Scene == RiverbendScene.Battle; }
    }

    void BeginRiverbendOpening() {
        world.RiverbendOperation.BeginOpening();
        selection.Clear();
        ResetEdgeScroll();
        CenterAt(RiverbendMap.PortPoint(22, 34));
        last = watch.Elapsed.TotalSeconds;
    }

    void TakeRiverbendCommand() {
        var operation = world.RiverbendOperation;
        if (paused || operation == null || !operation.TakeCommand()) return;
        selection.Clear();
        selection.Add(operation.CommandVehicle);
        CenterAt(operation.CommandVehicle.P);
        hits.Clear();
        simulation.Reset();
        edgeArmed = true;
        hudDirty = true;
        Invalidate();
    }

    void BeginRiverbendDeparture() {
        var operation = world.RiverbendOperation;
        if (operation == null || !operation.BeginDeparture()) return;
        riverbendDepartureCamera = camera;
        riverbendDepartureFollow = operation.DepartureSubject.P;
        selection.Clear();
        ResetEdgeScroll();
        ResetClassicInput();
        ResetOriginalControls();
        hits.Clear();
        drag = panning = false;
        Capture = false;
        hudDirty = true;
    }

    void AdvanceRiverbendScene(double elapsed, bool focused) {
        if (paused || !focused) { simulation.Reset(); return; }
        var operation = world.RiverbendOperation;
        simulation.Advance(elapsed, MatchTempo.DefaultRate, operation.Advance);
        float t = operation.Elapsed;
        var west = world.Home(0);
        var bridge = operation.Bridge;
        var junction = RiverbendMap.PortPoint(RiverbendMap.RiverJunction.X, RiverbendMap.RiverJunction.Y);
        if (operation.Opening) {
            var road = RiverbendMap.PortPoint(22, 34);
            var eastBridge = RiverbendMap.PortPoint(bridge.Right + .5f, 33);
            if (t < 4.5f) CenterAt(HarborOperation.Glide(road, west, t / 4.5f));
            else if (t < 8.8f) CenterAt(HarborOperation.Glide(west, eastBridge, (t - 4.5f) / 4.3f));
            else if (t < 12.2f) CenterAt(HarborOperation.Glide(eastBridge, junction, (t - 8.8f) / 3.4f));
            else CenterAt(HarborOperation.Glide(junction, west, (t - 12.2f) / 4.9f));
        } else if (operation.Departing || operation.Result) {
            var subject = operation.DepartureFocus;
            var stop = RiverbendOperation.DepartureCameraStop;
            float lead = subject.X + 2.1f * Campaign.Tile;
            var roadAhead = HarborOperation.Glide(new PointF(Math.Min(lead, stop.X), subject.Y), stop,
                (lead - stop.X + 5 * Campaign.Tile) / (5 * Campaign.Tile));
            float follow = (float)(1 - Math.Exp(-3.6 * Math.Min(elapsed, .25)));
            riverbendDepartureFollow = new PointF(
                HarborOperation.Blend(riverbendDepartureFollow.X, roadAhead.X, follow),
                HarborOperation.Blend(riverbendDepartureFollow.Y, roadAhead.Y, follow));
            CenterAt(HarborOperation.Glide(riverbendDepartureCamera, riverbendDepartureFollow, t / 2.6f));
        }
    }

    void DrawRiverbendScene(Graphics g) {
        var operation = world.RiverbendOperation;
        Rect(g, 0, 0, W, 50, Color.FromArgb(232, ink));
        Rect(g, 34, 17, 4, 17, gold);
        Txt(g, "河湾镇  /  第二章", 48, 14, 18, pale, true);
        if (operation.Result) {
            var lines = operation.HasDepartureTroops ? RiverbendCompletion :
                new[] { RiverbendCompletion[0], "司令部接令，将从内陆公路向西北保障岛推进。" };
            DrawCampaignDepartureResult(g, MissionBriefing.Riverbend.Title, lines, "下一关 · 孤岛要塞", StartIslandMission);
        } else if (operation.Departing) {
            int page = operation.Elapsed < 4.1f ? 0 : operation.Elapsed < 8.4f ? 1 : 2;
            DrawChapterDepartureRadio(g, RiverbendDepartureRadio[page], 62, 880);
        } else if (operation.Ready && !paused) {
            Button(g, W - 240, H - 79, 204, 46, "接管先遣队", TakeRiverbendCommand, true);
            Txt(g, "Enter", W - 97, H - 63, 13, ink, true);
        }
    }

    void DrawRiverbendRoadside(Graphics g) {
        if (world.RiverbendOperation == null) return;
        var exit = RiverbendMap.PortPoint(RiverbendMap.RiverExitSign.X, RiverbendMap.RiverExitSign.Y);
        if (InHarborShot(exit, false)) DrawHarborRoadSign(g, exit, "矿业盆地 ›");
    }

}
}
