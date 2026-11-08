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
            "灰港的海缆站在一夜之间失联，沿岸补给线随即遭到袭击。总部无法确认内陆局势，命令先遣队在旧码头建立滩头阵地。原定的野外训练已变成实战；你必须在教官的无线电引导下稳住阵脚，为后续行动打开入口。",
            "在旧码头建立能独立补给的基地，组织步战协同，夺回港区。",
            "消灭港区内全部敌方守军。");

        public static readonly MissionBriefing Riverbend = new MissionBriefing(
            "OPERATION 02   /   RIVERBEND", "河湾镇", "西南进入 / 内陆补给通道",
            "登陆点站稳后，一条残存的军用频道传回消息：通往内陆的补给车队被截在河湾镇。两支敌方武装正隔河争夺交通线，谁也不肯让出桥头。你的纵队从西南进入战区。这里没有可靠的盟友；必须依托当地矿区重建补给，击败两方势力，重新打通向内陆推进的道路。",
            "从西南滩头立足，击败盘踞河湾的两支武装，打通内陆通道。",
            "消灭两个敌方阵营的全部单位与建筑，或迫使其投降。");

        public static readonly MissionBriefing Island = new MissionBriefing(
            "OPERATION 03   /   ISLAND FORTRESS", "孤岛要塞", "西北资源岛 / 大陆反攻",
            "越过河湾后，先遣队终于与总部恢复了短暂联络：大陆五支敌对军团正争夺灰港腹地的控制权，彼此交火，也随时可能转向你。你奉命在西北近海资源岛建立反攻支点。岛上的矿藏足以支撑扩军，唯一的堤桥却也是敌军的入口。守住这条生命线，再把战线推向大陆，结束灰港的混战。",
            "以资源岛为反攻支点，守住堤桥，逐步清除大陆上的五支敌军。",
            "消灭五个敌方阵营的全部单位与建筑，或迫使其投降。");

        public static MissionBriefing For(Campaign campaign) {
            return campaign.IslandMission ? Island : campaign.River != null ? Riverbend : Training;
        }
    }

    partial class CommandWindow {
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
