using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
partial class CommandWindow {
    static readonly string[] PrologueText = {
        "多年战事结束后，沿岸各部仍各有武装，靠一纸停火协议共用灰港和河湾公路。\n内陆矿石从这里出海，海运来的粮食、燃料和设备则沿公路送往各地。",
        "这天夜里，长途通信和港务调度相继中断，互相矛盾的封路令陆续传来。\n沿线分部相继自行设卡，停火协议在这一夜被撕毁。",
        "进港的货车被当地驻军扣留，司令部的放行令沦为废纸。\n驻军随后占住旧码头，原本靠泊的补给船因此被迫退到外海。",
        "封港的消息传到外港，司令部当即下令，命先遣队夺回旧码头。",
        "部队从码头西侧的浅滩登陆，作为指挥官，你需要带领你的部队，击退驻军，夺回码头。"
    };
    static readonly int[] ProloguePages = { 0, 0, 1, 2, 3, 3, 4 };
    const string ChapterRadioSpeaker = "无线电 · 许岑 / 作战参谋";
    static readonly string[] DepartureText = {
        "干得漂亮！旧码头的善后工作就交给保障队了。",
        "指挥官，河湾西岸来报，北线和白石都拒绝撤卡。两边正在争夺东岸桥头。",
        "司令部命令我们夺回大桥，肃清东岸叛军，把公路重新打开。",
        "让我们收拢先遣队，向河湾进发。"
    };
    static readonly string[] ChapterCompletionText = {
        "保障队接管旧码头，补给船恢复靠泊。",
        "先遣队正沿公路前往河湾，准备夺回大桥。"
    };
    PointF chapterCameraFrom;
    double chapterCaptionSeconds, chapterHoldSeconds;

    bool ChapterScenePlaying { get { return RiverbendScenePlaying || !briefing && world != null && world.HarborOperation != null && world.HarborOperation.Playing; } }
    bool HarborDepartureResult { get { return !briefing && world != null && world.HarborOperation != null && world.HarborOperation.Scene == HarborScene.Complete; } }

    bool HarborDeparturePending {
        get {
            return !briefing && world != null && world.HarborOperation != null && world.Won && world.HarborOperation.Scene == HarborScene.Battle;
        }
    }
    bool ChapterControlsLocked { get { return ChapterScenePlaying || HarborDeparturePending || RiverbendDeparturePending; } }

    void StartFirstChapter() {
        Restart();
        world = Campaign.OpenGreyhaven();
        world.PrepareBattleNavigation();
        riverMap = world.River;
        EnsureRiverArt();
        ClearRiverGroundCache();
        selection.Clear();
        briefing = false;
        hits.Clear();
        zoom = DefaultZoom;
        CenterAt(RiverbendMap.PortPoint(66.4f, 31.25f));
        chapterCameraFrom = camera;
        chapterCaptionSeconds = chapterHoldSeconds = 0;
        last = watch.Elapsed.TotalSeconds;
        hudDirty = true;
        Invalidate();
    }

    void AdvanceChapterScene(double elapsed, bool focused) {
        if (paused || !focused) {
            simulation.Reset();
            return;
        }
        simulation.Advance(elapsed, MatchTempo.DefaultRate, TickChapterScene);
        PointChapterCamera(world.HarborOperation);
    }

    void TickChapterScene(float step) {
        var operation = world.HarborOperation;
        bool wasReady = operation.Ready;
        operation.Advance(step);
        if (!operation.Playing || operation.Scene == HarborScene.LandParty) return;
        double seconds = step / MatchTempo.DefaultRate;
        chapterCaptionSeconds += seconds;
        if (!wasReady) return;
        bool sameCaption = false;
        string caption;
        if (operation.Opening) {
            int page = ProloguePages[(int)operation.Scene];
            sameCaption = page == ProloguePages[(int)operation.Scene + 1];
            caption = PrologueText[page];
        } else {
            caption = DepartureText[(int)operation.Scene - (int)HarborScene.ReopenGate];
        }
        chapterHoldSeconds += seconds;
        double readSeconds = Math.Max(3, .95 + caption.Length / 8.5);
        bool canTransition = operation.Scene >= HarborScene.Muster || chapterHoldSeconds >= .8;
        if (sameCaption || (canTransition && chapterCaptionSeconds >= readSeconds)) {
            PointChapterCamera(operation);
            AdvanceChapterShot(operation);
        }
    }

    void BeginHarborDeparture() {
        if (paused || world.HarborOperation == null || !world.HarborOperation.BeginDeparture()) return;
        help = false;
        ResetClassicInput();
        ResetOriginalControls();
        ResetEdgeScroll();
        selection.Clear();
        hits.Clear();
        drag = panning = false;
        Capture = false;
        chapterCameraFrom = camera;
        chapterCaptionSeconds = chapterHoldSeconds = 0;
        hudDirty = true;
    }

    void ContinueChapterScene() {
        if (RiverbendScenePlaying) { TakeRiverbendCommand(); return; }
        var operation = world.HarborOperation;
        if (operation != null && operation.Scene == HarborScene.LandParty) AdvanceChapterShot(operation);
    }

    void AdvanceChapterShot(HarborOperation operation) {
        var previous = operation.Scene;
        if (paused || !operation.Continue()) return;
        chapterHoldSeconds = 0;
        if (!operation.Opening || ProloguePages[(int)previous] != ProloguePages[(int)operation.Scene])
            chapterCaptionSeconds = 0;
        chapterCameraFrom = camera;
        hits.Clear();
        if (operation.Scene == HarborScene.Battle) {
            selection.Add(operation.CommandVehicle);
            edgeArmed = true;
        }
        hudDirty = true;
        Invalidate();
    }

    void ChapterPan(float x, float y, float time) {
        CenterAt(HarborOperation.Glide(chapterCameraFrom, RiverbendMap.PortPoint(x, y), time));
    }

    void PointChapterCamera(HarborOperation operation) {
        float t = operation.Elapsed;
        switch (operation.Scene) {
            case HarborScene.SupplyRoad:
                ChapterPan(63.45f, 31.25f, t / 3.2f);
                break;
            case HarborScene.QuayWork:
                ChapterPan(49f, 22.1f, t / 2.4f);
                break;
            case HarborScene.Checkpoint:
                ChapterPan(61.55f, 31.65f, t / 2.65f);
                break;
            case HarborScene.ApproachShore:
                PanAlongShore(t / 6.7f);
                break;
            case HarborScene.LandParty:
                CenterAt(HarborOperation.Glide(chapterCameraFrom, world.Home(0), (t - .85f) / 3.4f));
                break;
            case HarborScene.ReopenGate:
                ChapterPan(59, 31.65f, t / 2.1f);
                break;
            case HarborScene.ReturnShip:
                ChapterPan(49f, 22.1f, t / 2.7f);
                break;
            case HarborScene.Muster:
                var focus = operation.DepartureBase == null ? operation.DepartureBaseSite : operation.DepartureBase.P;
                var gate = RiverbendMap.HarborGateCenter;
                float lead = HarborOperation.Blend(0, 3 * Campaign.Tile, (t - 3.65f) / 1.8f);
                var ahead = new PointF(Math.Min(focus.X + lead, gate.X + Campaign.Tile), focus.Y);
                CenterAt(HarborOperation.Glide(chapterCameraFrom, ahead, t / 3f));
                break;
            case HarborScene.Dispatch:
            case HarborScene.Complete:
                if (operation.Scene == HarborScene.Complete) {
                    CenterAt(HarborOperation.DispatchCamera);
                    break;
                }
                CenterAt(HarborOperation.Glide(chapterCameraFrom, HarborOperation.DispatchCamera, t / 2.3f));
                break;
        }
    }

    void PanAlongShore(float time) {
        float t = HarborOperation.Blend(0, 1, time);
        float inv = 1 - t;
        var a = chapterCameraFrom;
        var b = RiverbendMap.PortPoint(47, 28);
        var c = RiverbendMap.PortPoint(30, 37);
        var d = RiverbendMap.PortPoint(16.6f, 45.4f);
        CenterAt(new PointF(
            a.X * inv * inv * inv + 3 * b.X * inv * inv * t + 3 * c.X * inv * t * t + d.X * t * t * t,
            a.Y * inv * inv * inv + 3 * b.Y * inv * inv * t + 3 * c.Y * inv * t * t + d.Y * t * t * t));
    }

    void DrawChapterScene(Graphics g) {
        var operation = world.HarborOperation;

        if (operation.Darkness > 0)
            Rect(g, 0, 0, W, H, Color.FromArgb((int)(216 * operation.Darkness), C("#0b1729")));

        Rect(g, 0, 0, W, 50, Color.FromArgb(232, ink));
        Rect(g, 34, 17, 4, 17, gold);
        Txt(g, "重连灰港  /  第一章", 48, 14, 18, pale, true);

        if (operation.Scene == HarborScene.Complete) {
            DrawChapterDepartureResult(g);
            return;
        }
        if (operation.Departing) {
            DrawChapterDepartureRadio(g, DepartureText[(int)operation.Scene - (int)HarborScene.ReopenGate]);
            return;
        }

        string button = operation.Scene == HarborScene.LandParty && operation.Ready && !paused ? "接管先遣队" : null;
        DrawHarborCaption(g, PrologueText[ProloguePages[(int)operation.Scene]], button);
    }

    void DrawChapterDepartureRadio(Graphics g, string text, float top = 62, float width = 772) {
        int lines = text.Count(character => character == '\n') + 1;
        var box = new RectangleF(16, top, width, 65 + (lines - 1) * 23);
        Plate(g, box, Color.FromArgb(245, panel), C("#ac925f"));
        Rect(g, box.X + 1, box.Y + 10, 4, box.Height - 20, gold);
        Txt(g, ChapterRadioSpeaker, box.X + 15, box.Y + 8, 13, gold, true);
        BriefingParagraph(g, text, new RectangleF(box.X + 15, box.Y + 32, box.Width - 30, box.Height - 38), 18, pale);
    }

    void DrawChapterDepartureResult(Graphics g) {
        DrawCampaignDepartureResult(g, MissionBriefing.Prologue.Title, ChapterCompletionText, "下一关 · 河湾镇", StartRiverMission);
    }

    void DrawCampaignDepartureResult(Graphics g, string title, string[] lines, string next, Action continueCampaign) {
        var box = new RectangleF(24, H - 224, 556, 200);
        Plate(g, box, Color.FromArgb(246, panel), C("#a2b398"));
        Rect(g, box.X + 1, box.Y + 12, 4, 47, gold);
        Txt(g, "行动成功", box.X + 19, box.Y + 13, 30, pale, true);
        Txt(g, title, box.X + 160, box.Y + 24, 16, gold);
        Line(g, box.X + 20, box.Y + 57, box.Right - 20, box.Y + 57, C("#5d7265"));
        Txt(g, lines[0], box.X + 20, box.Y + 67, 16, pale);
        Txt(g, lines[1], box.X + 20, box.Y + 90, 16, pale);
        Txt(g, BattleSummary(), box.X + 20, box.Y + 118, 14, dim);
        Button(g, box.X + 20, box.Y + 150, 181, 35, next, continueCampaign, true);
        Button(g, box.X + 212, box.Y + 150, 142, 35, "返回选关", OpenMissions);
    }

    void DrawChapterPause(Graphics g) {
        hits.Clear();
        Rect(g, 0, 0, W, H, Color.FromArgb(171, ink));
        var box = new RectangleF((W - 490) * .5f, (H - 216) * .5f, 490, 216);
        Plate(g, box, C("#1f302f"), C("#a2b398"));
        Txt(g, "演出已暂停", box.X + 30, box.Y + 26, 35, pale, true);
        Line(g, box.X + 32, box.Y + 85, box.Right - 32, box.Y + 85, C("#5d7265"));
        Button(g, box.X + 32, box.Y + 112, 200, 45, "继续演出", OpenOptions, true);
        Button(g, box.X + 258, box.Y + 112, 200, 45, "返回选关", OpenMissions);
        Txt(g, "Enter", box.X + 37, box.Y + 168, 13, dim);
        Txt(g, "Esc", box.X + 263, box.Y + 168, 13, dim);
    }

    void DrawHarborCaption(Graphics g, string text, string button) {
        bool twoLines = text.Contains("\n");
        float height = twoLines ? 72 : 56;
        var box = new RectangleF(30, H - height - 22, W - 60, height);
        Plate(g, box, Color.FromArgb(245, C("#1c2d30")), C("#7c8a76"));
        Rect(g, box.X + 1, box.Y + 10, 4, height - 20, gold);
        float inset = twoLines ? 10 : 15;
        BriefingParagraph(g, text, new RectangleF(box.X + 20, box.Y + inset, box.Width - (button == null ? 40 : 242), height - inset), 20, pale, harborOpening: true);
        if (button != null) {
            float top = box.Y + (height - 39) * .5f;
            Button(g, box.Right - 211, top, 188, 39, button, ContinueChapterScene, true);
            Txt(g, "Enter", box.Right - 76, top + 11, 12, ink, true);
        }
    }

    void DrawChapterCoach(Graphics g, string advice) {
        var box = new RectangleF(16, 12, 857, 108);
        hits.Add(new HitBox { Box = box, Run = () => { } });
        Plate(g, box, Color.FromArgb(245, panel), C("#ac925f"));
        Rect(g, box.X + 1, box.Y + 10, 4, 49, gold);
        Txt(g, ChapterRadioSpeaker, box.X + 15, box.Y + 8, 13, gold, true);
        int lesson = world.Lesson;
        string phase = lesson < 2 ? "建立滩头基地" : lesson < 9 ? "让补给运转起来" : "清除码头守军";
        Txt(g, phase, box.X + 15, box.Y + 33, 20, pale, true);
        Txt(g, world.LessonTitle, box.X + 213, box.Y + 37, 15, dim);
        string instruction = advice ?? LessonHint();
        if (lesson == 0 && !world.Landing)
            instruction = "指挥官，我是许岑，原定的训练由我负责。我们先把滩头站稳。\n" + instruction;
        BriefingParagraph(g, instruction, new RectangleF(box.X + 15, box.Y + 63, 713, 41), 14, pale);
        Button(g, box.Right - 111, box.Y + 25, 98, 39, "定位本步", FocusLesson);

        var delivery = world.Live(0).FirstOrDefault(o => o.LastReceipt > 0 && world.OreReceipts[0] == o.LastReceipt);
        if (delivery != null && world.Time - delivery.ReceiptAt < 11) {
            var note = new RectangleF(box.X, box.Bottom + 6, 660, 37);
            Plate(g, note, Color.FromArgb(237, ink), C("#637c6c"));
            Txt(g, "许岑  /  第一车矿已入账，滩头基地可以自行整备部队。", note.X + 14, note.Y + 8, 15, pale);
        }
    }
}
}
