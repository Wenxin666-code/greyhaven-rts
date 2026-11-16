using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
internal enum RiverbendScene { Opening, Battle, Departure, Result }

internal sealed class RiverbendOperation {
    internal const string HandoffRadio = "指挥官，趁他们鹬蚌相争，我们先在这里集结部队吧。\n桥西有间空着的值班室，派工兵占下来，步兵就能进去守住桥口。";
    internal const string CrossingRadio = "我们过桥了。接下来，肃清叛军吧！";
    const float OpeningSeconds = 17.5f;
    const float DepartureSpeed = 4.2f;
    internal static readonly PointF DepartureCameraStop = RiverbendMap.PortPoint(77.5f, 34);
    readonly Campaign battle;
    readonly Piece[] arrival, north, south, seizedTrucks, impoundGuards;
    readonly PointF[] arrivalSlots;
    sealed class MarchOrder {
        internal Piece Unit;
        internal PointF Exit;
        internal bool Leaving;
    }
    readonly List<MarchOrder> departing = new List<MarchOrder>();
    readonly Piece waitingTruck, followingTruck;
    readonly Dictionary<Piece, Piece> recalledGuards = new Dictionary<Piece, Piece>();
    Piece attackedFacility;
    PointF raidApproach;
    float facilityRecallAt;
    static readonly int[] GuardPostOffsets = { 0, -1, 1, -2, 2, 3 };
    Piece departureBase, departureEscort;
    Piece[] marchers;
    int nextMarcher;
    bool notifiedWithdrawal, noticedBridgeHouse;
    float nextMarchOrder, guidanceUntil;
    string guidance;

    internal RiverbendScene Scene { get; private set; }
    internal float Elapsed { get; private set; }
    internal bool Opening { get { return Scene == RiverbendScene.Opening; } }
    internal bool Departing { get { return Scene == RiverbendScene.Departure; } }
    internal bool Result { get { return Scene == RiverbendScene.Result; } }
    internal bool Ready { get { return Opening && Elapsed >= OpeningSeconds &&
        (arrival.All(unit => unit.Path.Count == 0) || Elapsed >= 24); } }
    internal bool Crossed { get; private set; }
    internal Piece CommandVehicle { get { return arrival[0]; } }
    internal Piece RoadTruck { get { return waitingTruck; } }
    internal string Guidance { get { return battle.Time < guidanceUntil ? guidance : null; } }
    internal Rectangle Bridge { get { return battle.River.Bridges[0]; } }
    internal Piece DepartureSubject {
        get {
            if (departureBase != null) return departureBase;
            return departing.Where(order => order.Unit.Path.Count > 0)
                .OrderByDescending(order => order.Unit.P.X).Select(order => order.Unit)
                .FirstOrDefault() ?? marchers.FirstOrDefault() ?? waitingTruck;
        }
    }
    internal PointF DepartureFocus {
        get {
            var subject = DepartureSubject;
            if (subject != departureBase || departureEscort == null ||
                Campaign.Dist(subject.P, departureEscort.P) > 13 * Campaign.Tile) return subject.P;
            return HarborOperation.Glide(subject.P, departureEscort.P, .38f);
        }
    }
    internal bool HasDepartureTroops { get { return marchers.Length > 0; } }

    internal RiverbendOperation(Campaign battle) {
        this.battle = battle;
        Scene = RiverbendScene.Battle;
        var home = battle.River.WestStart;
        arrival = new[] {
            Spawn("mcv", 0, home.X, home.Y),
            Spawn("ifv", 0, home.X + 1.6f, home.Y + 2.4f),
            Spawn("squad", 0, home.X + 3f, home.Y - 2.1f),
            Spawn("squad", 0, home.X + .7f, home.Y - 2.4f),
            Spawn("engineer", 0, home.X - 2f, home.Y - 2.1f)
        };
        arrivalSlots = arrival.Select(unit => unit.P).ToArray();
        for (int side = 1; side <= 2; side++) {
            var camp = battle.Home(side);
            battle.Spawn("hq", side, camp.X, camp.Y);
        }
        BuildCamp(1, new Point(72, 13), new Point(77, 16), new Point(76, 24), new Point(70, 20));
        BuildCamp(2, new Point(73, 57), new Point(77, 55), new Point(77, 48), new Point(69, 51));
        north = new[] {
            Spawn("squad", 1, 58.7f, 32.1f), Spawn("at", 1, 63.8f, 30.1f),
            Spawn("tank", 1, 63.2f, 28.3f), Spawn("ifv", 1, 66.3f, 28.8f),
            Spawn("squad", 1, 60.7f, 30.3f), Spawn("squad", 1, 65.1f, 29.1f)
        };
        south = new[] {
            Spawn("squad", 2, 60.5f, 35.1f), Spawn("at", 2, 64.5f, 37.5f),
            Spawn("tank", 2, 66.6f, 39.6f), Spawn("ifv", 2, 63.3f, 41.9f),
            Spawn("squad", 2, 62.1f, 38.2f), Spawn("squad", 2, 67.4f, 40.5f)
        };
        SeedReserve(1, 69, 22.8f, 1);
        SeedReserve(2, 69, 47.8f, -1);
        seizedTrucks = new[] { ParkedTruck(57.9f, 26.8f, true), ParkedTruck(60f, 26.8f, true) };
        impoundGuards = new[] {
            Spawn("squad", 1, 57.5f, 27.9f),
            Spawn("squad", 1, 59.2f, 28.3f),
            Spawn("squad", 1, 61.1f, 27.7f)
        };
        foreach (var guard in impoundGuards) {
            guard.Dir = 4;
            guard.Turn = 4;
            battle.Guard(guard);
        }
        waitingTruck = ParkedTruck(Bridge.Left - 6, Bridge.Bottom + .7f, false);
        followingTruck = ParkedTruck(Bridge.Left - 9, Bridge.Bottom + .7f, false);
        battle.Radio = "许岑：" + HandoffRadio.Replace('\n', ' ');
        battle.RevealRiverbendIntelligence();
    }

    void BuildCamp(int side, Point power, Point refinery, Point barracks, Point factory) {
        BuildAt("power", side, power);
        var plant = BuildAt("refinery", side, refinery);
        BuildAt("barracks", side, barracks);
        BuildAt("factory", side, factory);
        var dock = Campaign.Middle(Campaign.ExitApron(plant.T, Campaign.Footprint(plant.T, plant.P)));
        battle.Spawn("miner", side, dock.X, dock.Y).DepartingDock = plant;
        plant.StarterDelivered = true;
        battle.Funds[side] = 4200;
    }

    Piece BuildAt(string key, int side, Point origin) {
        var type = Campaign.Info(key);
        return battle.Spawn(key, side, (origin.X + type.FoundationWidth * .5f) * Campaign.Tile,
            (origin.Y + type.FoundationHeight * .5f) * Campaign.Tile);
    }

    void SeedReserve(int side, float x, float y, int direction) {
        Spawn("tank", side, x + 1.4f, y + .5f * direction);
        Spawn("ifv", side, x - .5f, y + 1.7f * direction);
        Spawn("at", side, x + 2.7f, y + 1.6f * direction);
        for (int i = 0; i < 4; i++) Spawn("squad", side, x + 3.8f + i, y + (i % 2) * 1.2f * direction);
        Spawn("engineer", side, x + 6.2f, y - 1.6f * direction);
    }

    Piece Spawn(string type, int side, float x, float y) {
        var point = RiverbendMap.PortPoint(x, y);
        return battle.Spawn(type, side, point.X, point.Y);
    }

    Piece ParkedTruck(float x, float y, bool loaded) {
        var truck = Spawn("miner", -1, x, y);
        truck.Mining = false;
        truck.Cargo = loaded ? Campaign.LoadValue : 0;
        truck.Dir = 4;
        truck.Turn = 4;
        return truck;
    }

    internal bool IsSeizedTruck(Piece actor) { return seizedTrucks.Contains(actor); }
    internal bool IsImpoundGuard(Piece actor) { return impoundGuards.Contains(actor); }

    internal static bool IsBaseFacility(Piece unit) {
        return unit.T.Key == "hq" || unit.T.Key == "barracks" || unit.T.Key == "factory";
    }

    internal void ReportFacilityHit(Piece facility, Piece attacker) {
        if ((!Opening && Scene != RiverbendScene.Battle) || facility.Side != 1 || attacker.Side != 2 ||
            !IsBaseFacility(facility)) return;
        if (attackedFacility != null && attackedFacility.Alive && attackedFacility.Side == 1 &&
            (attackedFacility == facility || battle.Time < facilityRecallAt + 8)) return;
        attackedFacility = facility;
        raidApproach = attacker.Carrier == null ? attacker.P : attacker.Carrier.P;
        facilityRecallAt = battle.Time;
    }

    internal void RecallImpoundGuards() {
        if (attackedFacility == null) return;
        var facility = attackedFacility.Alive && attackedFacility.Side == 1 ? attackedFacility :
            battle.Live(1).Where(IsBaseFacility).OrderBy(unit => Campaign.Dist(unit.P, attackedFacility.P)).FirstOrDefault();
        if (facility == null || !facility.Deployed) return;
        var center = facility.P;
        var angle = Math.Atan2(raidApproach.Y - center.Y, raidApproach.X - center.X);
        var footprint = Campaign.Footprint(facility.T, center);
        float radius = Math.Max(footprint.Width, footprint.Height) * .5f + Campaign.Tile * 1.25f;
        for (int i = 0; i < impoundGuards.Length; i++) {
            var guard = impoundGuards[i];
            if (!guard.Deployed || guard.Side != 1 ||
                recalledGuards.TryGetValue(guard, out var previous) && previous == facility) continue;
            foreach (var offset in GuardPostOffsets) {
                double heading = angle + (i - 1 + offset) * Math.PI / 3;
                var post = new PointF(center.X + (float)Math.Cos(heading) * radius,
                    center.Y + (float)Math.Sin(heading) * radius);
                if (impoundGuards.Any(other => other != guard && other.Deployed &&
                    other.GuardHome.HasValue && Campaign.Dist(other.GuardHome.Value, post) < Campaign.Tile)) continue;
                if (!battle.GuardAt(guard, post)) continue;
                recalledGuards[guard] = facility;
                break;
            }
        }
    }

    internal void BeginOpening() {
        Scene = RiverbendScene.Opening;
        Elapsed = 0;
        for (int i = 0; i < arrival.Length; i++) {
            var unit = arrival[i];
            unit.P = new PointF(arrivalSlots[i].X - 10 * Campaign.Tile, arrivalSlots[i].Y);
            unit.Dir = 4;
            unit.Turn = 4;
        }
        for (int i = 0; i < arrival.Length; i++) battle.Move(arrival[i], arrivalSlots[i], false);
        battle.BeginRiverbendFront(1, north, RiverbendMap.PortPoint(62, 31), RiverbendMap.PortPoint(65, 32));
        battle.BeginRiverbendFront(2, south, RiverbendMap.PortPoint(65, 35.8f), RiverbendMap.PortPoint(61.5f, 37.3f));
    }

    internal bool TakeCommand() {
        if (!Ready) return false;
        Scene = RiverbendScene.Battle;
        Announce(HandoffRadio, 24);
        return true;
    }

    void Announce(string text, float duration = 12) {
        guidance = text;
        guidanceUntil = battle.Time + duration;
    }

    internal void UpdateBattleGuidance() {
        if (Scene != RiverbendScene.Battle) return;
        var guardRoom = new PointF((RiverbendMap.RiverBridgeHouse.X + 1) * Campaign.Tile,
            (RiverbendMap.RiverBridgeHouse.Y + 1) * Campaign.Tile);
        if (!noticedBridgeHouse && Guidance == null &&
            battle.Live(0).Any(unit => unit.T.Key == "engineer" && Campaign.Dist(unit.P, guardRoom) < 4 * Campaign.Tile) &&
            battle.Units.Any(unit => unit.Deployed && unit.Side < 0 && unit.T.Garrison && unit.P == guardRoom)) {
            noticedBridgeHouse = true;
            Announce("值班室就在路边。工兵占下来以后，让步兵进去守住桥口。");
        }
        if (!Crossed && battle.Live(0).Any(unit => !unit.T.Building && battle.Armed(unit) &&
            unit.P.X > (Bridge.Right + .5f) * Campaign.Tile)) {
            Crossed = true;
            Announce(CrossingRadio);
        }
        if (!notifiedWithdrawal && (battle.Remaining(1) == 0) != (battle.Remaining(2) == 0)) {
            notifiedWithdrawal = true;
            int defeated = battle.Remaining(1) == 0 ? 1 : 2;
            Announce(battle.StoryFactionName(defeated) + "已经退出战斗。把东岸最后一处封锁清掉。");
        }
    }

    internal bool BeginDeparture() {
        if (Scene != RiverbendScene.Battle || !battle.Won || !battle.OutcomeReady) return false;
        Scene = RiverbendScene.Departure;
        Elapsed = 0;
        battle.RevealRiverbendRoad();
        foreach (var house in battle.Live(0).Where(unit => unit.T.Garrison))
            battle.EvacuateHouse(house);
        var bases = battle.Units.Where(unit => unit.Side == 0 && unit.Alive && !unit.Selling &&
            (unit.T.Key == "mcv" || unit.T.Key == "hq" && (unit.Deployed || unit.Packing > 0)));
        departureBase = bases.OrderBy(unit => unit != CommandVehicle).FirstOrDefault();
        if (departureBase != null && departureBase.T.Key == "hq" &&
            departureBase.Packing <= 0 && !battle.Relocate(departureBase, departureBase.P))
            departureBase = null;
        var rally = departureBase == null ? RiverbendMap.PortPoint(Bridge.Right + 2, 34) : departureBase.P;
        marchers = battle.Live(0).Where(unit => unit != departureBase && !unit.T.Building && unit.T.Key != "miner")
            .OrderBy(unit => Campaign.Dist(unit.P, rally)).ToArray();
        if (departureBase != null) {
            departureEscort = marchers.FirstOrDefault(unit => !unit.T.Foot && battle.Armed(unit));
            marchers = new[] { departureBase }.Concat(marchers).ToArray();
        }
        foreach (var unit in marchers) battle.Halt(unit);
        if (waitingTruck.Deployed)
            battle.MoveHarborDeparture(waitingTruck, RiverbendMap.PortPoint(RiverbendMap.RiverExitX, 36.65f));
        if (followingTruck.Deployed)
            battle.MoveHarborDeparture(followingTruck, RiverbendMap.PortPoint(RiverbendMap.RiverExitX - 3, 35.7f));
        nextMarchOrder = 1.3f;
        return true;
    }

    void AddMarchOrders() {
        // Infantry use the shoulders and vehicles use the two inner lanes.
        int foot = 0, vehicles = 0;
        foreach (var unit in marchers) {
            int index = unit.T.Foot ? foot++ : vehicles++;
            float lane = unit.T.Foot ? (index % 2 == 0 ? 32.1f : 35.85f) : (index % 2 == 0 ? 33.35f : 34.6f);
            float exit = Math.Max(Bridge.Right + 5, RiverbendMap.RiverExitX - index / 2 * 1.5f);
            departing.Add(new MarchOrder { Unit = unit, Exit = RiverbendMap.PortPoint(exit, lane) });
        }
    }

    void OrderMarch(MarchOrder order) {
        var unit = order.Unit;
        if (unit.Path.Count != 0) return;
        if (unit == departureBase && Campaign.Dist(unit.P, order.Exit) > 9 &&
            battle.MoveHarborDeparture(unit, order.Exit)) {
            order.Leaving = true;
            return;
        }
        var lane = order.Exit.Y;
        float x = unit.P.X / Campaign.Tile - .5f;
        float roadX = Math.Max(Bridge.Left - 2, Math.Min(x + 3, order.Exit.X / Campaign.Tile - 2));
        bool holdEscort = !order.Leaving && unit == departureEscort &&
            departureBase.P.X < (Bridge.Right - 8) * Campaign.Tile &&
            x > Bridge.Right - 2 && Elapsed < 15;
        if (holdEscort) {
            var rendezvous = RiverbendMap.PortPoint(Bridge.Right + 1, 34.6f);
            if (Campaign.Dist(unit.P, rendezvous) > Campaign.Tile * .6f)
                battle.MoveHarborDeparture(unit, rendezvous);
            return;
        }
        bool nearLane = Math.Abs(unit.P.Y - lane) < Campaign.Tile * .75f;
        if (!order.Leaving && !nearLane && x < RiverbendMap.RiverExitX - 8) {
            foreach (float offset in new[] { 0f, 2f, -2f, 4f }) {
                var join = new PointF((roadX + offset + .5f) * Campaign.Tile, lane);
                if (battle.MoveHarborDeparture(unit, join)) return;
            }
        }
        if (Campaign.Dist(unit.P, order.Exit) > 9)
            order.Leaving = battle.MoveHarborDeparture(unit, order.Exit);
    }

    internal void Advance(float dt) {
        if (Opening) {
            Elapsed += dt / MatchTempo.DefaultRate;
            battle.Tick(dt);
            return;
        }
        if (!Departing && !Result) return;
        Elapsed += dt / MatchTempo.DefaultRate;
        battle.TickVisuals(dt);
        if (departureBase != null && departureBase.Packing > 0)
            battle.AdvanceHarborPacking(departureBase, dt);
        if (Elapsed >= nextMarchOrder && marchers.Length > 0) {
            if (departing.Count == 0) AddMarchOrders();
            // Spread navigation work across frames if the player brings a large force.
            int end = Math.Min(departing.Count, nextMarcher + 3);
            for (; nextMarcher < end; nextMarcher++) OrderMarch(departing[nextMarcher]);
            if (nextMarcher == departing.Count) nextMarcher = 0;
            nextMarchOrder = Elapsed + .13f;
        }
        battle.MoveHarborVehicle(waitingTruck, dt, 1.18f, true);
        battle.MoveHarborVehicle(followingTruck, dt, 1.18f, true);
        foreach (var order in departing) {
            var unit = order.Unit;
            if (unit.Path.Count == 0) continue;
            float pace = order.Leaving ? Campaign.Info("mcv").Speed * DepartureSpeed / ReferenceBalance.Speed(unit)
                : unit == departureBase ? DepartureSpeed : 2.3f;
            // The civilian and formation pace rules are for combat. The convoy leader
            // holds a consistent cruise speed and still yields to actual collision.
            battle.MoveHarborVehicle(unit, dt, pace, true, unit != departureBase);
        }
        if (Departing && Elapsed >= 13.5f && (Elapsed >= 18 ||
            departureEscort == null || departureEscort.Path.Count > 0 &&
                Campaign.Dist(departureBase.P, departureEscort.P) < 13 * Campaign.Tile))
            Scene = RiverbendScene.Result;
    }
}

public partial class Campaign {
    internal RiverbendOperation RiverbendOperation { get; private set; }
    bool RiverbendRivalry { get { return RiverbendOperation != null && Remaining(1) > 0 && Remaining(2) > 0; } }
    const float RiverbendOrderInterval = 7;
    const float RiverbendWithdrawalFraction = .20f;
    const float RiverbendRegroupSeconds = 12;
    const float RiverbendTurnPreparation = 24;
    const float RiverbendTurnWaveInterval = 30;
    const int RiverbendVanguardSize = 5;
    const int RiverbendFollowupSize = 8;

    bool RiverbendHasBase(int side) {
        return Units.Any(unit => unit.Alive && unit.Side == side && RiverbendOperation.IsBaseFacility(unit));
    }

    bool RiverbendFieldTarget(Piece unit) { return Armed(unit) || unit.T.Key == "engineer" || unit.T.Key == "mcv"; }

    bool KnowsRiverbendTarget(int side, Piece[] known) {
        return known.Any(unit => unit.Side == side && RiverbendFieldTarget(unit));
    }

    bool RiverbendCanTarget(int side, Piece[] known) {
        return side == 0 || RiverbendHasBase(side) || KnowsRiverbendTarget(side, known);
    }

    void PressRiverbend(int side, AiBrain brain, Piece[] known) {
        if (side == 1) RiverbendOperation.RecallImpoundGuards();
        RetreatAiMiners(side, brain, known);
        int opponent = AiOpponent(side, brain, known);
        brain.Opponent = opponent;
        if (opponent < 0) return;

        int rival = 3 - side;
        bool rivalHasBase = RiverbendHasBase(rival);
        bool lostRivalBase = brain.RiverbendRivalHasBase && !rivalHasBase;
        brain.RiverbendRivalHasBase = rivalHasBase;
        bool rivalContact = KnowsRiverbendTarget(rival, known);
        bool leavingRival = opponent == 0 && (HasSurrendered(rival) || !rivalHasBase && !rivalContact);
        bool startingTurn = leavingRival && brain.RiverbendRivalDefeatedAt < 0;
        if (startingTurn) {
            brain.RiverbendRivalDefeatedAt = Time;
            brain.NextPush = Math.Max(brain.NextPush, Time + RiverbendTurnPreparation);
        }

        foreach (var wing in brain.Wings.ToArray()) {
            wing.Units = wing.Units.Where(unit => unit.Deployed).ToArray();
            if (wing.Units.Length == 0) {
                brain.Wings.Remove(wing);
                continue;
            }
            if (wing.Phase == 3) {
                RecoverRiverbendWing(brain, wing);
                continue;
            }
            if (AiWingStrength(wing.Units) <= wing.InitialValue * RiverbendWithdrawalFraction) {
                ReturnAiWing(side, brain, wing);
                wing.NextRetreatOrder = Time + RiverbendOrderInterval;
                continue;
            }
            if (leavingRival && wing.Opponent == rival) {
                // Release the old assignments without redirecting every surviving unit
                // in the same update. Units in contact with the player keep fighting.
                foreach (var unit in wing.Units) {
                    CancelAiMove(unit);
                    var target = unit.Target;
                    if (target == null || !target.Alive || target.Side == rival) Guard(unit);
                }
                brain.Wings.Remove(wing);
                continue;
            }
            bool chaseSurvivors = wing.Opponent == rival && lostRivalBase && rivalContact;
            bool retarget = HasSurrendered(wing.Opponent) || Remaining(wing.Opponent) == 0 || chaseSurvivors;
            if (!retarget && Time < wing.OrderedAt + RiverbendOrderInterval) continue;
            if (retarget) wing.Opponent = chaseSurvivors ? rival : opponent;

            // Keep live engagements and moving columns intact. Idle or blocked
            // survivors get a fresh attack route, even when only one remains.
            var ready = wing.Units.Where(unit => (unit.Target == null || !unit.Target.Alive) &&
                (retarget || !aiMovesPending.Contains(unit) &&
                    (unit.Path.Count == 0 || unit.StalledFor >= 3 || unit.NoProgressFor >= 3))).ToArray();
            if (ready.Length == 0) continue;
            wing.Goal = wing.Objective = AiObjective(side, wing.Opponent, known);
            wing.OrderedAt = Time;
            QueueAiMarch(ready, wing.Goal, replace: retarget, splitByMobility: true);
        }

        if (Time < brain.NextPush) return;
        // Opening orders stage the fighting seen by the camera. Once the first
        // attack can launch, bridge detachments join the same pool as reinforcements.
        foreach (var unit in brain.FieldGuard) {
            unit.GuardHome = null;
            unit.GuardReturning = unit.GuardTravelling = false;
        }
        brain.FieldGuard.Clear();
        var available = AiAvailable(side, brain, anywhere: true);
        int limit = int.MaxValue;
        if (leavingRival)
            limit = brain.LastDispatch[0] < brain.RiverbendRivalDefeatedAt ? RiverbendVanguardSize : RiverbendFollowupSize;
        if (available.Length > 0 && LaunchAiWing(side, brain, SelectAssaultUnits(side, brain, available, limit), opponent, known, false))
            brain.NextPush = Time + (leavingRival ? RiverbendTurnWaveInterval : RiverbendOrderInterval);
    }

    void RecoverRiverbendWing(AiBrain brain, AiWing wing) {
        if (wing.RegroupUntil > 0) {
            if (Time >= wing.RegroupUntil) brain.Wings.Remove(wing);
            return;
        }
        if (wing.Units.All(unit => Dist(unit.P, wing.Goal) <= 5 * Tile) || Time - wing.OrderedAt > 100) {
            foreach (var unit in wing.Units) {
                CancelAiMove(unit);
                Guard(unit);
            }
            wing.RegroupUntil = Time + RiverbendRegroupSeconds;
            return;
        }
        if (Time < wing.NextRetreatOrder) return;
        var stalled = wing.Units.Where(unit => Dist(unit.P, wing.Goal) > 5 * Tile &&
            !aiMovesPending.Contains(unit) &&
            (unit.Path.Count == 0 || unit.NoProgressFor >= 3 || unit.StalledFor >= 3)).ToArray();
        foreach (var unit in stalled) {
            Halt(unit);
            unit.GuardReturning = true;
        }
        QueueAiMarch(stalled, wing.Goal, attackMove: false, splitByMobility: true);
        wing.NextRetreatOrder = Time + RiverbendOrderInterval;
    }

    void RevealRiverbendTiles(Rectangle region) {
        for (int y = FogRow(region.Top * Tile); y < FogRow(region.Bottom * Tile); y++)
            for (int x = FogColumn(region.Left * Tile); x < FogColumn(region.Right * Tile); x++)
                Discover(0, x, y);
    }

    internal void RevealRiverbendIntelligence() {
        RevealRiverbendTiles(new Rectangle(10, 25, 29, 23));
        RevealRiverbendTiles(new Rectangle(37, 29, 22, 13));
        RevealRiverbendTiles(new Rectangle(55, 24, 17, 23));
    }

    internal void RevealRiverbendRoad() { RevealRiverbendTiles(new Rectangle(10, 23, 113, 23)); }
    internal void BeginRiverbendFront(int side, Piece[] fighters, PointF armor, PointF flank) {
        var brain = Brain(side);
        brain.NextPush = 10 + side * 2;
        brain.FieldGuard.UnionWith(fighters);
        foreach (var fighter in fighters) Guard(fighter);
        GuardAt(fighters[2], armor);
        GuardAt(fighters[3], flank);
        GuardAt(fighters[5], new PointF(flank.X + (side == 1 ? 1 : -1) * Tile, flank.Y));
    }
}
}
