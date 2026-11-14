using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;

static class ChapterOneChecks {
    [STAThread]
    static int Main(string[] args) {
        try {
            using (var window = new CommandWindow()) window.CheckFirstChapter(args.Length > 0 ? args[0] : null);
            Console.WriteLine("First chapter: opening, battle, automatic harbor departure and next-mission transition passed.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}

namespace Greyhaven {
partial class CommandWindow {
    static void ExpectChapter(bool condition, string reason) {
        if (!condition) throw new InvalidOperationException(reason);
    }

    void WaitForChapter(Func<bool> done, float limit, string message) {
        for (int i = 0; i < limit / MatchTempo.Step && !done(); i++)
            world.Tick((float)MatchTempo.Step);
        ExpectChapter(done(), message);
    }

    void BuildChapterFacility(string key) {
        var type = Campaign.Info(key);
        ExpectChapter(world.StartConstruction(type), "begin " + key + ": " + world.Radio);
        WaitForChapter(() => world.Construction(type).Ready, type.Seconds * 2 + 8, "prepare " + key);
        var point = SuggestSpot(type);
        ExpectChapter(!point.IsEmpty && world.Build(type, point), "place " + key + " at tutorial marker " + point);
        WaitForChapter(() => world.Live(0).Any(p => p.T == type), 8, "complete " + key);
    }

    static void CheckHarborCoats() {
        foreach (var sprite in new[] { HarborSprites.Landing, HarborSprites.Cover, HarborSprites.Supply, HarborSprites.Office }) {
            string key = sprite.Frame(0);
            var atlas = HarborSprites.Atlas.Value;
            using (var body = atlas.Read(key))
            using (var mask = atlas.Read(key + "-paint"))
            using (var blue = BuildingTint(body, mask, sprite.Source, Color.FromArgb(61, 130, 184)))
            using (var orange = BuildingTint(body, mask, sprite.Source, Color.FromArgb(214, 131, 45))) {
                int changed = 0, neutral = 0;
                for (int y = 0; y < body.Height; y += 3) for (int x = 0; x < body.Width; x += 3) {
                    var a = blue.GetPixel(x, y);
                    var b = orange.GetPixel(x, y);
                    var paint = mask.GetPixel(x, y);
                    ExpectChapter(a.A == b.A, "faction tint must preserve harbor alpha");
                    if (a.A < 220) continue;
                    if (a.ToArgb() != b.ToArgb()) changed++;
                    else if (paint.A == 0) neutral++;
                }
                ExpectChapter(changed > 60 && neutral > 60, "harbor art needs independent tinted and neutral areas");
            }
        }
    }

    static void CheckHarborDepartureDetour() {
        var port = Campaign.OpenGreyhaven();
        var start = RiverbendMap.PortPoint(36, 31.5f);
        var goal = RiverbendMap.PortPoint(55, 31.5f);
        var buildingPoint = RiverbendMap.PortPoint(43, 31.5f);
        var building = port.Spawn("power", 0, buildingPoint.X, buildingPoint.Y);
        var baseCar = port.Spawn("mcv", 0, start.X, start.Y);
        ExpectChapter(port.Route(baseCar, goal), "a route past the building exists");
        var staticRoute = new[] { start }.Concat(baseCar.Path).ToArray();
        var longestLeg = Enumerable.Range(1, staticRoute.Length - 1)
            .OrderByDescending(i => Campaign.Dist(staticRoute[i - 1], staticRoute[i])).First();
        var parkedAt = HarborOperation.Glide(staticRoute[longestLeg - 1], staticRoute[longestLeg], .5f);
        var parked = port.Spawn("miner", 0, parkedAt.X, parkedAt.Y);
        parked.Path.Enqueue(start); // This old order does not run during the closing shot.
        ExpectChapter(port.MoveHarborDeparture(baseCar, goal), "the departure route plans around the stopped vehicle");
        var foundation = Campaign.Footprint(building.T, building.P);
        foundation.Inflate(baseCar.T.TrafficRadius, baseCar.T.TrafficRadius);
        for (int i = 0; i < 2300 && baseCar.Path.Count > 0; i++) {
            port.MoveHarborVehicle(baseCar, (float)MatchTempo.Step, 1, true);
            ExpectChapter(!foundation.Contains(baseCar.P) &&
                !Campaign.TrafficContact(baseCar.T, baseCar.P, parked.T, parked.P) && baseCar.TrafficBlocked < .10f,
                "the base vehicle should clear the building and parked traffic without braking against either");
        }
        ExpectChapter(Campaign.Dist(baseCar.P, goal) < 1, "the detour reaches the real rally point");
    }

    void CheckStoppedTruck(Piece truck) {
        var start = RiverbendMap.PortPoint(63.55f, 31.25f);
        var end = RiverbendMap.PortPoint(58.85f, 30.35f);
        var tank = world.Spawn("tank", 0, start.X, start.Y);
        try {
            ExpectChapter(!world.CanMoveTo(tank, truck.P) && !world.Move(tank, truck.P, false),
                "a parked truck must block a movement order to its own position");
            ExpectChapter(world.Move(tank, end, false), "a vehicle must still be able to route around the truck");
            for (int step = 0; step < 2700 && tank.Path.Count > 0; step++) {
                world.MoveHarborVehicle(tank, (float)MatchTempo.Step);
                ExpectChapter(!Campaign.TrafficContact(tank.T, tank.P, truck.T, truck.P),
                    "the detour must not drive through the detained truck");
            }
            ExpectChapter(Campaign.Dist(tank.P, end) < 2, "the truck detour did not reach its destination");
        } finally {
            world.Units.Remove(tank);
        }
    }

    void TrySkipChapter() {
        OnKeyDown(new KeyEventArgs(Keys.Enter));
        OnKeyUp(new KeyEventArgs(Keys.Enter));
        OnKeyDown(new KeyEventArgs(Keys.D));
        OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, W / 2, H / 2, 120));
        OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, W / 2, H / 2, 0));
    }

    void NextChapterShot() {
        OnKeyDown(new KeyEventArgs(Keys.Enter));
        OnKeyDown(new KeyEventArgs(Keys.Enter));
        OnKeyUp(new KeyEventArgs(Keys.Enter));
    }

    void PressChapterEscape() {
        OnKeyDown(new KeyEventArgs(Keys.Escape));
        OnKeyUp(new KeyEventArgs(Keys.Escape));
    }

    internal void CheckFirstChapter(string frames) {
        pulse.Stop();
        CheckHarborCoats();
        CheckHarborDepartureDetour();
        ClientSize = new Size(W, H);
        if (frames != null) Directory.CreateDirectory(frames);
        using (var frame = new Bitmap(W, H))
        using (var graphics = Graphics.FromImage(frame)) {
            Action<string> capture = name => {
                hudDirty = true;
                OnPaint(new PaintEventArgs(graphics, ClientRectangle));
                if (frames != null) frame.Save(Path.Combine(frames, name + ".png"), ImageFormat.Png);
            };

            OpenMissions();
            OpenMapReview();
            ExpectChapter(riverMap.IsChapterOne && riverMap.WidthCells == 88 && riverMap.HeightCells == 58,
                "chapter-one menu uses the new map");
            capture("00-map");
            CloseMapReview();
            LaunchSelectedMission();
            OnKeyDown(new KeyEventArgs(Keys.Escape));
            OnKeyDown(new KeyEventArgs(Keys.Escape));
            ExpectChapter(paused && !briefing, "holding Escape must pause only once");
            capture("opening-paused");
            ExpectChapter(hits.Count == 2 && hits.Any(hit => hit.Id == "继续演出") && hits.Any(hit => hit.Id == "返回选关"),
                "the paused opening offers resume and mission selection only");
            OnKeyUp(new KeyEventArgs(Keys.Escape));
            NextChapterShot();
            ExpectChapter(!paused && world.HarborOperation.Scene == HarborScene.SupplyRoad,
                "Enter resumes the same shot");
            PressChapterEscape();
            PressChapterEscape();
            ExpectChapter(briefing && missionPicker && !paused, "a fresh Escape from pause returns to mission selection");
            PressChapterEscape();
            ExpectChapter(briefing && !missionPicker, "Escape from mission selection returns to title");
            OpenMissions();
            LaunchSelectedMission();
            var campaign = world;
            var operation = world.HarborOperation;
            var engineer = operation.CommandVehicle;
            var aboard = world.Units.Where(o => o.Side == 0).ToArray();
            var dayTraffic = world.Units.Where(o => o.Side < 0 && o.T.Key == "miner").ToArray();
            var dayStarts = dayTraffic.Select(o => o.P).ToArray();
            var quayTrucks = dayTraffic.Where(o => o.Cargo == 0).ToArray();
            var throughTrucks = dayTraffic.Where(o => o.Cargo > 0).ToArray();
            ExpectChapter(dayTraffic.Length == 4 && operation.RoadVehicle == null,
                "the daytime traffic starts without the night truck");
            Piece roadTruck = null;
            var truckStart = PointF.Empty;
            var guard = world.Live(1).First();
            var guardStart = guard.P;
            var dockPatrol = world.Live(1).First(o => o.Patrol && o.P.X < RiverbendMap.HarborBarrier.Left);
            bool sawStopSignal = false, sawBrake = false, sawVehicleOnRamp = false;
            int embarkedAtPreviousStep = aboard.Length;
            float[] shotTimes = { 1.75f, 3.55f, 2.5f, 5.05f, 5.9f, 1.3f, 2.82f };
            const double frameTime = MatchTempo.Step / MatchTempo.DefaultRate;
            double openingSeconds = 0;

            for (int page = 0; page < (int)HarborScene.Battle; page++) {
                capture("opening-" + page + "-start");
                var startCamera = camera;
                TrySkipChapter();
                ExpectChapter((int)operation.Scene == page && !world.Landing && !paused && camera == startCamera,
                    "opening cannot be skipped, deployed or panned early");
                bool sampled = false;
                for (int step = 0; step < 1700 && !operation.Ready; step++) {
                    var beforeCamera = camera;
                    var beforeActors = aboard.Select(o => o.P).ToArray();
                    var beforeFreight = quayTrucks.Select(o => o.P).ToArray();
                    AdvanceChapterScene(frameTime, true);
                    openingSeconds += frameTime;
                    ExpectChapter(Campaign.Dist(beforeCamera, camera) < 12, "the camera must slide between world landmarks");
                    int stillAboard = aboard.Count(o => o.Carrier != null);
                    ExpectChapter(aboard.Take(aboard.Length - stillAboard).All(o => o.Carrier == null),
                        "the command vehicle leads, then the two riflemen follow one at a time");
                    for (int i = 0; i < quayTrucks.Length; i++)
                        ExpectChapter(Campaign.Dist(beforeFreight[i], quayTrucks[i].P) < 1,
                            "the trucks must drive into their bays without teleporting between shots");
                    if (roadTruck == null && operation.RoadVehicle != null) {
                        roadTruck = operation.RoadVehicle;
                        truckStart = roadTruck.P;
                        ExpectChapter(operation.Scene == HarborScene.Blackout && !operation.PortOnline &&
                            !dayTraffic.Contains(roadTruck) && roadTruck.Path.Count > 0,
                            "a separate night truck drives in after the blackout");
                    }
                    for (int i = 0; i < aboard.Length; i++)
                        for (int j = 0; j < i; j++)
                            ExpectChapter(!Campaign.TrafficContact(aboard[i].T, aboard[i].P, aboard[j].T, aboard[j].P),
                                "passengers must not overlap aboard or during disembarkation");
                    if (operation.RampOpen == 1)
                        for (int i = 0; i < aboard.Length; i++)
                            ExpectChapter(Campaign.Dist(beforeActors[i], aboard[i].P) < 2,
                                "the landing party must move continuously from deck to ramp to land");
                    if (operation.RampOpen < 1)
                        ExpectChapter(stillAboard == aboard.Length, "passengers cannot leave before the ramp finishes");
                    ExpectChapter(embarkedAtPreviousStep - stillAboard <= 1, "passengers must disembark one at a time");
                    embarkedAtPreviousStep = stillAboard;
                    sawStopSignal |= guard.WeaponAim > .2f;
                    sawBrake |= operation.BrakeLights;
                    if (!sawVehicleOnRamp && operation.Scene == HarborScene.LandParty &&
                        operation.AboardLandingCraft(engineer) && operation.DeckHeight(engineer) < .29f) {
                        ExpectChapter(world.WaterAt(new PointF(operation.LandingHull.X + 2.25f * Campaign.Tile, operation.LandingHull.Y)) &&
                            !world.WaterAt(new PointF(operation.LandingHull.X + 3.12f * Campaign.Tile, operation.LandingHull.Y)),
                            "the bow floats while the ramp reaches dry ground");
                        capture("opening-6-ramp");
                        sawVehicleOnRamp = true;
                    }
                    if (!sampled && operation.Elapsed >= shotTimes[page]) {
                        capture("opening-" + page + "-scene");
                        sampled = true;
                    }
                }
                ExpectChapter(operation.Ready, "opening shot " + page + " did not finish");
                if (operation.Scene == HarborScene.SupplyRoad) {
                    float gate = RiverbendMap.HarborBarrier.Left;
                    ExpectChapter(dayTraffic.All(truck => truck.Travel > 40) &&
                        dayTraffic.Where((truck, i) => dayStarts[i].X < gate && truck.P.X > gate).Any() &&
                        dayTraffic.Where((truck, i) => dayStarts[i].X > gate && truck.P.X < gate).Any(),
                        "daytime traffic moves through the open gate in both directions");
                }
                if (operation.Scene == HarborScene.QuayWork)
                    ExpectChapter(quayTrucks.Length == HarborOperation.QuayTruckBays.Length &&
                        quayTrucks.Where((truck, i) => truck.Path.Count == 0 &&
                            Campaign.Dist(truck.P, HarborOperation.QuayTruckBays[i]) < 1).Count() == quayTrucks.Length,
                        "both day trucks visibly finish parking in marked bays before the scene changes");
                float sceneEnd = world.Time;
                float visualEnd = world.VisualTime;
                float patrolTravel = dockPatrol.Travel;
                var shipAtCaption = operation.CargoShip;
                capture("opening-" + page + "-ready");
                var heldCamera = camera;
                float heldDark = operation.Darkness, heldFog = operation.FogBlend;
                if (operation.Scene == HarborScene.LandParty) {
                    ExpectChapter(hits.Count == 1 && hits[0].Id == "接管先遣队", "the player confirms the handoff");
                    var ramp = new PointF(operation.LandingHull.X + 3.12f * Campaign.Tile, operation.LandingHull.Y);
                    ExpectChapter(operation.Elapsed <= 6.5f && aboard.Where(o => o.T.Foot).All(o =>
                            Campaign.Dist(o.P, ramp) < 3 * Campaign.Tile && !world.WaterAt(o.P)) && world.CanDeploy(engineer),
                        "the landing ends promptly with the riflemen near the ramp and a deployable command vehicle");
                    Console.WriteLine("Landing handoff after {0:F1} seconds in the last shot.", operation.Elapsed);
                    AdvanceChapterScene(.12, true);
                    ExpectChapter(operation.Scene == HarborScene.LandParty, "the ready handoff waits for the player");
                    var handoff = hits[0].Box;
                    ExpectChapter(handoff.Y > H - 126 && handoff.Right < W - 30, "handoff button is inside the bottom caption");
                    var click = new MouseEventArgs(MouseButtons.Left, 1, (int)(ox + (handoff.X + handoff.Width / 2) * scale),
                        (int)(oy + (handoff.Y + handoff.Height / 2) * scale), 0);
                    OnMouseDown(click);
                    OnMouseUp(click);
                } else {
                    ExpectChapter(hits.Count == 0, "automatic shots have no next button");
                    NextChapterShot();
                    ExpectChapter((int)operation.Scene == page, "Enter cannot bypass the caption hold");
                    if (operation.Scene == HarborScene.LowerRamp) {
                        PressChapterEscape();
                        for (int tick = 0; tick < 12; tick++) AdvanceChapterScene(.1, true);
                        ExpectChapter(operation.Scene == HarborScene.LowerRamp && paused,
                            "a paused caption must not time out");
                        NextChapterShot();
                        ExpectChapter(operation.Scene == HarborScene.LowerRamp && !paused, "resume leaves the ready shot in place");
                    }
                    double readSeconds = Math.Max(3, .95 + PrologueText[ProloguePages[page]].Length / 8.5);
                    double holdLimit = Math.Max(2, readSeconds - chapterCaptionSeconds + .25);
                    double hold = 0;
                    while ((int)operation.Scene == page && hold < holdLimit) {
                        AdvanceChapterScene(frameTime, true);
                        hold += frameTime;
                    }
                    bool merged = page == (int)HarborScene.SupplyRoad || page == (int)HarborScene.ApproachShore;
                    ExpectChapter(merged ? hold <= 2 * frameTime : hold >= .79,
                        "shared captions run continuously; a caption change leaves time to finish reading");
                    openingSeconds += hold;
                }
                ExpectChapter((int)operation.Scene == page + 1, "opening must advance through each shot in order");
                ExpectChapter(world.Time > sceneEnd && world.VisualTime > visualEnd && camera == heldCamera &&
                    heldDark == operation.Darkness && heldFog == operation.FogBlend,
                    "caption holds keep time running without a jump in camera, lighting or fog");
                if (page == (int)HarborScene.QuayWork || page == (int)HarborScene.Blackout)
                    ExpectChapter(dockPatrol.Travel > patrolTravel + 3, "the dock patrol keeps walking while its caption is held");
                if (page == (int)HarborScene.Blackout)
                    ExpectChapter(operation.CargoShip.Y < shipAtCaption.Y - 12, "the ship keeps sailing while its caption is held");
            }
            Console.WriteLine("Opening reaches player handoff after {0:F1} seconds.", openingSeconds);

            ExpectChapter(sawVehicleOnRamp && object.ReferenceEquals(campaign, world) && world.Units.Contains(engineer) &&
                world.Units.Contains(guard) && operation.Scene == HarborScene.Battle &&
                Campaign.Dist(engineer.P, world.Home(0)) < 2 && Campaign.Dist(guard.P, guardStart) > 20 &&
                engineer.Path.Count == 0 && aboard.All(o => o.Carrier == null),
                "preserve the real ground party at handoff; engineer at " + engineer.P + ", target " + world.Home(0));
            ExpectChapter(!operation.PortOnline && operation.GateBlocked && operation.GateLift == 0 &&
                operation.CargoOnQuay == 1 && operation.RampOpen == 1 && operation.FogBlend == 1 && operation.Darkness == 0,
                "blackout, gate, cargo and completed landing must survive the last shot");
            ExpectChapter(sawStopSignal && sawBrake && world.Units.Contains(roadTruck) && roadTruck.Side < 0 &&
                throughTrucks.All(truck => !world.Units.Contains(truck)) && operation.TruckDetained &&
                roadTruck.Path.Count == 0 && Campaign.Dist(roadTruck.P, truckStart) > 90 &&
                Campaign.Dist(roadTruck.P, RiverbendMap.PortPoint(60.15f, 31.25f)) < 2,
                "the distinct night truck is detained at the gate while through traffic has left");
            ExpectChapter(quayTrucks.Where((truck, i) => world.Units.Contains(truck) &&
                Campaign.Dist(truck.P, HarborOperation.QuayTruckBays[i]) < 1).Count() == quayTrucks.Length,
                "the same two parked trucks persist in the battle at the quay");
            ExpectChapter(operation.CargoOutside &&
                !world.Battlefield.Contains(new PointF(operation.CargoShip.X, operation.CargoShip.Y + 5 * Campaign.Tile)),
                "the supply ship and its stern sail beyond the playable map");
            ExpectChapter(world.Terrain.At(13, 46).Ground == RiverbendMap.Surface.Water &&
                world.Terrain.At(17, 46).Ground == RiverbendMap.Surface.Ramp &&
                world.CanDeploy(engineer) && world.Explored(0, RiverbendMap.PortPoint(34, 37)) &&
                !world.Explored(0, world.Harbor.P) && !world.Visible(guard),
                "initial vision and deployment use the new terrain");
            CheckStoppedTruck(roadTruck);
            capture("05-command");

            OnKeyDown(new KeyEventArgs(Keys.D));
            WaitForChapter(() => world.Lesson == 1, 12, "deploy by the regular keyboard control");
            BuildChapterFacility("power");
            BuildChapterFacility("refinery");
            WaitForChapter(() => world.OreReceipts[0] > 0, 160, "starter miner did not deliver a real load");
            capture("06-first-ore");
            BuildChapterFacility("barracks");
            ExpectChapter(world.Train(Campaign.Info("squad"), 0, null), "queue a rifleman");
            WaitForChapter(() => world.InfantryDelivered > 0, 30, "rifleman did not leave the barracks");
            BuildChapterFacility("factory");
            ExpectChapter(world.Train(Campaign.Info("ifv"), 0, null), "queue the IFV");
            WaitForChapter(() => world.LessonKey == "eliminate", 65, "IFV did not finish the lesson");
            var troops = world.Live(0).Where(u => world.Armed(u) && !u.T.Building).ToArray();
            ExpectChapter(world.MarchTo(troops, RiverbendMap.PortPoint(56.3f, 30.3f), true, true) > 0,
                "assault route along the port road");
            WaitForChapter(() => world.Won, 250, "the tutorial force could not clear the port");
            ExpectChapter(!operation.BeginDeparture(), "wait for the result transition");
            PressChapterEscape();
            capture("victory-paused");
            ExpectChapter(paused && hits.Any(hit => hit.Id == "继续演出"), "Escape also pauses the pre-departure hold");
            NextChapterShot();
            capture("victory-hold");
            ExpectChapter(!paused && hits.Count == 0, "the result cannot bypass the mandatory departure");
            world.TickVisuals(Campaign.BuildingDestructionSeconds + .3f);
            var survivors = world.Live(0).ToDictionary(unit => unit, unit => unit.Travel);
            BeginHarborDeparture();
            ExpectChapter(operation.Scene == HarborScene.ReopenGate, "port handover runs automatically");

            var stoppedTruck = roadTruck.P;
            var gateCenter = RiverbendMap.HarborGateCenter;
            bool truckPassedGate = false;
            bool sawBasePacking = false;
            Action advanceDepartureFrame = () => {
                var shot = operation.Scene;
                var lastTruckPosition = roadTruck.P;
                var lastCamera = camera;
                AdvanceChapterScene(frameTime, true);
                ExpectChapter(Campaign.Dist(lastCamera, camera) < 12, "the departure camera must glide between shots");
                ExpectChapter(operation.GateLift == 1 || roadTruck.P == stoppedTruck, "the truck waits until the arms are raised");
                sawBasePacking |= operation.Scene == HarborScene.Muster && engineer.T.Key == "hq" && engineer.Packing > 0;
                foreach (var actor in new[] { roadTruck }.Concat(operation.DepartureColumn)) {
                    foreach (var post in RiverbendMap.HarborGatePosts) {
                        var blocked = post;
                        blocked.Inflate(actor.T.TrafficRadius, actor.T.TrafficRadius);
                        ExpectChapter(!blocked.Contains(actor.P), "vehicles must keep clear of both gate posts");
                    }
                    if ((actor == roadTruck || shot == HarborScene.Dispatch) && Math.Abs(actor.P.X - gateCenter.X) < .5f * Campaign.Tile)
                        ExpectChapter(Math.Abs(actor.P.Y - gateCenter.Y) < 2.1f * Campaign.Tile, "actors cross in the open lanes");
                }
                truckPassedGate |= lastTruckPosition.X >= gateCenter.X && roadTruck.P.X < gateCenter.X;
            };
            float[] departureTimes = { 4.7f, 5.8f, 3.1f, 3.3f };
            for (int page = 0; page < departureTimes.Length; page++) {
                capture("departure-" + page + "-start");
                var shot = operation.Scene;
                TrySkipChapter();
                ExpectChapter(operation.Scene == shot, "departure cannot skip");
                bool sampled = false;
                for (int step = 0; step < 1700 && !operation.Ready; step++) {
                    advanceDepartureFrame();
                    if (!sampled && operation.Elapsed >= departureTimes[page]) {
                        capture("departure-" + page + "-scene");
                        sampled = true;
                    }
                }
                ExpectChapter(operation.Ready, "departure shot did not finish");
                if (shot == HarborScene.Muster) {
                    ExpectChapter(sawBasePacking && operation.DepartureBase == engineer && engineer.T.Key == "mcv",
                        "the player's actual construction yard must animate into a mobile base");
                    ExpectChapter(operation.DepartureColumn.Count() >= 2 && operation.DepartureColumn.All(unit =>
                        unit.Path.Count == 0 && Campaign.Dist(unit.P, gateCenter) < 8 * Campaign.Tile),
                        "the mobile base and escorts must assemble together at the gate before leaving: " +
                        string.Join(", ", operation.DepartureColumn.Select(u => u.T.Key + " at " + u.P + " with " + u.Path.Count + " waypoints")));
                }
                capture("departure-" + page + "-ready");
                NextChapterShot();
                ExpectChapter(hits.Count == 0 && operation.Scene == shot, "the outro has no advance button or Enter shortcut");
                double hold = 0;
                while (operation.Scene == shot && hold < 3) {
                    advanceDepartureFrame();
                    hold += frameTime;
                }
                ExpectChapter((int)operation.Scene == (int)shot + 1 && (page >= 2 || hold >= .79),
                    "departure captions advance automatically without delaying the departing column");
            }
            ExpectChapter(operation.Scene == HarborScene.Complete && operation.PortOnline && !operation.GateBlocked &&
                operation.CargoShip == HarborOperation.CargoBerth &&
                operation.CargoOnQuay == 2 && object.ReferenceEquals(campaign, world) &&
                world.Units.Contains(roadTruck) && truckPassedGate && roadTruck.Path.Count == 0 &&
                roadTruck.P.Y < gateCenter.Y - 3 * Campaign.Tile, "the same cargo truck clears the gate and parks at the quay: " +
                roadTruck.P + ", remaining waypoints " + roadTruck.Path.Count);
            var column = operation.DepartureColumn.ToArray();
            ExpectChapter(column.Contains(engineer) && column.Any(unit => unit.T.Foot) &&
                column.Any(unit => unit.T.Key == "ifv") && column.All(unit => survivors.ContainsKey(unit) &&
                world.Units.Contains(unit) && unit.Travel - survivors[unit] > 24 && unit.Path.Count > 0) &&
                engineer.P.X > gateCenter.X + Campaign.Tile,
                "the result appears while the real base vehicle and combat escorts are still advancing");
            capture("10-result");
            var duringResult = column.Select(unit => unit.P.X).ToArray();
            var marcher = column.First(unit => unit.T.Foot);
            var marchFrames = new HashSet<int>();
            float battleTimeAtResult = world.Time;
            using (var sprite = new Bitmap(96, 96))
            using (var spriteGraphics = Graphics.FromImage(sprite)) {
                for (int i = 0; i < 60; i++) {
                    AdvanceChapterScene(frameTime, true);
                    if (i % 3 != 0) continue;
                    DrawFootGait(spriteGraphics, marcher, new PointF(48, 72), marcher.T.Size);
                    FootMotion motion;
                    if (footMotion.TryGetValue(marcher.Id, out motion) && motion.Amount > .85f && !CombatPose(marcher))
                        marchFrames.Add(motion.Frame);
                }
            }
            ExpectChapter(world.Time == battleTimeAtResult && marchFrames.Count >= 3,
                "marching infantry must cycle real walk frames while the battle clock is frozen");
            ExpectChapter(column.Select((unit, i) => unit.P.X - duringResult[i]).All(distance => distance > 8),
                "the entire column must keep moving under the result panel");
            capture("10-result-moving");
            for (int i = 0; i < 3000 && column.Any(unit => unit.Path.Count > 0); i++)
                AdvanceChapterScene(frameTime, true);
            var resultZoom = zoom;
            zoom = MinBattleZoom;
            ExpectChapter(column.All(unit => ScreenOf(unit.P).X > W),
                "the result backdrop lets the column drive offscreen even at the widest battle zoom");
            zoom = resultZoom;
            var next = hits.Single(h => h.Id == "下一关 · 河湾镇");
            next.Run();
            ExpectChapter(world.River != null && !world.FirstChapter && !world.IslandMission && !world.RelayMission,
                "the chapter-one result can open the regular Riverbend mission");
        }
    }
}
}
