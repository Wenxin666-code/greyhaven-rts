using System.Collections.Generic;
using System.Linq;

namespace Greyhaven {
internal sealed record BasinRoadOrder(string Reference, string Time, string Checkpoint, string Directive) {
    internal static readonly BasinRoadOrder[] All = {
        new("雾脊转发 041", "21:06", "北坡道口",
            "北坡东行正线即刻封闭。驶往石门的列车一律扣留，白石矿列原有通行单作废。扣车后登记机车及车皮编号，回报待查。"),
        new("雾脊转发 042", "21:09", "白石装矿道口",
            "换装站待发的白石矿列立即起车，照原定线路经北坡开往石门。原通行单继续有效，沿线各站不得截留。"),
        new("雾脊转发 043", "21:12", "折返道岔",
            "折返道岔封闭，原定班次一律停开。在站机车及空车皮留供调度专列调用，未经复令不得移交。"),
        new("雾脊转发 044", "21:15", "南堤道口",
            "南堤自收报起停止接车。凡已由换装站发出的补给列车，在折返道岔待避。接车时间另行通知。"),
        new("雾脊转发 045", "21:18", "石门东道口",
            "石门东道口封闭，暂停办理货运交接。已到列车留置，机车燃料及货单逐项登记，等候调度核验。")
    };
}

internal sealed class BasinOrderCustody {
    internal readonly record struct Defeat(int Winner, int Defeated, float At);
    readonly List<int>[] files = Enumerable.Range(0, BasinRoadOrder.All.Length + 1)
        .Select(_ => new List<int>()).ToArray();
    readonly int[] lastKiller = Enumerable.Repeat(-1, BasinRoadOrder.All.Length + 1).ToArray();
    readonly int[] lastMilitaryLoss = Enumerable.Repeat(-1, BasinRoadOrder.All.Length + 1).ToArray();
    readonly Queue<(int Document, int From)> unread = new();
    readonly List<Defeat> defeats = new();
    internal IReadOnlyList<Defeat> Defeats => defeats;
    internal int ReadCount { get; private set; }
    internal int UnreadCount => unread.Count;
    internal (int Document, int From) Current => unread.Peek();

    internal BasinOrderCustody() {
        for (int side = 1; side < files.Length; side++) files[side].Add(side);
    }

    internal void RecordLoss(Piece victim, Piece killer, bool military) {
        if (victim.Side <= 0 || killer.Side < 0 || killer.Side == victim.Side) return;
        lastKiller[victim.Side] = killer.Side;
        if (military) lastMilitaryLoss[victim.Side] = killer.Side;
    }

    internal void Collect(Campaign world) {
        int active = 0;
        foreach (var unit in world.Units)
            if (unit.Alive && unit.Side >= 0 && unit.T.CountsForElimination) active |= 1 << unit.Side;
        for (int side = 1; side < files.Length; side++) {
            if ((active & (1 << side)) == 0 && !defeats.Any(eventItem => eventItem.Defeated == side))
                defeats.Add(new Defeat(CreditedWinner(world, side), side, world.VisualTime));
            if (files[side].Count == 0 || (active & (1 << side)) != 0) continue;
            int holder = side, visited = 0;
            while (holder > 0 && (active & (1 << holder)) == 0 && (visited & (1 << holder)) == 0) {
                visited |= 1 << holder;
                holder = CreditedWinner(world, holder);
            }
            if (holder < 0 || (active & (1 << holder)) == 0) {
                if (active != 1) continue;
                holder = 0;
            }
            foreach (int document in files[side]) {
                if (holder == 0) unread.Enqueue((document, side));
                else files[holder].Add(document);
            }
            files[side].Clear();
        }
    }

    int CreditedWinner(Campaign world, int side) =>
        world.HasSurrendered(side) && lastMilitaryLoss[side] >= 0 ? lastMilitaryLoss[side] : lastKiller[side];

    internal void Acknowledge() {
        unread.Dequeue();
        ReadCount++;
    }
}
}
