using Godot;
using Greyhaven;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private enum FrontPage { Menu, Missions, Rules, Archive }
    private enum StructureTool { None, Repair, Sell }
    private FrontPage frontPage;
    private StructureTool structureTool;
    private Control ui, panel, top, startPanel, missionPanel, rulesPanel, coach, receipt;
    private Panel messagePanel;
    private Label funds, lesson, lessonPhase, messageSpeaker, selectionText, radarOffline, stats, resultTitle, resultMission;
    private Label enterPrompt, radarNote, fieldRadioSpeaker;
    private Label chapterHeading, chosenLocation, briefCode, briefTitle, mapLocation;
    private RichTextLabel briefStory, briefOrders, briefVictory;
    private TextureRect briefMap, expandedMap;
    private Control mapViewer;
    private Label mapTitle;
    private readonly System.Collections.Generic.Dictionary<CampaignChapter, Texture2D> previews = new();
    private Button[] chapterButtons;
    private Button nextChapter, riverbendHandoff;
    private Control basinObjective;
    private ColorRect basinCut;
    private Label basinRemaining;
    private Control fieldRadio;
    private RichTextLabel fieldRadioText;
    private bool viewingMap;
    private RichTextLabel message, lessonHint;
    private Button takeCommand, repair, sell;
    private RadarView radar;
    private double battleSeconds;
    private bool showObjectives;
    private readonly HarborSkin skin = new();
    private Font uiBold;
    private ColorRect powerBar, resultRule;
    private static readonly Color Steel = new("#7c8a76"), Dim = HarborSkin.Dim, Teal = HarborSkin.Teal;
    private static readonly Regex FactionNames = new(
        "(?<joint>沿岸联合保障司令部|保障司令部|我方先遣队)|" +
        "(?<north>北线(?:驻防军|驻防分部|分部)?|当地驻军|驻军)|" +
        "(?<mine>白石(?:矿务警卫队|警卫队|分部)?(?!装矿道口))|" +
        "(?<rail>铁轨运输卫队|铁轨卫队)|(?<south>南岸地方防卫队|南岸防卫队)|" +
        "(?<border>边境独立旅|独立旅)");

    private void MakeInterface()
    {
        var layer = GetNode<CanvasLayer>("Interface");
        ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var theme = new Theme { DefaultFontSize = 15 };
        var bundled = GD.Load<Font>("res://assets/fonts/GreyhavenUI.ttf");
        theme.DefaultFont = new SystemFont
        {
            FontNames = new[] { "Microsoft YaHei UI" }, AllowSystemFallback = false,
            Fallbacks = new Godot.Collections.Array<Font> { bundled }
        };
        uiBold = new SystemFont
        {
            FontNames = new[] { "Microsoft YaHei UI" }, FontWeight = 700, AllowSystemFallback = false,
            Fallbacks = new Godot.Collections.Array<Font> { bundled }
        };
        view.CaptionFont = theme.DefaultFont;
        theme.SetColor("font_color", "Label", Pale);
        theme.SetColor("font_color", "Button", Pale);
        theme.SetColor("font_hover_color", "Button", Pale);
        theme.SetColor("font_pressed_color", "Button", Pale);
        theme.SetColor("font_disabled_color", "Button", Dim);
        theme.SetFont("font", "Button", uiBold);
        skin.Button(theme);
        ui.Theme = theme;
        layer.AddChild(ui);
        ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MakeFront();
        basinCut = Accent(ui, 0, 0, 1472, 819, new Color("#13181700"));
        basinCut.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        basinCut.Visible = false;

        top = Accent(ui, 0, 0, 1472, 50, new Color(Ink, 232 / 255f));
        Accent(top, 34, 17, 4, 17);
        chapterHeading = Bold(top, "", 48, 12, 350, 28, 18);
        var pause = AddButton(top, "暂停  Esc", 1331, 9, 124, 31, () => SetPaused(true), 15, inset: 33);
        AddPauseGlyph(pause);

        panel = SteelPanel(ui, 1304, 0, 168, 819);
        Accent(panel, 0, 0, 2, 819);
        Bold(panel, "军费", 10, 9, 39, 22, 13).Modulate = Dim;
        funds = Bold(panel, "", 48, 3, 116, 32, 23);
        funds.Modulate = Gold;
        AddButton(panel, "任务目标", 8, 44, 72, 29, () => { showObjectives = true; SetPaused(true); }, 12);
        AddButton(panel, "选项 Esc", 88, 44, 72, 29, () => SetPaused(true), 12);
        var radarTray = Accent(panel, 8, 86, 152, 104, Ink);
        Position(radarTray, 8, 86, 152, 104);
        radarOffline = Bold(radarTray, "雷达离线", 2, 26, 148, 22, 13);
        radarOffline.HorizontalAlignment = HorizontalAlignment.Center;
        radarOffline.Modulate = Dim;
        radarNote = LabelAt(radarTray, "", 2, 52, 148, 20, 10);
        radarNote.HorizontalAlignment = HorizontalAlignment.Center;
        radarNote.Modulate = Dim;
        radar = new RadarView { Position = new Vector2(1, 0), Size = new Vector2(150, 104), Field = view, Order = RadarOrder };
        radarTray.AddChild(radar);
        powerBar = Accent(panel, 2, 199, 3, 35, Teal);
        repair = AddButton(panel, "维修 [K]", 8, 197, 72, 29, () => ToggleStructureTool(StructureTool.Repair), 12);
        sell = AddButton(panel, "出售 [L]", 88, 197, 72, 29, () => ToggleStructureTool(StructureTool.Sell), 12);
        MakeProduction();
        stats = LabelAt(panel, "", 10, 773, 149, 42, 12);
        stats.Modulate = Teal;

        coach = Strip(ui, "#202c32f5", "#ac925f");
        Position(coach, 16, 12, 857, 108);
        Accent(coach, 1, 10, 4, 49);
        Bold(coach, HarborSequence.Speaker, 15, 5, 410, 20, 13).Modulate = Gold;
        lessonPhase = Bold(coach, "", 15, 28, 195, 29, 20);
        lesson = LabelAt(coach, "", 213, 35, 390, 26, 15);
        lesson.Modulate = Dim;
        lessonHint = Paragraph(coach, 15, 63, 713, 43, 14);
        AddButton(coach, "定位本步", 746, 25, 98, 39, LocateLesson, 12);
        receipt = Strip(ui, "#101b20ed", "#637c6c");
        Position(receipt, 16, 126, 660, 37);
        LabelAt(receipt, "许岑  /  第一车矿已入账，滩头基地可以自行整备部队。", 14, 8, 631, 24, 15);

        messagePanel = Strip(ui, "#1c2d30f5", "#7c8a76");
        var messageAccent = Accent(messagePanel, 1, 10, 4, 43);
        messageAccent.Name = "Accent";
        messageSpeaker = Bold(messagePanel, HarborSequence.Speaker, 15, 5, 410, 20, 13);
        messageSpeaker.Modulate = Gold;
        resultTitle = Bold(messagePanel, "行动成功", 19, 8, 137, 43, 30);
        resultMission = LabelAt(messagePanel, MissionBriefing.Prologue.Title, 160, 20, 325, 29, 16);
        resultMission.Modulate = Gold;
        resultRule = Accent(messagePanel, 20, 57, 516, 1, new Color("#5d7265"));
        message = Paragraph(messagePanel, 20, 10, 1100, 80, 20);
        takeCommand = AddButton(messagePanel, "接管先遣队", 0, 0, 188, 39, AcceptMessage, 15);
        enterPrompt = Bold(takeCommand, "Enter", 134, 8, 49, 22, 12);
        enterPrompt.Modulate = Ink;
        nextChapter = AddButton(messagePanel, "下一关 · 河湾镇", 20, 150, 181, 35,
            () => { if (activeChapter.Next is { } next) LaunchChapter(next); });
        SetLit(nextChapter, true);
        archiveResult = AddButton(messagePanel, "战役档案", 365, 150, 171, 35, OpenArchive);
        riverbendHandoff = AddButton(ui, "接管先遣队", 1232, 740, 204, 46, TakeCommand);
        SetLit(riverbendHandoff, true);
        Bold(riverbendHandoff, "Enter", 143, 10, 55, 25, 13).Modulate = Ink;

        fieldRadio = Strip(ui, "#202c32f5", "#ac925f");
        Position(fieldRadio, 16, 12, 880, 88);
        Accent(fieldRadio, 1, 10, 4, 68);
        fieldRadioSpeaker = Bold(fieldRadio, ChapterSequence.Speaker, 15, 5, 420, 20, 13);
        fieldRadioSpeaker.Modulate = Gold;
        fieldRadioText = Paragraph(fieldRadio, 15, 32, 850, 50, 18);

        basinObjective = Strip(ui, "#202c32f5", "#ac925f");
        Position(basinObjective, 16, 114, 245, 33);
        basinObjective.MouseFilter = Control.MouseFilterEnum.Ignore;
        basinRemaining = LabelAt(basinObjective, "", 13, 4, 219, 26, 15);
        basinRemaining.Modulate = Gold;

        MakePointer();
        MakeCommandBar();
        MakePausePanels();
        MakeRoadOrderPanel();
        MakeBasinDefeatFeed();
    }

    private void MakeFront()
    {
        var front = new TextureRect { Texture = view.Art.Static("harbor.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Stop, Name = "Front" };
        ui.AddChild(front);
        Position(front, 0, 0, 1472, 819);
        var menuShade = FrontShade(front, 172, 0);
        var directoryShade = FrontShade(front, 191, 143);
        directoryShade.Name = "DirectoryShade";
        menuShade.Name = "MenuShade";
        var header = SteelPanel(front, 0, 0, 1472, 55);
        Accent(header, 74, 8, 4, 37);
        AddEmblem(header);
        Bold(header, "灰港战区司令部", 96, 11, 189, 30, 20);
        Bold(header, "GREYHAVEN   /   OPERATIONS COMMAND", 285, 17, 500, 24, 12).Modulate = Dim;
        Bold(header, "●  单人行动", 1329, 13, 138, 24, 15).Modulate = Teal;
        var footer = SteelPanel(front, 0, 774, 1472, 45);
        Bold(footer, "GH   /   港区联合行动群", 48, 10, 410, 25, 13).Modulate = Dim;
        Bold(footer, "重连灰港", 1135, 10, 290, 25, 13).Modulate = Dim;
        startPanel = Page(front);
        Accent(startPanel, 61, 203, 49, 4);
        Bold(startPanel, "GH—01     /     GREYHAVEN FRONT", 62, 215, 555, 32, 18).Modulate = Gold;
        Bold(startPanel, "断线前沿", 53, 240, 590, 110, 89);
        Bold(startPanel, "灰 港 登 陆", 64, 360, 396, 44, 29);
        Accent(startPanel, 64, 428, 398, 1, Steel);
        LabelAt(startPanel, "建立滩头基地，消灭港区的敌方部队与建筑。", 64, 448, 620, 41, 19);
        var start = AddButton(startPanel, "开始游戏", 64, 521, 335, 60, () => OpenFront(FrontPage.Missions), 23);
        SetLit(start, true);
        Bold(start, "›", 301, 0, 28, 45, 30).Modulate = Ink;
        AddButton(startPanel, "规则介绍", 64, 598, 335, 44, () => OpenFront(FrontPage.Rules));
        archiveEntry = AddButton(startPanel, "战役档案", 64, 653, 335, 44, OpenArchive);
        AddButton(startPanel, "退出游戏", 64, 712, 190, 40, () => GetTree().Quit());

        missionPanel = Page(front);
        Bold(missionPanel, "行动选择", 58, 82, 360, 62, 46);
        Bold(missionPanel, "重连灰港  /  行动目录", 61, 150, 418, 25, 14).Modulate = Gold;
        chapterButtons = new Button[CampaignChapter.Playable.Length];
        for (int i = 0; i < chapterButtons.Length; i++)
        {
            var chapter = CampaignChapter.Playable[i];
            chapterButtons[i] = AddButton(missionPanel, $"0{i + 1}    {chapter.Brief.Title}", 64, 214 + i * 81, 373, 64,
                () => ChooseChapter(chapter), 23);
        }
        chosenLocation = LabelAt(missionPanel, "", 84, 540, 340, 29, 15);
        chosenLocation.Modulate = Dim;
        AddButton(missionPanel, "查看地图", 84, 574, 330, 46, OpenMapViewer);
        AddButton(missionPanel, "‹  返回主界面", 64, 674, 180, 45, () => OpenFront(FrontPage.Menu));
        var brief = SteelPanel(missionPanel, 492, 91, 908, 628);
        Accent(brief, 0, 0, 6, 83);
        briefCode = Bold(brief, "", 35, 20, 690, 26, 14);
        briefCode.Modulate = Gold;
        briefTitle = Bold(brief, "", 32, 45, 575, 56, 38);
        briefMap = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore, Name = "MapPreview" };
        brief.AddChild(briefMap);
        Position(briefMap, 36, 128, 414, 245);
        Accent(brief, 36, 128, 414, 245, new Color(Ink, 75 / 255f));
        mapLocation = Bold(brief, "", 48, 342, 385, 23, 13);
        Bold(brief, "行动简报", 478, 125, 386, 36, 21);
        briefStory = Paragraph(brief, 478, 173, 386, 195, 17);
        Accent(brief, 36, 398, 828, 1, new Color("#506165"));
        LabelAt(brief, "任务目标", 36, 419, 170, 26, 15).Modulate = Gold;
        briefOrders = Paragraph(brief, 36, 449, 830, 34, 18);
        LabelAt(brief, "胜利条件", 36, 488, 82, 25, 14).Modulate = Dim;
        briefVictory = Paragraph(brief, 121, 486, 745, 34, 16);
        launchChapterButton = AddButton(brief, "开始序章", 586, 529, 278, 62, () => LaunchChapter(chosenChapter), 23);
        var launch = launchChapterButton;
        SetLit(launch, true);
        Bold(launch, "›", 242, 0, 30, 45, 30).Modulate = Ink;

        MakeManual(front);
        MakeArchive(front);
        mapViewer = SteelPanel(front, 165, 84, 1142, 650);
        mapTitle = Bold(mapViewer, "", 25, 12, 580, 47, 30);
        expandedMap = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        mapViewer.AddChild(expandedMap);
        Position(expandedMap, 28, 74, 1086, 505);
        AddButton(mapViewer, "返回选关", 28, 591, 185, 38, CloseMapViewer);
    }

    private Button launchChapterButton;

    private void ChooseChapter(CampaignChapter chapter)
    {
        chosenChapter = chapter;
        var brief = chapter.Brief;
        chosenLocation.Text = mapLocation.Text = brief.Location;
        briefTitle.Text = brief.Title;
        briefCode.Text = brief.Code;
        ColorParagraph(briefStory, brief.Story);
        ColorParagraph(briefOrders, brief.Orders);
        ColorParagraph(briefVictory, brief.Victory);
        launchChapterButton.Text = chapter == CampaignChapter.Harbor ? "开始序章" : "进入" + brief.Title;
        if (!previews.TryGetValue(chapter, out var preview))
        {
            var map = chapter == CampaignChapter.Harbor ? RiverbendMap.ChapterOne() :
                chapter == CampaignChapter.Basin ? RiverbendMap.Basin() : new RiverbendMap();
            previews.Add(chapter, preview = RadarView.Preview(map));
        }
        briefMap.Texture = preview;
        for (int i = 0; i < chapterButtons.Length; i++) SetLit(chapterButtons[i], chapter == CampaignChapter.Playable[i]);
    }

    private void LaunchChapter(CampaignChapter chapter)
    {
        chosenChapter = chapter;
        viewingMap = false;
        ResetChapter(true, chapter);
    }

    private void OpenMapViewer()
    {
        viewingMap = true;
        expandedMap.Texture = briefMap.Texture;
        mapTitle.Text = chosenChapter.Brief.Title;
        UpdateInterface();
    }

    private void CloseMapViewer()
    {
        viewingMap = false;
        UpdateInterface();
    }

    private static Control Page(Control parent)
    {
        var page = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(page);
        Position(page, 0, 0, 1472, 819);
        return page;
    }

    private void OpenFront(FrontPage page)
    {
        frontPage = page;
        viewingMap = false;
        if (page == FrontPage.Missions) ChooseChapter(chosenChapter);
        UpdateInterface();
    }

    private void ReturnToMissions()
    {
        ResetChapter(false);
        OpenFront(FrontPage.Missions);
    }

    private Control SteelPanel(Control parent, float x, float y, float w, float h)
    {
        var node = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        node.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        parent.AddChild(node);
        Position(node, x, y, w, h);
        var metal = new TextureRect { Texture = view.Art.Static("plate.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Tile, MouseFilter = Control.MouseFilterEnum.Ignore };
        node.AddChild(metal);
        metal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = Gradient(node, new Color(Ink, 80 / 255f), new Color(Ink, 170 / 255f), vertical: true);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var highlight = Accent(node, 1, 1, w - 2, 1, new Color("#697579"));
        highlight.AnchorRight = 1;
        highlight.OffsetRight = -1;
        var baseEdge = Accent(node, 0, h - 3, w, 2, new Color("#070e12"));
        baseEdge.AnchorRight = baseEdge.AnchorTop = baseEdge.AnchorBottom = 1;
        baseEdge.OffsetRight = 0;
        baseEdge.OffsetTop = -3;
        baseEdge.OffsetBottom = -1;
        return node;
    }

    private static void Position(Control control, float x, float y, float w, float h)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(w, h);
    }

    private static ColorRect Accent(Control parent, float x, float y, float w, float h, Color? color = null)
    {
        var line = new ColorRect { Color = color ?? Gold, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(line);
        Position(line, x, y, w, h);
        return line;
    }

    private Panel Strip(Control parent, string fill, string edge)
    {
        var result = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        result.AddThemeStyleboxOverride("panel", skin.Plate(fill, edge));
        parent.AddChild(result);
        return result;
    }

    private Label Bold(Control parent, string text, float x, float y, float w, float h, int font)
    {
        var label = LabelAt(parent, text, x, y, w, h, font);
        label.AddThemeFontOverride("font", uiBold);
        return label;
    }

    private static Label LabelAt(Control parent, string text, float x, float y, float w, float h, int font)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", font);
        parent.AddChild(label);
        Position(label, x, y, w, h);
        return label;
    }

    private static RichTextLabel Paragraph(Control parent, float x, float y, float w, float h, int font)
    {
        var label = new RichTextLabel { BbcodeEnabled = true, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Arbitrary, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("normal_font_size", font);
        label.AddThemeColorOverride("default_color", Pale);
        parent.AddChild(label);
        Position(label, x, y, w, h);
        return label;
    }

    private static Color FactionInk(int side, bool onPaper = false) => onPaper
        ? BattleArt.Faction(side).Lerp(new Color("#302c24"), .34f)
        : BattleArt.Faction(side).Lerp(Pale, .40f);

    private static string FactionText(string text, bool onPaper = false) =>
        FactionNames.Replace(text, match =>
        {
            int side = match.Groups["joint"].Success ? 0 : match.Groups["north"].Success ? 1
                : match.Groups["mine"].Success ? 2 : match.Groups["rail"].Success ? 3
                : match.Groups["south"].Success ? 4 : 5;
            return $"[color=#{FactionInk(side, onPaper).ToHtml(false)}]{match.Value}[/color]";
        });

    private static void ColorParagraph(RichTextLabel label, string text, bool onPaper = false)
    {
        var styled = FactionText(text, onPaper);
        if (label.Text != styled) label.Text = styled;
    }

    private static Button AddButton(Control parent, string text, float x, float y, float w, float h,
        Action action, int font = 15, bool compact = false, int inset = 11)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar };
        if ((!compact && w >= 48) || inset != 11)
            button.Alignment = HorizontalAlignment.Left;
        if (inset != 11)
        {
            button.Text = "";
            var label = LabelAt(button, text, inset, 0, w - inset - 3, h - 1, font);
            label.AddThemeFontOverride("font", parent.GetThemeFont("font", "Button"));
        }
        button.AddThemeFontSizeOverride("font_size", font);
        button.Pressed += action;
        parent.AddChild(button);
        Position(button, x, y, w, h);
        return button;
    }

    private void SetLit(Button button, bool lit)
    {
        if (button.HasThemeStyleboxOverride("normal") == lit) return;
        if (lit)
        {
            button.AddThemeStyleboxOverride("normal", skin.Plate("#ecb54e", "#ecb54e", 32, true));
            button.AddThemeStyleboxOverride("hover", skin.Plate("#ffd077", "#ecb54e", 32, true));
            button.AddThemeStyleboxOverride("pressed", skin.Plate("#d59d3d", "#ecb54e", 32, true));
            button.AddThemeColorOverride("font_color", Ink);
            button.AddThemeColorOverride("font_hover_color", Ink);
            button.AddThemeColorOverride("font_pressed_color", Ink);
        }
        else
        {
            button.RemoveThemeStyleboxOverride("normal");
            button.RemoveThemeStyleboxOverride("hover");
            button.RemoveThemeStyleboxOverride("pressed");
            button.RemoveThemeColorOverride("font_color");
            button.RemoveThemeColorOverride("font_hover_color");
            button.RemoveThemeColorOverride("font_pressed_color");
        }
    }

    private void Layout()
    {
        if (world == null) return;
        var size = GetViewport().GetVisibleRect().Size;
        view.Area = new Rect2(0, 0, view.Cinematic ? size.X : size.X - 168, size.Y);
        panel.Position = new Vector2(size.X - 168, 0);
        view.Center(view.CameraPosition);
        view.QueueRedraw();
    }

    private void UpdateInterface()
    {
        UpdateRoadOrderPanel();
        UpdateCommands();
        bool battle = started && !SceneRunning && !Finished;
        var front = ui.GetNode<Control>("Front");
        front.Visible = !started || ReadingArchive;
        front.GetNode<Control>("DirectoryShade").Visible = frontPage == FrontPage.Missions;
        front.GetNode<Control>("MenuShade").Visible = frontPage != FrontPage.Missions;
        startPanel.Visible = frontPage == FrontPage.Menu;
        missionPanel.Visible = frontPage == FrontPage.Missions && !viewingMap;
        mapViewer.Visible = viewingMap;
        rulesPanel.Visible = frontPage == FrontPage.Rules;
        archivePanel.Visible = ReadingArchive;
        top.Visible = started && !ReadingArchive && (SceneRunning || Finished && !world.Lost);
        chapterHeading.Text = activeChapter.SceneTitle;
        panel.Visible = battle;
        RefreshPausePanels();

        var basinSequence = sequence as BasinSequence;
        bool basinClosing = basinSequence?.Departing == true;
        messagePanel.Visible = started && !ReadingArchive &&
            (Finished || sequence.Departing && !basinClosing && sequence.Caption.Length > 0 || sequence.Opening && world.FirstChapter);
        riverbendHandoff.Visible = started && !paused && !world.FirstChapter && sequence.CanTakeCommand;
        takeCommand.Visible = !paused && (sequence.CanTakeCommand && world.FirstChapter || Finished);
        nextChapter.Visible = Finished && world.Won && activeChapter.Next != null;
        if (nextChapter.Visible) nextChapter.Text = "下一关 · " + activeChapter.Next.Brief.Title;
        archiveResult.Visible = Finished && world.Won;
        takeCommand.Text = Finished ? "返回选关" : "接管先遣队";
        SetLit(takeCommand, !Finished);
        UpdateCaption();

        var basin = world.BasinOperation;
        bool basinRadio = basinSequence != null && (sequence.Opening || basinClosing);
        string guidance = basinRadio ? sequence.Caption : sequence.FieldRadio;
        fieldRadio.Visible = started && !ReadingArchive && !string.IsNullOrEmpty(guidance) && !WaitingForDeparture &&
            (battle || basinRadio && !paused);
        fieldRadio.Position = new Vector2(16, basinRadio ? 62 : 12);
        basinObjective.Visible = battle && basin != null && !WaitingForDeparture;
        if (basinObjective.Visible)
        {
            int remaining = Enumerable.Range(1, world.FactionCount - 1).Count(side => world.Remaining(side) > 0);
            basinRemaining.Text = $"仍在抵抗的武装  {remaining} / 5";
            basinObjective.Position = new Vector2(16, fieldRadio.Visible ? 114 : 12);
        }
        if (fieldRadio.Visible)
        {
            fieldRadioSpeaker.Text = basinClosing ? basinSequence.ClosingSpeaker
                : basinRadio ? basinSequence.OpeningSpeaker : ChapterSequence.Speaker;
            ColorParagraph(fieldRadioText, guidance);
        }
        UpdateBasinDefeatFeed();
        coach.Visible = battle && world.Tutorial && !WaitingForDeparture;
        int stage = world.Lesson;
        lessonPhase.Text = stage < 2 ? "建立滩头基地" : stage < 9 ? "让补给运转起来" : "清除码头守军";
        lesson.Text = world.LessonTitle;
        if (coach.Visible)
        {
            string hint = world.Coach() ?? LessonHint();
            if (stage == 0 && !world.Landing)
                hint = "指挥官，我是许岑，原定的训练由我负责。我们先把滩头站稳。\n" + hint;
            ColorParagraph(lessonHint, hint);
        }
        receipt.Visible = coach.Visible && world.Live(0).Any(p => p.LastReceipt > 0 &&
            world.OreReceipts[0] == p.LastReceipt && world.Time - p.ReceiptAt < 11);
        if (!battle) return;
        funds.Text = "$ " + world.Funds[0];
        int power = world.Power(0);
        bool online = power >= 0 && world.Live(0).Any(p => p.T.Key == "radar" || p.T.Key == "satellite");
        radar.Visible = online;
        radarOffline.Visible = radarNote.Visible = !online;
        radarNote.Text = world.Live(0).Any(p => p.T.Key == "radar") ? "恢复供电后启用" : "建造雷达站后启用";
        powerBar.Size = new Vector2(3, MathF.Min(120, 35 + MathF.Abs(power) * .2f));
        powerBar.Color = power < 0 ? new Color("#ee8b72") : Teal;
        if (online) radar.QueueRedraw();
        stats.Text = $"电力 {power:+#;-#;0}       {TimeSpan.FromSeconds(battleSeconds):mm\\:ss}\n{activeChapter.Brief.Title}          ×{multiplier}";
        var inspected = selected.Count == 1 ? selected[0] : null;
        bool showingOil = inspected?.T.IsOilDerrick == true;
        selectionText.Text = showingOil ? $"油田 +{BasinOperation.OilPayout} / {BasinOperation.OilPayoutSeconds} 秒"
            : inspected?.T.Key == "miner" ? world.MiningStatus(inspected)
            : world.Radio.Length > 15 ? world.Radio[..14] + "…" : world.Radio;
        selectionText.TooltipText = showingOil
            ? $"油田每 {BasinOperation.OilPayoutSeconds} 游戏秒入账 {BasinOperation.OilPayout} 军费。"
            : world.Radio;
        SetLit(repair, structureTool == StructureTool.Repair);
        SetLit(sell, structureTool == StructureTool.Sell);
        RefreshProduction();
    }

    private void UpdateCaption()
    {
        var screen = GetViewport().GetVisibleRect().Size;
        bool result = Finished;
        bool departing = sequence.Departing && !result;
        bool opening = sequence.Opening;
        int height = result ? 200 : departing ? 65 : sequence.Caption.Contains('\n') ? 72 : 56;
        float width = result ? 556 : departing ? world.FirstChapter ? 772 : 880 : screen.X - 60;
        float x = result ? 24 : departing ? 16 : 30;
        float textWidth = width - (takeCommand.Visible && !result ? 242 : departing ? 30 : 40);
        message.AddThemeFontSizeOverride("normal_font_size", result ? 16 : departing ? 18 : 20);
        ColorParagraph(message, result ? world.Lost ? sequence.Failure :
            string.Join("\n", sequence.Debrief) + $"\n用时 {TimeSpan.FromSeconds(battleSeconds):mm\\:ss}  /  存活部队 {world.Live(0).Count(p => !p.T.Building)}" :
            opening || departing ? sequence.Caption : "");
        if (departing)
        {
            message.Size = new Vector2(textWidth, message.Size.Y);
            height = Math.Max(height, 32 + message.GetContentHeight() + 10);
        }
        float y = departing ? 62 : screen.Y - height - (result ? 24 : 22);
        Position(messagePanel, x, y, width, height);
        var plate = result ? skin.Plate("#202c32f6", "#a2b398") : departing
            ? skin.Plate("#202c32f5", "#ac925f") : skin.Plate("#1c2d30f5", "#7c8a76");
        if (messagePanel.GetThemeStylebox("panel") != plate) messagePanel.AddThemeStyleboxOverride("panel", plate);
        Position(messagePanel.GetNode<Control>("Accent"), 1, result ? 12 : 10, 4, result ? 47 : height - 20);
        messageSpeaker.Visible = departing;
        messageSpeaker.Text = ChapterSequence.Speaker;
        resultTitle.Visible = resultMission.Visible = resultRule.Visible = result;
        resultTitle.Text = world.Lost ? "行动终止" : "行动成功";
        resultMission.Text = activeChapter.Brief.Title;
        Position(message, departing ? 15 : 20, result ? 67 : departing ? 32 : height == 72 ? 10 : 15,
            textWidth, result ? 78 : departing ? height - 32 : height - 10);
        Position(takeCommand, result ? nextChapter.Visible ? 212 : 20 : width - 211,
            result ? 150 : (height - 39) * .5f, result ? 142 : 188, result ? 35 : 39);
        if (result) Position(archiveResult, nextChapter.Visible ? 365 : 174, 150, 171, 35);
        enterPrompt.Visible = opening;
    }

    private bool IsBuildingLesson => Campaign.Info(world.LessonKey) is { Building: true, Key: not "hq" };

    private string LessonHint()
    {
        string key = world.LessonKey;
        var type = Campaign.Info(key);
        var own = world.Live(0);
        float speed = MatchTempo.DefaultRate * multiplier;
        if (key == "hq")
            return world.Landing ? "指挥部正在展开，完工后进入下一步。" : selected.Any(p => p.T.Key == "mcv")
                ? "再次点工程车或按 D 展开。" : "先选中工程车，也可以点「定位本步」。";
        if (IsBuildingLesson)
        {
            var job = world.Construction(type);
            if (job != null && !job.Ready) return type.Name + "：" + world.ConstructionState(job, 0, speed) + "。右键图标暂停／再右键取消，左键继续。";
            var site = world.Units.FirstOrDefault(p => p.Alive && p.Side == 0 && p.T == type);
            if (site != null) return type.Name + " 正在施工，约 " + (int)Math.Ceiling((1 - site.Ready) * Campaign.AssemblySeconds / speed) + " 秒完成。";
            return placing == type ? "左键点击可建造的位置。红色格子表示该格不能建造。" :
                "点「建筑」中的「" + type.Name + "」生产，等待就绪后再点图标放置。点「定位本步」可查看推荐位置。";
        }
        if (key is "squad" or "ifv")
        {
            var source = own.FirstOrDefault(p => p.Queue.Contains(type));
            return source != null ? source.T.Name + " #" + source.Id + " 已接单。点「总览」查看进度，等部队出列。" :
                "按 E 选步兵、R 选车辆，再点「" + type.Name + "」。军费随进度扣除，缺钱自动等待。";
        }
        if (key is "ore" or "receipt")
        {
            var truck = own.FirstOrDefault(p => p.T.Key == "miner");
            if (truck == null) return "精炼厂完工会派出首车，请留出厂门。矿车损失后可在车辆工厂补造。";
            if (!truck.Mining) return "选中矿车，左键点已发现的金色矿石，恢复自动采矿。";
            if (key == "ore") return truck.Patch == null ? "矿车会向未探索地带找矿。也可派步兵先探路，再选矿车左键点金色矿石。" :
                "矿车已找到金色矿点，正在驶近。点「定位本步」查看矿点和车上的载量。";
            return truck.Returning ? "矿车正在返厂卸货。留出厂门，留意精炼厂上方显示的入账金额。" :
                "采集进度 " + (int)truck.Cargo + " / " + Campaign.LoadValue + "。满载后自动返厂，也可选车点己方精炼厂提前卸矿。";
        }
        return "点「定位本步」选中战斗部队，按住 Ctrl+Shift 沿港务中心到货运闸的公路搜索。";
    }
}
