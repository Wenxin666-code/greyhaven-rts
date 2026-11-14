using System;
using System.Drawing;
#if !GODOT
using System.Drawing.Drawing2D;
#endif
using System.Linq;
using System.Text.RegularExpressions;

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

        public static readonly MissionBriefing Prologue = new MissionBriefing(
            "OPERATION 01   /   GREYHAVEN LANDING", "断线之夜", "旧港滩头 / 战役序章",
            "你是沿岸联合保障司令部的先遣指挥官。昨夜，北线驻防军封锁旧码头，拒绝执行司令部的放行令。原定训练改为登陆行动。夺回旧码头，让补给船恢复靠泊。",
            "在滩头展开工程车，建立补给基地，夺回旧码头。",
            "清除港区内全部封锁守军。");

        public static readonly MissionBriefing Training = new MissionBriefing(
            "OPERATION 01   /   GREYHAVEN LANDING", "野外训练", "旧港滩头 / 先遣行动",
            "灰港的长途通信与港口调度在同一夜中断，沿岸驻军开始争抢补给。原定的野外训练紧急转为登陆行动。北线驻防军的封锁哨兵守在旧码头，不肯放行。你带工程分队建立补给基地，在许岑的引导下完成整备，再向码头推进。保住这个入口，才能查明内陆发生了什么。",
            "在旧码头建立补给基地，击败封锁分队，稳住登陆入口。",
            "消灭港区内全部敌方守军。");

        public static readonly MissionBriefing Riverbend = new MissionBriefing(
            "OPERATION 02   /   RIVERBEND", "河湾镇", "西岸出发 / 突破河湾大桥",
            "旧码头已由保障队接管。北线驻防军扣下白石的运矿车，白石警卫队随即封锁东岸卸货场。两支分部都拒绝撤卡，正在争夺东岸桥头。我方从西岸渡桥，肃清东岸叛军，恢复公路通行。",
            "从西岸渡桥，击败东岸两支武装，打通通往工业带的公路。",
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
            if (campaign.FirstChapter) return Prologue;
            return campaign.RelayMission ? Relay : campaign.IslandMission ? Island : campaign.River != null ? Riverbend : Training;
        }
    }

#if !GODOT
    partial class CommandWindow {
        string[] StoryVictoryLines(string[] fallback) {
            if(world.RelayMission)return fallback;
            if(world.FirstChapter)return new[]{ChapterCompletionText[0],ChapterCompletionText[1],fallback[1]};
            if(world.Tutorial)return new[]{"旧码头的封锁解除，后续保障部队正在接管。","许岑：河湾大桥还在封锁中。我们先打通公路，再向内陆推进。",fallback[1]};
            if(world.IslandMission)return new[]{"矿业盆地已控制，灰港的外围补给支点建立。","周砚：几份封路令早于通信中断。原始记录在雾脊中继站。",fallback[1]};
            if(world.River!=null)return new[]{"河湾大桥恢复通行，保障队接管桥头。","先遣队沿内陆公路向西北保障岛进发。",fallback[1]};
            return fallback;
        }
        const string BriefingFactionNames = "北线(?:驻防军|驻防分部|分部)?|白石(?:矿务警卫队|警卫队|分部)?";
        static readonly Regex BriefingFactions = new Regex(BriefingFactionNames);
        static readonly Regex HarborOpeningFactions = new Regex(BriefingFactionNames + "|当地驻军|驻军");

        void BriefingParagraph(Graphics g, string text, RectangleF bounds, float size, Color color, bool harborOpening = false) {
            var font = F(size, false);
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var brush = new SolidBrush(color)) {
                format.FormatFlags &= ~StringFormatFlags.NoClip;
                format.Trimming = StringTrimming.None;
                g.DrawString(text, font, brush, bounds, format);

                var names = (harborOpening ? HarborOpeningFactions : BriefingFactions).Matches(text).Cast<Match>().ToArray();
                if (names.Length == 0) return;
                // Measuring the full paragraph keeps colored names aligned across line wraps.
                format.SetMeasurableCharacterRanges(names.Select(name => new CharacterRange(name.Index, name.Length)).ToArray());
                var regions = g.MeasureCharacterRanges(text, font, bounds, format);
                try {
                    for (int i = 0; i < regions.Length; i++) {
                        var coat = UnitColor(names[i].Value.StartsWith("白石", StringComparison.Ordinal) ? 2 : 1);
                        brush.Color = Color.FromArgb((3 * coat.R + 2 * pale.R) / 5,
                            (3 * coat.G + 2 * pale.G) / 5, (3 * coat.B + 2 * pale.B) / 5);
                        var state = g.Save();
                        g.SetClip(regions[i], CombineMode.Intersect);
                        g.DrawString(text, font, brush, bounds, format);
                        g.Restore(state);
                    }
                } finally {
                    foreach (var region in regions) region.Dispose();
                }
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
#endif
}
