using Godot;
using Greyhaven;

public partial class HarborGame
{
    private Control scenePause, battlePause, diplomacyPanel;
    private ColorRect pauseCurtain;
    private Label battlePauseTitle, pauseCode, pauseMission;
    private RichTextLabel pauseStory, pauseOrders, pauseVictory;
    private Button[] rateButtons;
    private bool diplomacyOpen;
    private readonly Label[] factionLabels = new Label[3], factionStates = new Label[3], factionOrders = new Label[3];
    private readonly ColorRect[] factionColors = new ColorRect[3];

    private void MakePausePanels()
    {
        pauseCurtain = Accent(ui, 0, 0, 1472, 819, new Color(Ink, 177 / 255f));
        pauseCurtain.MouseFilter = Control.MouseFilterEnum.Stop;
        scenePause = Strip(pauseCurtain, "#1f302f", "#a2b398");
        Position(scenePause, 491, 301.5f, 490, 216);
        Bold(scenePause, "演出已暂停", 30, 20, 429, 50, 35);
        Accent(scenePause, 32, 85, 426, 1, new Color("#5d7265"));
        SetLit(AddButton(scenePause, "继续演出", 32, 112, 200, 45, () => SetPaused(false)), true);
        AddButton(scenePause, "返回选关", 258, 112, 200, 45, ReturnToMissions);
        LabelAt(scenePause, "Enter", 37, 165, 92, 23, 13).Modulate = Dim;
        LabelAt(scenePause, "Esc", 263, 165, 86, 23, 13).Modulate = Dim;

        battlePause = Strip(pauseCurtain, "#1f302f", "#a2b398");
        Position(battlePause, 285, 172, 900, 550);
        Accent(battlePause, 1, 33, 6, 112);
        pauseCode = Bold(battlePause, "", 39, 27, 730, 24, 14);
        pauseCode.Modulate = Gold;
        battlePauseTitle = Bold(battlePause, "指挥已暂停", 38, 62, 580, 77, 49);
        pauseMission = Bold(battlePause, "", 656, 96, 215, 33, 21);
        pauseMission.Modulate = Gold;
        Accent(battlePause, 44, 153, 811, 1, new Color("#5d7265"));
        pauseStory = Paragraph(battlePause, 43, 175, 814, 88, 17);
        Bold(battlePause, "任务目标", 43, 270, 395, 24, 14).Modulate = Gold;
        pauseOrders = Paragraph(battlePause, 43, 301, 814, 30, 17);
        Bold(battlePause, "胜利条件", 43, 337, 85, 23, 13).Modulate = Dim;
        pauseVictory = Paragraph(battlePause, 126, 339, 731, 23, 16);
        SetLit(AddButton(battlePause, "返回战场", 43, 381, 241, 49, () => SetPaused(false)), true);
        AddButton(battlePause, "重开本关", 309, 381, 182, 49, () => LaunchChapter(activeChapter));
        AddButton(battlePause, "返回选关", 511, 381, 170, 49, ReturnToMissions);
        AddButton(battlePause, "键盘与鼠标", 43, 443, 391, 36, OpenKeyboard);
        AddButton(battlePause, "生产队列", 456, 443, 391, 36, () => { SetPaused(false); OpenQueue(); });
        Bold(battlePause, "对局速度", 43, 496, 99, 30, 15);
        rateButtons = new Button[4];
        for (int i = 0; i < rateButtons.Length; i++)
        {
            int rate = i + 1;
            rateButtons[i] = AddButton(battlePause, rate + "×", 145 + i * 179, 492, 164, 34, () => SetSpeed(rate));
        }
        MakeKeyboard();
        diplomacyPanel = Strip(pauseCurtain, "#202c32", "#ecb54e");
        Position(diplomacyPanel, 280, 134, 910, 535);
        Bold(diplomacyPanel, "阵营信息", 32, 16, 410, 54, 30);
        LabelAt(diplomacyPanel, "本关阵营关系固定，各敌方互相敌对。", 34, 66, 840, 31, 16).Modulate = Dim;
        for (int i = 0; i < factionLabels.Length; i++)
        {
            int y = 116 + i * 50;
            factionColors[i] = Accent(diplomacyPanel, 35, y + 8, 19, 19, BattleArt.Faction(i));
            factionLabels[i] = Bold(diplomacyPanel, "", 72, y, 245, 35, 19);
            factionStates[i] = LabelAt(diplomacyPanel, "", 327, y, 175, 35, 16);
            factionOrders[i] = LabelAt(diplomacyPanel, "", 507, y, 375, 35, 14);
            factionOrders[i].ClipText = true;
        }
        AddButton(diplomacyPanel, "返回战场", 655, 467, 205, 39, () => SetDiplomacy(false));

    }

    private void RefreshPausePanels()
    {
        pauseCurtain.Visible = started && paused;
        if (!pauseCurtain.Visible) return;
        bool cinematic = SceneRunning || Finished;
        pauseCurtain.Color = new Color(Ink, (cinematic ? 171 : 177) / 255f);
        scenePause.Visible = cinematic;
        diplomacyPanel.Visible = !cinematic && diplomacyOpen;
        keyboardPanel.Visible = !cinematic && keyboardOpen && !diplomacyOpen;
        battlePause.Visible = !cinematic && !keyboardOpen && !diplomacyOpen;
        if (cinematic || keyboardOpen || diplomacyOpen) return;
        var mission = activeChapter.Brief;
        pauseCode.Text = mission.Code;
        pauseMission.Text = mission.Title;
        ColorParagraph(pauseStory, mission.Story);
        ColorParagraph(pauseOrders, mission.Orders);
        ColorParagraph(pauseVictory, mission.Victory);
        battlePauseTitle.Text = showObjectives ? "任务简报" : "指挥已暂停";
        for (int i = 0; i < rateButtons.Length; i++) SetLit(rateButtons[i], multiplier == i + 1);
    }

    private void SetDiplomacy(bool open)
    {
        diplomacyOpen = open;
        if (open)
        {
            for (int side = 0; side < factionLabels.Length; side++)
            {
                bool present = side < world.FactionCount;
                factionLabels[side].Visible = factionStates[side].Visible = factionOrders[side].Visible = factionColors[side].Visible = present;
                if (!present) continue;
                factionLabels[side].Text = world.StoryFactionName(side);
                factionStates[side].Text = side == 0 ? "我方" : world.HasSurrendered(side) ? "已投降" :
                    world.Remaining(side) == 0 ? "已歼灭" : "互相敌对";
                factionOrders[side].Text = world.StoryFactionTask(side);
            }
        }
        SetPaused(open);
    }
}
