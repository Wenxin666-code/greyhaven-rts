using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
internal enum HarborScene {
    SupplyRoad, QuayWork, Blackout, Checkpoint, ApproachShore, LowerRamp, LandParty,
    Battle, ReopenGate, ReturnShip, Muster, Dispatch, Complete
}

internal sealed class HarborOperation {
    const float CraneUnloadTime = 4.95f, CraneRelease = .85f;
    const float DeckLevel = .405f, RampHinge = 2.14f, RampTip = 3.12f;
    internal static readonly PointF CargoBerth = RiverbendMap.PortPoint(54, 15);
    static readonly float CargoChannelY = RiverbendMap.PortPoint(54, 6).Y;
    static readonly PointF Shore = RiverbendMap.PortPoint(13.17f, 46);
    internal static readonly PointF[] QuayTruckBays = { RiverbendMap.PortPoint(52.65f, 26.65f), RiverbendMap.PortPoint(54.8f, 26.65f) };
    static readonly PointF QuayGuidePost = RiverbendMap.PortPoint(53.75f, 27.55f);
    static readonly PointF[] QuayPatrol = { RiverbendMap.PortPoint(53.75f, 24.75f), RiverbendMap.PortPoint(51.5f, 24.75f),
        RiverbendMap.PortPoint(50, 26.4f), RiverbendMap.PortPoint(51.4f, 27.65f), RiverbendMap.PortPoint(53.75f, 28.2f) };
    static readonly RectangleF[] ClosedGate = RiverbendMap.HarborGatePosts.Concat(new[] { RiverbendMap.HarborBarrier }).ToArray();
    static readonly PointF[] ReceivingBays = { RiverbendMap.PortPoint(50.15f, 26.65f), RiverbendMap.PortPoint(48.7f, 27.2f) };
    static readonly float[] FreightLaneOffsets = { 0, -.95f, .95f, -1.9f, 1.9f };
    static readonly float DepartureFront = RiverbendMap.HarborGateCenter.X - 2.4f * Campaign.Tile;
    internal static readonly PointF DispatchCamera = RiverbendMap.PortPoint(61.55f, 32.5f);
    static readonly PointF[] DeckSlots = { new PointF(1.03f, 0), new PointF(.08f, -.28f), new PointF(-.73f, .29f) };
    static readonly float[] DisembarkTimes = { .23f, .75f, 1.25f };

    internal HarborScene Scene { get; private set; }
    internal float Elapsed { get; private set; }
    internal bool Opening { get { return Scene < HarborScene.Battle; } }
    internal bool Departing { get { return Scene > HarborScene.Battle && Scene < HarborScene.Complete; } }
    internal bool Playing { get { return Opening || Departing; } }
    internal bool Ready {
        get {
            if (!Playing || Elapsed + .0001f < SceneLength) return false;
            if (Scene == HarborScene.QuayWork)
                return !quayTraffic.Where((truck, i) => truck.Path.Count != 0 ||
                    Campaign.Dist(truck.P, QuayTruckBays[i]) > 1).Any();
            if (Scene == HarborScene.Checkpoint)
                return TruckDetained && sentries[0].Path.Count == 0 && sentries[1].Path.Count == 0;
            if (Scene == HarborScene.LandParty)
                return landingParty.All(unit => unit.Carrier == null && unit.Path.Count == 0);
            if (Scene == HarborScene.Muster)
                return musterOrdered && (departureColumn.All(unit => unit.Path.Count == 0) || Elapsed >= 24);
            if (Scene == HarborScene.Dispatch) {
                var lead = DispatchLead;
                return lead == null || lead.P.X >= RiverbendMap.HarborGateCenter.X + 1.5f * Campaign.Tile || Elapsed >= 9;
            }
            return true;
        }
    }

    internal float SceneLength {
        get {
            switch (Scene) {
                case HarborScene.SupplyRoad: return 3.2f;
                case HarborScene.QuayWork: return 6.4f;
                case HarborScene.Blackout: return 4.8f;
                case HarborScene.Checkpoint: return 7.35f;
                case HarborScene.ApproachShore: return 7.3f;
                case HarborScene.LowerRamp: return 2.15f;
                case HarborScene.LandParty: return 5f;
                case HarborScene.ReopenGate: return 5.7f;
                case HarborScene.ReturnShip: return 8.6f;
                case HarborScene.Muster: return 4.3f;
                case HarborScene.Dispatch: return 4.1f;
                default: return 0;
            }
        }
    }

    internal bool PortOnline { get; private set; }
    internal bool GateBlocked { get; private set; }
    internal bool TruckDetained { get; private set; }
    internal float GateLift { get; private set; }
    internal float CraneCycle { get; private set; }
    internal int CargoOnQuay { get; private set; }
    internal PointF CargoShip { get; private set; }
    internal bool CargoOutside { get { return CargoShip.Y <= cargoOutsideY; } }
    internal PointF LandingCraft { get { return landingDeck.P; } }
    internal PointF LandingHull { get { return new PointF(LandingCraft.X, LandingCraft.Y - Campaign.Tile * .5f); } }
    internal float RampOpen { get; private set; }
    internal float Darkness { get; private set; }
    internal float FogBlend { get; private set; }
    internal float CargoWake { get; private set; }
    internal float LandingWake { get; private set; }
    internal Piece RoadVehicle { get; private set; }
    internal Piece DepartureBase { get; private set; }
    internal PointF DepartureBaseSite { get; private set; }
    internal IEnumerable<Piece> DepartureColumn { get { return departureColumn; } }
    internal Piece DispatchLead {
        get { return departureColumn.Contains(DepartureBase) ? DepartureBase : departureColumn.FirstOrDefault(); }
    }
    internal Piece CommandVehicle { get { return landingParty[0]; } }
    internal RectangleF[] Barriers { get; private set; }
    internal bool BrakeLights { get { return TruckDetained || RoadVehicle != null && RoadSpeed(RoadVehicle) < .91f; } }
    internal float RoadSpeed(Piece truck) {
        if (truck != RoadVehicle) return 1.24f;
        if (Scene != HarborScene.Checkpoint) return 1;
        float distance = Campaign.Dist(truck.P, truck.Goal);
        return Math.Max(.30f, Math.Min(1, distance / (Campaign.Tile * .75f)));
    }

    readonly Campaign battle;
    readonly Piece[] landingParty, sentries, throughTraffic, quayTraffic;
    readonly PointF[] landingOrders;
    readonly Piece landingDeck;
    readonly List<Piece> departureColumn = new List<Piece>();
    readonly float[] walkedOnDeck = new float[3];
    readonly float cargoOutsideY;
    bool quayGuidePosted, musterOrdered, fogPrepared;
    float cargoSailingTime, unloadingTime, columnSpeed;
    float stoppedAt = -1, signalAt = -1;

    internal HarborOperation(Campaign battle, Piece[] landingParty, Piece[] sentries) {
        this.battle = battle;
        this.landingParty = landingParty;
        this.sentries = sentries;
        PortOnline = true;
        GateLift = 1;
        CargoShip = CargoBerth;
        // The map edge is x - y = Width. Leave seven cells of clearance for the entire hull.
        cargoOutsideY = CargoBerth.X - battle.Battlefield.Width - 7 * Campaign.Tile;
        Barriers = RiverbendMap.HarborGatePosts;
        throughTraffic = new[] {
            SendTruck(RiverbendMap.PortPoint(56.4f, 32.9f), RiverbendMap.PortPoint(RiverbendMap.InlandRoadExit + 7, 32.9f), true),
            SendTruck(RiverbendMap.PortPoint(50.2f, 32.9f), RiverbendMap.PortPoint(RiverbendMap.InlandRoadExit, 32.9f), true)
        };
        quayTraffic = new[] { SendQuayTruck(59.7f, QuayTruckBays[0]), SendQuayTruck(65.2f, QuayTruckBays[1]) };
        battle.SetWaypoints(sentries[2], QuayPatrol, true);
        landingDeck = new Piece { T = Campaign.Info("mcv"), Side = 0, P = RiverbendMap.PortPoint(10, 46), Hp = 1 };
        landingOrders = new[] { battle.Home(0), RiverbendMap.PortPoint(18.55f, 45.05f), RiverbendMap.PortPoint(18.7f, 46.75f) };
        for (int i = 0; i < landingParty.Length; i++) {
            var actor = landingParty[i];
            actor.Carrier = landingDeck;
            actor.P = OnDeck(DeckSlots[i]);
            actor.Turn = actor.TurretTurn = actor.Dir = 4;
        }
    }

    internal static float Blend(float a, float b, float t) {
        t = Math.Max(0, Math.Min(1, t));
        t = t * t * (3 - 2 * t);
        return a + (b - a) * t;
    }

    internal static PointF Glide(PointF a, PointF b, float t) {
        return new PointF(Blend(a.X, b.X, t), Blend(a.Y, b.Y, t));
    }

    internal bool Continue() {
        if (!Ready) return false;
        Scene++;
        Elapsed = 0;
        if (Scene == HarborScene.Battle) {
            foreach (var truck in throughTraffic) battle.Units.Remove(truck);
            battle.HandoverHarbor();
        }
        if (Scene == HarborScene.Muster) FindDepartureBase();
        if (Scene == HarborScene.Dispatch) SendDepartureColumn();
        return true;
    }

    internal bool BeginDeparture() {
        if (Scene != HarborScene.Battle || !battle.Won || !battle.OutcomeReady) return false;
        Scene = HarborScene.ReopenGate;
        Elapsed = 0;
        return true;
    }

    internal void Advance(float dt) {
        if (Scene == HarborScene.Complete) {
            if (departureColumn.Any(unit => unit.Path.Count > 0)) {
                battle.TickVisuals(dt);
                MoveDepartureColumn(dt);
            }
            return;
        }
        if (!Playing) return;
        float seconds = dt / MatchTempo.DefaultRate;
        Elapsed += seconds;

        if (Departing) {
            AdvanceDeparture(dt);
            battle.TickVisuals(dt);
            battle.MoveHarborVehicle(RoadVehicle, dt, 1.24f);
            MoveDepartureColumn(dt);
            return;
        }

        switch (Scene) {
            case HarborScene.QuayWork:
                WorkCrane(1.16f, 1);
                AdvanceQuayParking(dt);
                break;
            case HarborScene.Blackout:
                Darkness = Blend(0, .64f, (Elapsed - .24f) / 3.2f);
                if (PortOnline && Elapsed >= 1.18f) {
                    PortOnline = false;
                    RoadVehicle = SendTruck(RiverbendMap.PortPoint(72.95f, 31.25f), RiverbendMap.PortPoint(60.15f, 31.25f), true);
                }
                break;
            case HarborScene.Checkpoint:
                AdvanceCheckpoint();
                break;
            case HarborScene.ApproachShore:
                Darkness = Blend(.64f, .25f, Elapsed / 6.8f);
                float approach = (Elapsed - 4.4f) / 2.8f;
                landingDeck.P = Glide(RiverbendMap.PortPoint(10, 46), Shore, approach);
                LandingWake = Wake(approach);
                break;
            case HarborScene.LowerRamp:
                RampOpen = Blend(0, 1, (Elapsed - .25f) / 1.65f);
                break;
            case HarborScene.LandParty:
                AdvanceLandingLight();
                break;
        }

        if (!PortOnline && !CargoOutside) {
            cargoSailingTime += seconds;
            MoveCargoShip(CargoBerth.Y, cargoOutsideY, cargoSailingTime / 12f);
        }
        battle.Tick(dt);
        if (RampOpen == 1) unloadingTime += seconds;
        PlaceDeckPassengers(dt);
        if (Scene == HarborScene.Checkpoint) SignalTruck(dt);
    }

    Piece SendQuayTruck(float x, PointF bay) {
        var start = RiverbendMap.PortPoint(x, 30.1f);
        return SendTruck(start, new PointF(bay.X, start.Y), false);
    }

    Piece SendTruck(PointF start, PointF destination, bool loaded) {
        var truck = battle.Spawn("miner", -1, start.X, start.Y);
        truck.Mining = false;
        truck.Cargo = loaded ? Campaign.LoadValue : 0;
        truck.Turn = truck.TurretTurn = truck.Dir = destination.X > start.X ? 4 : 12;
        battle.Move(truck, destination, false);
        return truck;
    }

    void AdvanceQuayParking(float dt) {
        var guide = sentries[2];
        if (!quayGuidePosted) {
            battle.GuardAt(guide, QuayGuidePost);
            quayGuidePosted = true;
        }

        Piece arriving = null;
        for (int i = 0; i < quayTraffic.Length; i++) {
            var truck = quayTraffic[i];
            var bay = QuayTruckBays[i];
            if (truck.Path.Count == 0 && truck.Goal != bay) battle.Move(truck, bay, false);
            if (truck.Path.Count > 0 && (arriving == null || Campaign.Dist(guide.P, truck.P) < Campaign.Dist(guide.P, arriving.P)))
                arriving = truck;
        }

        if (arriving == null && !guide.Patrol) battle.SetWaypoints(guide, QuayPatrol, true);
        else if (arriving != null && guide.Path.Count == 0) Campaign.FaceHarborGuard(guide, arriving.P, dt);
    }

    void AdvanceCheckpoint() {
        float lift = 1 - Blend(0, 1, (Elapsed - 1.85f) / 1.37f);
        if (GateLift == 1 && lift < 1)
            battle.GuardAt(sentries[0], RiverbendMap.PortPoint(59.10f, 31.17f));
        GateLift = lift;
        if (GateLift == 0 && !GateBlocked) SetRoadblock(true, false);

        if (!TruckDetained && GateBlocked && RoadVehicle.Path.Count == 0) {
            battle.Halt(RoadVehicle);
            stoppedAt = Elapsed;
            SetRoadblock(true, true);
            battle.GuardAt(sentries[1], new PointF(RoadVehicle.P.X, RoadVehicle.P.Y + Campaign.Tile * .92f));
        }
    }

    void SignalTruck(float dt) {
        if (!GateBlocked || sentries[0].Path.Count != 0) return;
        if (signalAt < 0) signalAt = Elapsed;
        Campaign.FaceHarborGuard(sentries[0], RoadVehicle.P, dt);
        float lowering = stoppedAt < 0 ? 1 : Blend(1, 0, (Elapsed - stoppedAt - .75f) / .7f);
        sentries[0].WeaponAim = Blend(0, .92f, (Elapsed - signalAt) / .38f) * lowering;
        if (TruckDetained && sentries[1].Path.Count == 0)
            Campaign.FaceHarborGuard(sentries[1], RoadVehicle.P, dt);
    }

    void AdvanceLandingLight() {
        Darkness = Blend(.25f, 0, (Elapsed - .55f) / 2.35f);
        if (!fogPrepared && Elapsed >= 2.35f) {
            fogPrepared = true;
            battle.RevealLandingGround();
        }
        FogBlend = Blend(0, 1, (Elapsed - 2.35f) / 2.05f);
    }

    PointF OnDeck(PointF slot) {
        return new PointF(LandingHull.X + slot.X * Campaign.Tile, LandingHull.Y + slot.Y * Campaign.Tile);
    }

    internal bool AboardLandingCraft(Piece actor) { return actor.Carrier == landingDeck; }

    internal float DeckHeight(Piece actor) {
        float x = (actor.P.X - LandingHull.X) / Campaign.Tile;
        return DeckLevel * Math.Max(0, Math.Min(1, (RampTip - x) / (RampTip - RampHinge)));
    }

    PointF DeckPosition(int index, float distance) {
        var slot = DeckSlots[index];
        return OnDeck(new PointF(slot.X + distance / Campaign.Tile, slot.Y));
    }

    void PlaceDeckPassengers(float step) {
        for (int i = 0; i < landingParty.Length; i++) {
            var actor = landingParty[i];
            if (!AboardLandingCraft(actor)) continue;
            actor.P = DeckPosition(i, walkedOnDeck[i]);
            if (unloadingTime < DisembarkTimes[i]) continue;

            // Keep the whole unit pointed down the ramp until its rear clears the lip.
            float rear = actor.T.Foot ? .23f : UnitGroundMetric.LengthCells(actor.T.Key) * .5f + .08f;
            float length = (RampTip + rear - DeckSlots[i].X) * Campaign.Tile;
            float walk = Math.Min(length, walkedOnDeck[i] + step * ReferenceBalance.Speed(actor));
            var next = DeckPosition(i, walk);
            if (i > 0) {
                var ahead = landingParty[i - 1];
                float gap = actor.T.TrafficRadius + ahead.T.TrafficRadius + .24f * Campaign.Tile;
                if (AboardLandingCraft(ahead) && ahead.P.X - next.X < gap) continue;
            }
            bool blocked = false;
            for (int j = 0; j < landingParty.Length; j++) {
                var other = landingParty[j];
                if (j == i || !other.Alive) continue;
                // Campaign.Tick resets embarked pieces to the carrier's pivot.
                var point = AboardLandingCraft(other) ? DeckPosition(j, walkedOnDeck[j]) : other.P;
                if (Campaign.TrafficContact(actor.T, next, other.T, point)) { blocked = true; break; }
            }
            if (blocked) continue;

            actor.P = next;
            if (walk > walkedOnDeck[i]) actor.MovedAt = battle.VisualTime;
            actor.Travel += walk - walkedOnDeck[i];
            walkedOnDeck[i] = walk;
            if (walk < length) continue;
            actor.Carrier = null;
            if (!battle.Move(actor, landingOrders[i], false)) actor.Carrier = landingDeck;
        }
    }

    void SetRoadblock(bool gate, bool detained) {
        if (GateBlocked == gate && TruckDetained == detained) return;
        GateBlocked = gate;
        TruckDetained = detained;
        Barriers = gate ? ClosedGate : RiverbendMap.HarborGatePosts;
        if (detained) {
            float length = Campaign.Tile * UnitGroundMetric.LengthCells("miner");
            float halfWidth = RoadVehicle.T.TrafficRadius;
            var truck = new RectangleF(RoadVehicle.P.X - length / 2, RoadVehicle.P.Y - halfWidth,
                length, halfWidth * 2);
            Barriers = Barriers.Concat(new[] { truck }).ToArray();
        }
        battle.HarborTopologyChanged();
    }

    internal void ClearTruckBarrier(Piece truck) {
        if (truck == RoadVehicle && TruckDetained) SetRoadblock(GateBlocked, false);
    }

    void AdvanceDeparture(float dt) {
        switch (Scene) {
            case HarborScene.ReopenGate:
                FogBlend = Blend(1, 0, Elapsed / 1.85f);
                MoveCargoShip(cargoOutsideY, CargoChannelY, (Elapsed - .15f) / 4.2f);
                GateLift = Blend(0, 1, (Elapsed - 1.05f) / 1.68f);
                if (GateLift == 1 && GateBlocked) {
                    SetRoadblock(false, false);
                    ReleaseRoadTruck();
                }
                break;
            case HarborScene.ReturnShip:
                PortOnline = true;
                MoveCargoShip(CargoChannelY, CargoBerth.Y, (Elapsed - .55f) / 2.85f);
                WorkCrane(3.58f, 2);
                break;
            case HarborScene.Muster:
                if (!musterOrdered && Elapsed >= 2.65f) MusterDepartureColumn(dt);
                break;
        }
    }

    void ReleaseRoadTruck() {
        if (RoadVehicle == null || !RoadVehicle.Deployed) return;
        var gate = RiverbendMap.HarborGateCenter;
        var lanes = new[] { RoadVehicle.P.Y }.Concat(FreightLaneOffsets.Select(offset => gate.Y + offset * Campaign.Tile));
        foreach (float lane in lanes)
            foreach (var bay in ReceivingBays)
                if (battle.TrySetHarborRoute(RoadVehicle, new PointF(RoadVehicle.P.X, lane),
                    new PointF(bay.X, lane), bay)) return;
    }

    static bool IsDepartureBase(Piece unit) {
        return unit.Side == 0 && unit.Alive && !unit.Selling && unit.Carrier == null &&
            (unit.T.Key == "hq" && unit.Ready >= 1 || unit.T.Key == "mcv");
    }

    void FindDepartureBase() {
        DepartureBase = IsDepartureBase(CommandVehicle) ? CommandVehicle : battle.Units.FirstOrDefault(IsDepartureBase);
        DepartureBaseSite = DepartureBase == null ? RiverbendMap.HarborGateCenter : DepartureBase.P;
    }

    void MusterDepartureColumn(float dt) {
        if (DepartureBase != null && DepartureBase.T.Key == "hq") {
            if (DepartureBase.Packing > 0) {
                battle.AdvanceHarborPacking(DepartureBase, dt);
                if (DepartureBase.Packing > 0) return;
            } else if (battle.Relocate(DepartureBase, DepartureBase.P)) return;
            else DepartureBase = null;
        }
        musterOrdered = true;
        var gate = RiverbendMap.HarborGateCenter;
        var slots = new List<PointF>();
        for (float x = DepartureFront; x >= (RiverbendMap.PortRoad.Left + 1) * Campaign.Tile; x -= 1.4f * Campaign.Tile) {
            slots.Add(new PointF(x, gate.Y - Campaign.Tile));
            slots.Add(new PointF(x, gate.Y + Campaign.Tile));
        }
        var survivors = battle.Live(0).Where(unit => !unit.T.Building && unit.T.Key != "miner")
            .OrderBy(unit => unit != DepartureBase).ThenBy(unit => Campaign.Dist(unit.P, gate));
        foreach (var unit in survivors) {
            // The base vehicle uses the far lane so the camera sees it clearly beside the gate.
            float lane = unit == DepartureBase ? gate.Y - Campaign.Tile : unit.P.Y;
            foreach (var slot in slots.OrderByDescending(p => p.X).ThenBy(p => Math.Abs(p.Y - lane)).ToArray()) {
                if (!battle.MoveHarborDeparture(unit, slot)) continue;
                unit.PendingDeploy = false;
                unit.ForceMoving = false;
                departureColumn.Add(unit);
                slots.Remove(slot);
                break;
            }
        }
    }

    void SendDepartureColumn() {
        float exit = RiverbendMap.PortPoint(RiverbendMap.InlandRoadExit + 7, 0).X;
        for (int i = departureColumn.Count - 1; i >= 0; i--) {
            var unit = departureColumn[i];
            var slot = unit.Goal;
            // Keep each row's lead and lane through the gate, then continue well past the camera.
            var end = new PointF(exit - (DepartureFront - slot.X), slot.Y);
            if (!battle.MoveHarborDeparture(unit, end, departureColumn)) departureColumn.RemoveAt(i);
        }
        columnSpeed = ReferenceBalance.Speed(DispatchLead ?? CommandVehicle) * 4.2f;
    }

    void MoveDepartureColumn(float dt) {
        foreach (var unit in departureColumn) {
            if (unit.Path.Count == 0) continue;
            float pace = columnSpeed / ReferenceBalance.Speed(unit);
            if (Scene == HarborScene.Muster) {
                float remaining = Campaign.Dist(unit.P, unit.Goal);
                float ramp = Math.Min(Blend(0, 1, (Elapsed - 3.85f) / 1.25f),
                    Blend(0, 1, (remaining - 1.4f * Campaign.Tile) / (4 * Campaign.Tile)));
                pace = 1 + (unit == DepartureBase ? 3.5f : 2.5f) * ramp;
            }
            battle.MoveHarborVehicle(unit, dt, pace, Scene == HarborScene.Muster);
        }
    }

    void WorkCrane(float start, int load) {
        CraneCycle = Math.Max(0, Math.Min(1, (Elapsed - start) / CraneUnloadTime));
        if (CraneCycle >= CraneRelease) CargoOnQuay = load;
    }

    void MoveCargoShip(float fromY, float toY, float t) {
        CargoShip = new PointF(CargoBerth.X, Blend(fromY, toY, t));
        CargoWake = Wake(t);
    }

    static float Wake(float t) {
        t = Math.Max(0, Math.Min(1, t));
        return 4 * t * (1 - t);
    }
}

public partial class Campaign {
    internal HarborOperation HarborOperation { get; private set; }
    internal bool FirstChapter { get { return River != null && River.IsChapterOne; } }
    internal static Campaign OpenGreyhaven() { return new Campaign(true, true, false, false, false, true); }

    void SetupFirstChapter() {
        Ore.Clear();
        foreach (var cell in River.Ore) {
            var point = RiverbendMap.PortPoint(cell.X, cell.Y);
            Ore.Add(new OrePatch(point.X, point.Y, 2400));
        }
        var landing = new[] {
            SpawnAtPort("mcv", 0, 17, 45),
            SpawnAtPort("squad", 0, 19, 43),
            SpawnAtPort("squad", 0, 21, 45)
        };
        var sentries = new[] {
            SpawnAtPort("squad", 1, 60.25f, 28.45f),
            SpawnAtPort("squad", 1, 62.05f, 33.9f),
            SpawnAtPort("squad", 1, 49.6f, 26.2f)
        };
        foreach (var unit in sentries) Guard(unit);
        SetWaypoints(sentries[0], new[] { RiverbendMap.PortPoint(62.5f, 28.3f), RiverbendMap.PortPoint(59.6f, 28.7f) }, true);
        HarborOperation = new HarborOperation(this, landing, sentries);
        Radio = "登陆分队仍在艇上。";
    }

    Piece SpawnAtPort(string key, int side, float x, float y) {
        var point = RiverbendMap.PortPoint(x, y);
        return Spawn(key, side, point.X, point.Y);
    }

    internal bool TrySetHarborRoute(Piece unit, params PointF[] waypoints) {
        float pad = Math.Max(Clearance(unit), unit.T.TrafficRadius);
        var obstacles = Grid(pad).Barriers;
        var traffic = MineTraffic(unit, waypoints.Last());
        if (traffic != null) obstacles = obstacles.Concat(traffic).ToArray();
        var path = new Queue<PointF>();
        var from = unit.P;
        foreach (var target in waypoints) {
            if (Dist(from, target) < .001f) continue;
            if (unit.T.Foot) {
                if (!Clear(from, target, obstacles, pad)) return false;
                path.Enqueue(target);
            } else if (!TrackLegs(from, target, obstacles, path, pad)) return false;
            from = target;
        }
        if (path.Count == 0) return false;
        Halt(unit);
        unit.Path = path;
        unit.RouteVersion = topology;
        unit.Goal = from;
        return true;
    }

    internal bool MoveHarborDeparture(Piece unit, PointF destination, ICollection<Piece> travelingTogether = null) {
        // Most battle traffic is standing still during the closing shot, even if its old order remains queued.
        var traffic = MineTraffic(unit, destination, other => travelingTogether == null || !travelingTogether.Contains(other));
        if (traffic != null && traffic.Any(block => block.Contains(destination))) return false;
        if (!Move(unit, destination, false, traffic, unit.T.TrafficRadius)) return false;
        unit.TrafficBlocked = unit.TrafficWait = unit.Replan = 0;
        return true;
    }

    internal void MoveHarborVehicle(Piece vehicle, float dt, float speedScale = 1, bool replanTraffic = false) {
        if (vehicle == null || !vehicle.Deployed || vehicle.Path.Count == 0) return;
        var before = vehicle.P;
        if (replanTraffic) {
            vehicle.Replan -= dt;
            if (vehicle.TrafficBlocked >= .35f && vehicle.Replan <= 0) {
                MoveHarborDeparture(vehicle, vehicle.Goal);
                vehicle.Replan = .7f;
            }
        }
        TickWeaponAim(vehicle, dt);
        TickInfantryStance(vehicle, dt);
        vehicle.TrafficWait -= dt;
        TickUnitMovement(vehicle, dt, speedScale);
        float travel = Dist(before, vehicle.P);
        vehicle.Travel += travel;
        if (travel > .001f) vehicle.MovedAt = VisualTime;
    }

    internal void AdvanceHarborPacking(Piece baseUnit, float dt) {
        baseUnit.Relocation = null;
        TickPacking(baseUnit, dt);
    }

    internal static void FaceHarborGuard(Piece guard, PointF point, float dt) { Face(guard, point, dt); }

    internal void HarborTopologyChanged() { Changed(); }

    internal void RevealLandingGround() {
        var area = RectangleF.FromLTRB(14 * Tile, 29 * Tile, 38 * Tile, 49 * Tile);
        for (int y = FogRow(area.Top); y <= FogRow(area.Bottom); y++)
            for (int x = FogColumn(area.Left); x <= FogColumn(area.Right); x++)
                if (x >= 0 && y >= 0 && x < MapFogCols && y < MapFogRows)
                    Discover(0, x, y);
        Reveal();
    }

    internal void HandoverHarbor() {
        Reveal();
        Radio = "许岑：先展开工程车。站稳之后，我们沿公路去旧码头。";
    }
}
}
