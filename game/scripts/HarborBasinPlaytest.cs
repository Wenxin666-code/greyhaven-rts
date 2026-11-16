using System.Linq;
using Godot;
using Greyhaven;

public partial class HarborGame
{
    private Control basinTestPanel;
    private Button basinTestEntry, basinTestReinforce, basinTestRivalry;
    private readonly Button[] basinTestDefeat = new Button[Campaign.MaxFactions - 1];
    private bool basinTestOpen;
    private bool CanOpenBasinTest => started && world.BasinOperation != null && !SceneRunning &&
        !Finished && !WaitingForDeparture && !ReadingRoadOrder && !ReadingArchive;

    private void MakeBasinTestPanel()
    {
        basinTestEntry = AddButton(battlePause, "关卡测试  F9", 702, 381, 145, 49, () => SetBasinTest(true));
        basinTestPanel = Strip(pauseCurtain, "#202e30", "#9f9271");
        Position(basinTestPanel, 376, 186, 720, 447);
        Bold(basinTestPanel, "第三关测试", 36, 25, 540, 48, 31);
        Accent(basinTestPanel, 36, 80, 648, 1, new Color("#65796c"));
        basinTestReinforce = AddButton(basinTestPanel, $"增援 {Campaign.BasinTestReinforcementCount} 辆自行火炮", 36, 102, 310, 46,
            SendBasinReinforcements, 17);
        SetLit(basinTestReinforce, true);
        basinTestRivalry = AddButton(basinTestPanel, "模拟白石击败北线", 374, 102, 310, 46,
            () => TestBasinDefeat(1, 2), 17);

        Bold(basinTestPanel, "由我方直接击败", 36, 175, 265, 26, 17).Modulate = Gold;
        for (int i = 0; i < basinTestDefeat.Length; i++)
        {
            int side = i + 1;
            basinTestDefeat[i] = AddButton(basinTestPanel, "", 36 + i % 2 * 338,
                211 + i / 2 * 53, 310, 42, () => TestBasinDefeat(side, 0), 16);
            Accent(basinTestPanel, 41 + i % 2 * 338, 222 + i / 2 * 53, 3, 20, BattleArt.Faction(side));
        }
        AddButton(basinTestPanel, "返回战场  Esc", 503, 381, 181, 40, () => SetBasinTest(false), 16);
    }

    private void SetBasinTest(bool open)
    {
        if (open && !CanOpenBasinTest) return;
        basinTestOpen = open;
        if (open) showObjectives = false;
        SetPaused(open);
    }

    private void UpdateBasinTest()
    {
        basinTestEntry.Visible = CanOpenBasinTest;
        basinTestEntry.Text = BoundAction(Key.F9) == Key.None ? "关卡测试  F9" : "关卡测试";
        basinTestPanel.Visible = paused && basinTestOpen;
        if (!basinTestPanel.Visible) return;
        basinTestRivalry.Disabled = world.Remaining(1) == 0 || !world.Live(2).Any();
        for (int i = 0; i < basinTestDefeat.Length; i++)
        {
            int side = i + 1;
            bool defeated = world.Remaining(side) == 0;
            basinTestDefeat[i].Text = world.StoryFactionName(side) + (defeated ? " · 已出局" : "");
            basinTestDefeat[i].Disabled = defeated;
        }
    }

    private void SendBasinReinforcements()
    {
        var reinforcements = world.SendBasinTestReinforcements();
        SetBasinTest(false);
        if (reinforcements.Count == 0)
        {
            world.Radio = "我方附近没有可用的增援位置。";
            return;
        }
        CancelMapMode();
        selected.Clear();
        selected.AddRange(reinforcements);
        view.Center(reinforcements[reinforcements.Count / 2].P);
        world.Radio = $"{reinforcements.Count} 辆自行火炮已到位。";
        view.QueueRedraw();
    }

    private void TestBasinDefeat(int defeated, int victor)
    {
        if (!world.DefeatBasinTestFaction(defeated, victor)) return;
        SetBasinTest(false);
        Simulate((float)MatchTempo.Step);
        if (victor > 0) world.Radio = $"{world.StoryFactionName(victor)}已击败{world.StoryFactionName(defeated)}。";
        UpdateInterface();
        view.QueueRedraw();
    }
}
