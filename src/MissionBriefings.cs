using System.Drawing;

namespace Greyhaven {
    sealed class MissionBriefing {
        public readonly string Code, Title, Location, Story, Orders, Victory;

        MissionBriefing(string code, string title, string location, string story, string orders, string victory) {
            Code = code;
            Title = title;
            Location = location;
            Story = story;
            Orders = orders;
            Victory = victory;
        }

        public static readonly MissionBriefing Training = new MissionBriefing(
            "OPERATION 01   /   GREYHAVEN LANDING", "野外训练", "旧港滩头 / 先遣行动",
            "灰港的长途通信与港口调度在同一夜中断，沿岸驻军开始争抢补给。原定的野外训练紧急转为登陆行动。北线驻防军的封锁哨兵守在旧码头，不肯放行。你带工程分队建立补给基地，在许岑的引导下完成整备，再向码头推进。保住这个入口，才能查明内陆发生了什么。",
            "在旧码头建立补给基地，击败封锁分队，稳住登陆入口。",
            "消灭港区内全部敌方守军。");

        public static readonly MissionBriefing Riverbend = new MissionBriefing(
            "OPERATION 02   /   RIVERBEND", "河湾镇", "西南进入 / 内陆补给通道",
            "旧码头已由后续部队接管。河湾的两支武装却仍在争夺过境物资：北线驻防军准备以装甲部队封住北桥，白石矿务警卫队围绕南桥矿路经营设防。双方互不服从，也会彼此交战。你从西南立足，侦察渡口并击败两方，把工程分队送往内陆资源岛。",
            "从西南立足，击败河湾两支武装，打通通往工业带的道路。",
            "消灭两个敌方阵营的全部单位与建筑，或迫使其投降。");

        public static readonly MissionBriefing Island = new MissionBriefing(
            "OPERATION 03   /   ISLAND FORTRESS", "孤岛要塞", "西北资源岛 / 大陆反攻",
            "越过河湾后，你先一步抵达西北资源岛。大陆五路武装各争利益：北线驻防军扼守军路，白石警卫队保矿，铁轨卫队争通路，南岸防卫队据守城镇，独立旅袭扰薄弱矿路。它们各自采矿扩军、互不服从。守住唯一堤桥，逐步击败五方，建立内陆反攻支点。",
            "守住资源岛入口，击败大陆五支武装，建立内陆反攻支点。",
            "消灭五个敌方阵营的全部单位与建筑，或迫使其投降。");

        public static readonly MissionBriefing Relay = new MissionBriefing(
            "OPERATION 04   /   MISTRIDGE RELAY", "雾脊中继站", "西南山口 / 北岭通信枢纽",
            "资源岛的补给线终于稳定。工程官周砚发现：灰港断线前，北岭中继站仍在转发命令。它位于地图北部、我方出生地东北方，情报坐标已标注。你从西南山口展开基地，派工兵与护卫夺回枢纽。恢复通信过半时，统辖部会向仍在抵抗的守军拨付一笔增援经费。",
            "点击“定位中继站”查看目标；护送工兵夺控，累计恢复 6 分钟通信。",
            "恢复达到 100% 即获胜；敌方夺回时暂停，站毁则行动失败。");

        public static MissionBriefing For(Campaign campaign) {
            return campaign.RelayMission ? Relay : campaign.IslandMission ? Island : campaign.River != null ? Riverbend : Training;
        }
    }

    partial class CommandWindow {
        string[] StoryVictoryLines(string[] fallback) {
            if(world.RelayMission)return fallback;
            if(world.Tutorial)return new[]{"旧码头的封锁解除，后续保障部队正在接管。","许岑：工程车队停在河湾镇。我们先打通道路，再向内陆推进。",fallback[1]};
            if(world.IslandMission)return new[]{"矿业盆地已控制，灰港的外围补给支点建立。","周砚：几份封路令早于通信中断。原始记录在雾脊中继站。",fallback[1]};
            if(world.River!=null)return new[]{"河湾道路重新开放，工程分队可以继续前进。","许岑：抢先进入西北保障岛。大陆五路武装正在争夺矿业盆地。",fallback[1]};
            return fallback;
        }
        void BriefingParagraph(Graphics g, string text, RectangleF bounds, float size, Color color) {
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var brush = new SolidBrush(color)) {
                format.FormatFlags &= ~StringFormatFlags.NoClip;
                format.Trimming = StringTrimming.None;
                g.DrawString(text, F(size, false), brush, bounds, format);
            }
        }

        void DrawMissionSummary(Graphics g, MissionBriefing mission) {
            BriefingParagraph(g, mission.Story, new RectangleF(328, 347, 814, 88), 17, pale);
            Txt(g, "任务目标", 328, 447, 14, gold, true);
            BriefingParagraph(g, mission.Orders, new RectangleF(328, 473, 814, 29), 17, pale);
            Txt(g, "胜利条件", 328, 514, 13, dim, true);
            BriefingParagraph(g, mission.Victory, new RectangleF(411, 511, 731, 28), 16, pale);
        }
    }
}
