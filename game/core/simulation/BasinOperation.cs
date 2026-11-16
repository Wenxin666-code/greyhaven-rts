using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
internal sealed class BasinOperation {
    internal static readonly PointF Layby = RiverbendMap.PortPoint(RiverbendMap.BasinLayby.X, RiverbendMap.BasinLayby.Y);
    internal const string OpeningRadio = "指挥官，河湾送来的车到了。公路只到这儿，去石门的货得换火车。";
    internal const string LoadingRadio = "两车的货分开点数，装上平车。装好先别发，前头还没给放行信号。";
    internal const string StagingRadio = "路看清了。工程车就在站边，咱们从这里打过去。";
    internal const string HandoffRadio = "指挥官，先在站边展开。油田已经在给咱们供款，矿在货场南面。\n打下道口以后，把他们收到的公文也带回来。";
    internal const int OilPayout = 120, OilPayoutSeconds = 6;
    internal const string ClearRailRadio = "轨道上的车往两边让。给列车留路。";
    internal const string EndRadio = "车组，道岔扳好了，可以发车。石门那边还等着呢，路上留神。";
    internal const string LeadRadio = "指挥官，五份我都对过了。最晚一份是二十一点十八分收的，那时港务还没断线。\n都经雾脊转发，查签发人得去中继站翻底档。";
    internal const string GoToRelayRadio = "石门的货交给车组。周砚，把这五份原件收好，咱们去雾脊。";

    internal static string ContactReport(int side) => side switch {
        1 => "北线驻防军的装甲还在道口。先看清坦克的位置，再让步兵上。",
        2 => "白石警卫队露面了。咱们要是打矿场，留人盯着来路，他们可能会抽兵回援。",
        3 => "铁轨卫队的小队到了。留点人看着侧翼，别让他们绕到炮兵后头。",
        4 => "到南岸防卫队的地盘了。路边房屋先看清有没有人，再从街上过去。",
        _ => "边境独立旅的巡逻队已经到了这边。给咱们的矿车留个护卫。"
    };

    readonly Campaign world;
    readonly Piece[] trucks;
    float transferWork, nextParkingOrder;
    internal readonly List<Piece> RailBarricades = new();
    internal bool CommandTaken { get; private set; }
    float guidanceUntil;
    float nextOilPayout = float.PositiveInfinity;
    internal readonly Piece CommandVehicle;
    internal Piece OilDerrick { get; }
    internal readonly BasinOrderCustody Orders = new();
    internal BasinFreight Freight { get; }
    internal string Guidance => world.Time < guidanceUntil ? HandoffRadio
        : world.Time < world.StoryNoticeUntil ? world.StoryNotice : null;
    internal bool StillBriefing => world.Time < guidanceUntil;
    internal PointF ConvoyFocus => HarborOperation.Glide(trucks[1].P, trucks[0].P, .57f);
    internal bool ConvoyParked => Docked(0) && Docked(1);
    internal bool IsStoryTruck(Piece unit) => unit == trucks[0] || unit == trucks[1];

    internal BasinOperation(Campaign world) {
        this.world = world;
        Freight = new BasinFreight(world);
        var baseSite = world.River.Starts[0];
        CommandVehicle = Spawn("mcv", 0, baseSite.X, baseSite.Y);
        Spawn("squad", 0, baseSite.X + 3, baseSite.Y - 2);
        Spawn("squad", 0, baseSite.X + 3, baseSite.Y + 2);
        Spawn("at", 0, baseSite.X - 3, baseSite.Y + 2);
        OilDerrick = BuildAt("oilwell", 0, 15, 121);
        world.Funds[0] = 8200;

        var lot = RiverbendMap.TransferLot;
        BuildAt("municipal", -1, lot.X, lot.Y);
        trucks = new Piece[2];
        for (int i = 0; i < trucks.Length; i++) {
            var bay = Freight.LoadingBay(i);
            trucks[i] = world.Spawn("miner", -1, bay.X, bay.Y);
        }
        foreach (var truck in trucks) {
            truck.Mining = false;
            truck.Cargo = Campaign.LoadValue;
            truck.Dir = 4;
            truck.Turn = 4;
        }
        for (int side = 1; side < world.FactionCount; side++) {
            var home = world.River.Starts[side];
            BuildAt("hq", side, home.X - 1, home.Y - 1);
            BuildAt("power", side, home.X + 3, home.Y - 3);
            BuildAt("barracks", side, home.X - 6, home.Y - 3);
            var refinery = BuildAt("refinery", side, home.X - 5, home.Y + 3);
            refinery.StarterDelivered = true;
            var dock = Campaign.DockPoint(refinery);
            world.Spawn("miner", side, dock.X, dock.Y).DepartingDock = refinery;
            BuildAt("factory", side, home.X + 3, home.Y + 4);
            Spawn("squad", side, home.X + 1, home.Y + 4);
            Spawn("at", side, home.X + 5, home.Y + 2);
            Spawn("squad", side, home.X - 2, home.Y);
            Spawn("tank", side, home.X + 7, home.Y + 1);
            world.Funds[side] = 5100;
        }
        for (int side = 1; side < world.FactionCount; side++) {
            var post = world.River.BasinPost(side);
            var bunker = BuildAt("bunker", side, post.X, post.Y);
            foreach (var run in world.River.BasinWallRuns(side))
                for (int y = run.Top; y < run.Bottom; y++)
                    for (int x = run.Left; x < run.Right; x++) BuildAt("wall", side, x, y);
            var barricade = world.River.BasinRailBarrier(side);
            for (int y = barricade.Top; y < barricade.Bottom; y++)
                for (int x = barricade.Left; x < barricade.Right; x++)
                    RailBarricades.Add(BuildAt("wall", side, x, y));
            var sentry = Spawn("squad", side, post.X + 3, post.Y + 1);
            if (!world.EnterHouse(sentry, bunker)) world.Guard(sentry);
        }
        DeployOutriders(1, 97, 23, "tank");
        DeployOutriders(2, 110, 19, "ifv");
        DeployOutriders(3, 109, 89, "ifv");
        DeployOutriders(4, 99, 95, "ifv");
        DeployOutriders(4, 210, 148, "ifv");
        DeployOutriders(5, 220, 152, "ifv");
        foreach (var unit in world.Live(0).Where(piece => !piece.T.Building && piece != CommandVehicle)) world.Guard(unit);
    }

    void DeployOutriders(int side, float x, float y, string vehicle) {
        Spawn("squad", side, x, y);
        Spawn("squad", side, x + 1, y - 1);
        Spawn(vehicle, side, x + 2, y);
    }

    Piece Spawn(string key, int side, float x, float y) {
        var point = RiverbendMap.PortPoint(x, y);
        return world.Spawn(key, side, point.X, point.Y);
    }

    Piece BuildAt(string key, int side, int x, int y) {
        var type = Campaign.Info(key);
        return world.Spawn(key, side, (x + type.FoundationWidth * .5f) * Campaign.Tile,
            (y + type.FoundationHeight * .5f) * Campaign.Tile);
    }

    internal void HandOff() {
        CommandTaken = true;
        guidanceUntil = world.Time + 36;
        nextOilPayout = world.Time + OilPayoutSeconds;
        world.Radio = HandoffRadio;
    }

    internal void TickOilIncome() {
        if (world.Time < nextOilPayout || !OilDerrick.Deployed || OilDerrick.Side < 0) return;
        nextOilPayout += OilPayoutSeconds;
        world.Funds[OilDerrick.Side] += OilPayout;
        OilDerrick.LastReceipt = OilPayout;
        OilDerrick.ReceiptAt = world.Time;
    }

    internal void BeginArrival() {
        var column = world.Live(0).Where(unit => !unit.T.Building).ToArray();
        var stops = column.Select(unit => unit.P).ToArray();
        for (int i = 0; i < column.Length; i++) {
            column[i].P.X -= 6 * Campaign.Tile;
            column[i].Turn = column[i].Dir = 4;
        }
        for (int i = 0; i < column.Length; i++) world.Move(column[i], stops[i], false);
        for (int i = 0; i < trucks.Length; i++) {
            var truck = trucks[i];
            truck.P = RiverbendMap.PortPoint(1 - i * 6, 113.5f + .8f * i);
            var bend = RiverbendMap.PortPoint(8 - i * 2, 113 + .8f * i);
            var bay = Freight.LoadingBay(i);
            var wagon = Freight.Wagon(i);
            var lineUp = new PointF(bay.X - wagon.Direction.X * Campaign.Tile * 1.4f,
                bay.Y - wagon.Direction.Y * Campaign.Tile * 1.4f);
            if (!world.TrySetHarborRoute(truck, new[] { bend, lineUp, bay }, trucks))
                world.MoveHarborDeparture(truck, bay, trucks);
        }
    }

    bool Docked(int index) => trucks[index].Path.Count == 0 &&
        Campaign.Dist(trucks[index].P, Freight.LoadingBay(index)) <= Campaign.Tile * .5f;

    internal void AdvanceArrival(float dt) {
        foreach (var truck in trucks) world.MoveHarborVehicle(truck, dt, 2f, replanTraffic: true);
        if (Freight.Loads == trucks.Length) return;
        if (world.VisualTime >= nextParkingOrder) {
            nextParkingOrder = world.VisualTime + .65f;
            for (int i = 0; i < trucks.Length; i++)
                if (trucks[i].Path.Count == 0 && !Docked(i))
                    world.MoveHarborDeparture(trucks[i], Freight.LoadingBay(i), trucks);
        }
        int index = Freight.Loads;
        if (!Docked(index)) return;
        transferWork += dt;
        if (transferWork < 1.55f) return;
        trucks[index].Cargo = 0;
        Freight.Loads++;
        transferWork = 0;
    }

    internal void BeginFreightDeparture() {
        world.RemoveBasinRailBarricades(RailBarricades);
        Freight.PrepareDeparture();
    }
}

public partial class Campaign {
    internal BasinOperation BasinOperation { get; private set; }

    internal void RemoveBasinRailBarricades(IEnumerable<Piece> barriers) {
        foreach (var wall in barriers)
            if (wall.Alive) Kill(wall);
    }

    internal void RevealBasinScene(PointF center, float radius) {
        for (int y = Math.Max(0, FogRow(center.Y - radius)); y <= Math.Min(MapFogRows - 1, FogRow(center.Y + radius)); y++)
            for (int x = Math.Max(0, FogColumn(center.X - radius)); x <= Math.Min(MapFogCols - 1, FogColumn(center.X + radius)); x++) {
                var point = new PointF(FogOriginX + (x + .5f) * FogSize, FogOriginY + (y + .5f) * FogSize);
                if (Dist(center, point) <= radius) Discover(0, x, y);
            }
    }

}
}
