using Godot;
using Greyhaven;

public partial class HarborGame
{
    private Control[] manualPages;
    private Button[] manualTabs;
    private Label manualTitle;
    private int manualIndex;
    private static readonly string[] ManualTitles = { "战场规则", "单位图鉴", "经典指令", "晋升与扩建" };

    private void MakeManual(Control front)
    {
        rulesPanel = Page(front);
        Bold(rulesPanel, "规则介绍", 58, 79, 420, 65, 44);
        Bold(rulesPanel, "FIELD MANUAL   /   GREYHAVEN", 61, 145, 420, 26, 14).Modulate = Gold;
        var book = SteelPanel(rulesPanel, 318, 83, 1082, 649);
        manualTitle = Bold(book, "", 29, 16, 900, 42, 28);
        manualTabs = new Button[ManualTitles.Length];
        manualPages = new Control[ManualTitles.Length];
        for (int i = 0; i < manualTabs.Length; i++)
        {
            int page = i;
            manualTabs[i] = AddButton(rulesPanel, $"0{i + 1}    {ManualTitles[i]}", 64, 206 + i * 61, 220, 48,
                () => ShowManualPage(page));
            manualPages[i] = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            book.AddChild(manualPages[i]);
            Position(manualPages[i], 0, 74, 1082, 561);
        }
        ManualRows(manualPages[0], new[]
        {
            ("基地与防线", "展开工程车建立指挥部。普通建筑最多隔 2 个空格，基础炮台最多隔 4 格。"),
            ("采矿与军费", $"矿车满载 {Campaign.LoadValue} 后自动返厂。矿格逐渐采空，矿石不会再生。"),
            ("生产与交战", "左键生产或续单，右键减单或暂停。车辆共用队列，兵营各自排产。"),
            ("消灭敌方", "肃清敌方战斗力量和生产能力，或迫使对方投降。"),
            ("雷达与镜头", "雷达通电后显示小地图。选中部队后左键可下令，右键可定位。"),
            ("占领与驻防", "中立房屋由工兵先占领。步枪兵可进入房屋或驻兵碉堡，按 D 撤出。")
        });
        var roster = new[]
        {
            ("squad", "部署沙袋阵地，射击距离增加。可驻房屋和碉堡。"),
            ("at", "平时用机枪，部署后使用反装甲导弹。"),
            ("engineer", "占领或维修建筑。乘坐步兵战车可维修车辆。"),
            ("ifv", "载员改变武器或支援能力，按 D 卸载。"),
            ("tank", "炮塔独立瞄准，行军中也可以开炮。"),
            ("arty", "适合从后方支援和压制地面。"),
            ("mcv", "按 D 展开指挥部，也可收起并转移。"),
            ("miner", "找矿、采集、返厂。也可手动指定矿点。"),
            ("sniper", "远程反步兵。可搭乘战车，无法驻防房屋。")
        };
        for (int i = 0; i < roster.Length; i++)
        {
            var type = Campaign.Info(roster[i].Item1);
            var card = Strip(manualPages[1], "#202c32", "#546469");
            Position(card, 26 + i % 2 * 520, 13 + i / 2 * 104, 500, 96);
            Thumbnail(card, 9, 10, 96, 72).Texture = view.Art.Portrait(type);
            Bold(card, type.Name, 118, 4, 269, 31, 20);
            Bold(card, "$ " + type.Price, 405, 8, 85, 26, 16).Modulate = Gold;
            LabelAt(card, $"生命 {type.Hp}    视野 {type.SightCells} 格", 119, 33, 350, 23, 13).Modulate = Teal;
            LabelAt(card, roster[i].Item2, 119, 61, 376, 25, 14).Modulate = Dim;
        }
        (string Keys, string Help)[] commands =
        {
            ("左键 ／ 右键", "选择、移动或点名攻击 ／ 取消或拖动地图"),
            ("Shift＋左键 ／ T", "增减选择 ／ 同类选择，连按选全图同类"),
            ("Ctrl＋数字 ／ 数字", "创建、召回编组，Alt＋数字定位"),
            ("Ctrl＋Shift＋左键", "攻击移动"),
            ("G ／ Ctrl＋Alt＋左键", "原地警戒 ／ 定点警戒或护卫"),
            ("Ctrl ／ Alt＋左键", "强制开火 ／ 强制移动"),
            ("S、X、D", "停止、散开、展开或装卸"),
            ("按住 Z → 设点 → 松开", "规划路径，点回已有路径点可巡逻"),
            ("H、空格", "定位基地、最近事件"),
            ("Q、W、E、R", "建筑、防御、步兵、车辆"),
            ("P、N、M", "全选战斗单位、切换所选单位"),
            ("K、L、Tab", "维修、出售、阵营信息")
        };
        for (int i = 0; i < commands.Length; i++)
        {
            int y = 5 + i * 44;
            Accent(manualPages[2], 30, y, 1016, 1, new Color("#506165"));
            Bold(manualPages[2], commands[i].Keys, 34, y + 7, 340, 27, 14).Modulate = Gold;
            LabelAt(manualPages[2], commands[i].Help, 380, y + 7, 655, 27, 14);
        }
        ManualRows(manualPages[3], new[]
        {
            ("扩建距离", "从建筑地基边缘计距。炮台、民居和任务站无法用来接力扩建。"),
            ("晋升门槛", "击毁价值达自身价格的 3 倍成为老兵，达到 6 倍成为精英。"),
            ("老兵", "火力与移动速度提高，受到的伤害降低。部署步兵和部分武器有额外加成。"),
            ("精英", "保留老兵能力，部分车辆也会提升射速或获得连射。"),
            ("恢复", "精英单位逐步恢复生命。采矿车自带恢复能力。"),
            ("载员与驻防", "战车击杀的经验给战车，驻防射击的经验给开火的步兵。")
        });
        AddButton(rulesPanel, "‹  返回主界面", 64, 678, 220, 44, () => OpenFront(FrontPage.Menu));
        ShowManualPage(0);
    }

    private void ManualRows(Control page, (string Title, string Description)[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            int y = 10 + i * 90;
            Accent(page, 30, y, 1016, 1, new Color("#506165"));
            Bold(page, rows[i].Title, 34, y + 12, 980, 29, 20).Modulate = Gold;
            LabelAt(page, rows[i].Description, 34, y + 42, 980, 30, 17);
        }
    }

    private void ShowManualPage(int page)
    {
        manualIndex = (page + ManualTitles.Length) % ManualTitles.Length;
        manualTitle.Text = ManualTitles[manualIndex];
        for (int i = 0; i < manualPages.Length; i++)
        {
            manualPages[i].Visible = i == manualIndex;
            SetLit(manualTabs[i], i == manualIndex);
        }
    }
}
