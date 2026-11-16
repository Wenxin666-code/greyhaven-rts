using Godot;
using Greyhaven;
using System;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private static void Expect(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
    }

    private void WaitFor(Func<bool> condition, float gameSeconds, string description)
    {
        for (int i = 0; i < gameSeconds / MatchTempo.Step && !condition(); i++)
            Simulate((float)MatchTempo.Step);
        Expect(condition(), description + " · " + world.Radio);
    }

    private void RunVerification()
    {
        try
        {
            OpenFront(FrontPage.Missions);
            Expect(missionPanel.Visible && !startPanel.Visible, "Mission selection opens before the prologue");
            ResetChapter(true);
            var operation = world.HarborOperation;
            var vehicle = operation.CommandVehicle;
            Expect(!sequence.TakeCommand(), "No early handoff");
            WaitFor(() => sequence.CanTakeCommand, 180, "Opening reached handoff");
            Expect(operation.TruckDetained && operation.GateBlocked && operation.CargoOutside && operation.RampOpen == 1, "Opening state survives");
            var neutral = operation.RoadVehicle;
            Expect(neutral.Side < 0 && world.Units.Contains(neutral) && !world.CanMoveTo(vehicle, neutral.P), "Neutral truck blocks its occupied cell");
            TakeCommand();
            Expect(CanControl && selected.Single() == vehicle && vehicle.Carrier == null, "Play reuses the actual squad from the landing");
            Expect(world.Explored(0, RiverbendMap.PortPoint(34, 37)) && !world.Explored(0, world.Harbor.P), "Starting vision");
            var origin = view.Screen(vehicle.P);
            Expect(Campaign.Dist(view.Ground(origin), vehicle.P) < .01f && view.Pick(origin) == vehicle, "Projected actor can be clicked");
            Expect(!radar.Visible, "Radar hidden before commissioning");
            HandleKey(new InputEventKey { PhysicalKeycode = Key.Key1, CtrlPressed = true });
            selected.Clear();
            HandleKey(new InputEventKey { PhysicalKeycode = Key.Key1 });
            Expect(selected.Single() == vehicle && multiplier == 1, "Control group keeps the existing number keys");
            var blocked = view.Screen(RiverbendMap.PortPoint(12, 46));
            Expect(DescribePointer(blocked, false, false, false).Kind == PointerKind.Blocked, "Water has a blocked cursor");
            MapClick(blocked, false, false, false);
            Expect(selected.Single() == vehicle && vehicle.Path.Count == 0, "A rejected order preserves the selection and route");

            Deploy(vehicle);
            WaitFor(() => world.Lesson == 1, 14, "Deploy");
            BuildForVerification("power", 20, 38);
            BuildForVerification("refinery", 29, 37);
            WaitFor(() => world.OreReceipts[0] >= Campaign.LoadValue, 175, "First real delivery");
            BuildForVerification("barracks", 23, 43);
            var rifle = Campaign.Info("squad");
            ProductionClick(rifle, false, 5);
            var barracks = world.Live(0).Single(p => p.T.Key == "barracks");
            Expect(barracks.Queue.Count == 5, "Shift batch fills the actual barracks queue");
            OpenQueue();
            Expect(queueOpen && queueCounter.Text.StartsWith("1/2", StringComparison.Ordinal), "Queue pages follow the actual orders");
            ChangeQueuePage(1);
            CancelQueueOrder(queueRows[0]);
            Expect(barracks.Queue.Count == 4, "Queue cancel deletes the selected order");
            ProductionClick(rifle, true, 5);
            Expect(barracks.Queue.Count == 1 && barracks.ProductionPaused, "Right-click batch pauses the last order");
            ProductionClick(rifle, false);
            Expect(!barracks.ProductionPaused, "Click resumes production");
            SetTab(2);
            var rally = RiverbendMap.PortPoint(25, 42);
            Expect(world.CanRallyTo(barracks, rally), "Tutorial barracks has a reachable rally");
            selected.Clear();
            selected.Add(barracks);
            MapClick(view.Screen(rally), false, false, false);
            Expect(barracks.Rally is Point rallyPoint && Campaign.Dist(rallyPoint, rally) < .1f, "Rally is set from a battlefield click");
            WaitFor(() => world.InfantryDelivered > 0, 30, "Rifleman production");
            BuildForVerification("factory", 28, 43);
            ProductionClick(Campaign.Info("ifv"), false);
            WaitFor(() => world.VehiclesDelivered > 0, 65, "IFV production");

            selected.Clear();
            selected.AddRange(world.Live(0).Where(u => world.Armed(u) && !u.T.Building));
            view.Center(RiverbendMap.PortPoint(56.3f, 30.3f));
            MapClick(view.Screen(RiverbendMap.PortPoint(56.3f, 30.3f)), true, true, false);
            Expect(selected.Any(p => p.Path.Count > 0), "Attack-move issued from a screen point");
            WaitFor(() => world.Won, 250, "Clear port defenders");
            WaitFor(() => operation.Scene == HarborScene.Complete, 210, "Departure reaches results");
            Expect(operation.GateLift == 1 && !operation.GateBlocked, "Victory reopened the gate");
            UpdateInterface();
            GD.Print("PASS: Godot chapter one / opening, in-world handoff and picking, construction, mine delivery, infantry and vehicle production, attack-move, victory, departure.");
            Expect(nextChapter.IsVisibleInTree(), "Chapter one offers the next mission");
            VerifyRiverbend();
            VerifyRiverbendGuardOrders();
            VerifyInfantryOrders();
            VerifyAutomaticTargeting();
            VerifyCrushing();
            VerifyWalls();
            VerifyBasin();
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void VerifyRiverbend()
    {
        LaunchChapter(CampaignChapter.Riverbend);
        var operation = world.RiverbendOperation;
        Expect(operation != null && world.FactionCount == 3 && world.River.Bridges.Count == 1, "Riverbend terrain and factions");
        var commandVehicle = operation.CommandVehicle;
        var initialVehiclePosition = commandVehicle.P;
        var initialNorth = world.Units.Where(p => p.Side == 1).Sum(p => p.Hp);
        Expect(!sequence.TakeCommand(), "Riverbend cannot skip opening");
        WaitFor(() => sequence.CanTakeCommand, 65, "Riverbend convoy and opening reach handoff");
        Expect(world.Time >= 17 && Campaign.Dist(initialVehiclePosition, commandVehicle.P) > Campaign.Tile * 3, "Convoy actually entered");
        Expect(world.Units.Where(p => p.Side == 1).Sum(p => p.Hp) < initialNorth ||
            world.Units.Any(p => p.Side == 1 && p.Target?.Side == 2), "Opening fighting used real simulation");
        TakeCommand();
        Expect(CanControl && selected.Single() == commandVehicle && fieldRadio.Visible && !coach.Visible, "Riverbend handed off and radio appears");
        Expect(!world.Explored(0, world.Home(2)), "Far enemy base is outside starting vision");

        var lot = RiverbendMap.RiverBridgeHouse;
        var house = world.Units.Single(p => p.T.Garrison && Campaign.Footprint(p.T, p.P).Contains(new Point(lot.X * Campaign.Tile + 1, lot.Y * Campaign.Tile + 1)));
        var engineer = world.Live(0).Single(p => p.T.Key == "engineer");
        var rifleman = world.Live(0).First(p => p.T.Key == "squad");
        Expect(house.Side < 0 && !world.CanEnterHouse(rifleman, house), "Bridge watch room starts neutral");
        Expect(world.CanEngineer(engineer, house) && world.EngineerOrder(engineer, house), "Engineer can approach the watch room");
        WaitFor(() => house.Side == 0, 140, "Engineer captures watch room");
        Expect(world.EnterHouse(rifleman, house), "Rifleman receives garrison order");
        WaitFor(() => world.HouseCount(house) > 0, 140, "Rifleman garrisons captured building");
        world.EvacuateHouse(house);
        Expect(rifleman.Carrier == null, "Garrison evacuates");

        // Force an outcome only after exercising capture and garrison, to isolate the transition from AI match duration.
        foreach (var enemy in world.Units.Where(p => p.Side > 0)) enemy.Hp = 0;
        WaitFor(() => operation.Departing, 30, "Riverbend enters departure on victory");
        var origin = commandVehicle.P;
        WaitFor(() => Campaign.Dist(origin, commandVehicle.P) > 6 * Campaign.Tile, 24, "The same command vehicle leads the withdrawal");
        WaitFor(() => operation.Result, 80, "Riverbend departure reaches results");
        UpdateInterface();
        Expect(Finished && nextChapter.Visible && nextChapter.Text.Contains("盆地岔口") && resultMission.Text == "河湾镇", "Chapter two result leads to the basin");
        Expect(archiveResult.IsVisibleInTree(), "Victory offers campaign archive");
        archiveResult.EmitSignal(Button.SignalName.Pressed);
        Expect(archivePanel.IsVisibleInTree() && !messagePanel.IsVisibleInTree(), "Archive opens over victory");
        archiveBack.EmitSignal(Button.SignalName.Pressed);
        Expect(Finished && messagePanel.IsVisibleInTree() && !archivePanel.IsVisibleInTree(), "Closing archive restores the same result");
        nextChapter.EmitSignal(Button.SignalName.Pressed);
        Expect(activeChapter == CampaignChapter.Basin && sequence.Opening, "Riverbend result opens the basin directly");
        chosenChapter = CampaignChapter.Riverbend;
        ReturnToMissions();
        Expect(missionPanel.IsVisibleInTree() && chosenChapter == CampaignChapter.Riverbend, "Riverbend restarts from its mission card");
        GD.Print("PASS: Godot chapter two / river, live opening, same units, intelligence, engineer capture, garrison, departure, result.");
    }

    private void VerifyRiverbendGuardOrders()
    {
        LaunchChapter(CampaignChapter.Riverbend);
        WaitFor(() => sequence.CanTakeCommand, 65, "Riverbend reaches defensive test");
        TakeCommand();
        var operation = world.RiverbendOperation;
        var baseFacility = world.Live(1).Single(unit => unit.T.Key == "hq");
        var attacker = world.Live(2).First(world.Armed);
        var guards = world.Live(1).Where(operation.IsImpoundGuard).ToArray();
        Expect(guards.Length >= 2, "Impound is still staffed before the attack");
        var posts = guards.ToDictionary(guard => guard, guard => guard.GuardHome);
        operation.ReportFacilityHit(baseFacility, attacker);
        WaitFor(() => guards.Where(guard => guard.Deployed).Count(guard => guard.GuardHome != posts[guard] &&
            guard.GuardHome.HasValue && Campaign.Dist(guard.GuardHome.Value, baseFacility.P) < 4 * Campaign.Tile) >= 2,
            10, "Hit on North Line HQ recalls posted guards");

        var survivors = guards.Where(guard => guard.Deployed).Take(2).ToArray();
        var remote = Enumerable.Range(4, 22)
            .SelectMany(x => Enumerable.Range(8, 45).Select(y => RiverbendMap.PortPoint(x, y)))
            .Where(p => !world.Explored(2, p) && world.CanMoveTo(survivors[0], p))
            .OrderByDescending(p => Campaign.Dist(p, world.Home(2))).Take(2).ToArray();
        Expect(remote.Length == survivors.Length, "Place the survivors outside White Stone vision");
        foreach (var unit in world.Units.Where(unit => unit.Alive && unit.Side == 1 && !survivors.Contains(unit)))
            unit.Hp = 0;
        for (int i = 0; i < survivors.Length; i++)
        {
            survivors[i].P = remote[i];
            world.Guard(survivors[i]);
        }
        WaitFor(() => world.AiSummary(2).EndsWith("target=0", StringComparison.Ordinal), 18,
            "White Stone abandons the destroyed northern base");
        Expect(world.Remaining(1) == 2 && !world.HasSurrendered(1),
            "The unseen survivors remain active without blocking strategic retargeting");
        GD.Print("PASS: Riverbend AI / impound guards recall on HQ attack; two survivors no longer keep White Stone attacking an empty base.");
    }

    private void VerifyInfantryOrders()
    {
        LaunchChapter(CampaignChapter.Riverbend);
        WaitFor(() => sequence.CanTakeCommand, 65, "Riverbend opens for infantry checks");
        TakeCommand();
        Piece At(string key, float x, float y) {
            var point = RiverbendMap.PortPoint(x, y);
            return world.Spawn(key, 0, point.X, point.Y);
        }

        var squad = Campaign.Info("squad");
        var original = At("barracks", 14, 29);
        var primary = At("barracks", 29, 34);
        At("power", 14, 36);
        Expect(world.Train(squad, 0, original) && world.Train(squad, 0, primary), "Both orders enter the shared infantry queue");
        Expect(original.Queue.Count == 2 && primary.Queue.Count == 0 && world.ProductionRate(original) == 1.25f,
            "Two barracks work on one serial order at 1.25 times speed");
        Expect(world.SetPrimary(primary) && world.InfantryQueue(0) == original, "Changing the outlet retains the shared paid queue");
        WaitFor(() => world.InfantryDelivered == 1, squad.Seconds + 1, "The first serial infantry order finishes");
        Expect(world.InfantryDelivered == 1 && Campaign.Dist(world.LastReady.P, primary.P) < Campaign.Dist(world.LastReady.P, original.P),
            "The first soldier exits the selected barracks");
        Simulate((float)MatchTempo.Step);
        float work = original.Work;
        Expect(work > 0 && world.Sell(original) && primary.Queue.Count == 1 && primary.Work == work, "The remaining barracks keeps paid work");

        var bunker = At("bunker", 23, 46);
        var apron = Campaign.Middle(Campaign.ExitApron(bunker.T, Campaign.Footprint(bunker.T, bunker.P)));
        world.Spawn("miner", -1, apron.X, apron.Y).Mining = false;
        var first = At("squad", 18, 46);
        var second = At("squad", 20, 46);
        Expect(world.EnterHouse(first, bunker) && world.EnterHouse(second, bunker) && first.Goal != second.Goal,
            "Two soldiers choose distinct free entrances even when the main door is blocked");
        WaitFor(() => world.HouseCount(bunker) == 2, 45, "Both soldiers garrison the bunker");
        world.EvacuateHouse(bunker);
        Expect(first.Deployed && second.Deployed, "Both soldiers can leave the bunker");
        world.Halt(first);
        world.Halt(second);
        selected.Clear();
        selected.AddRange(new[] { first, second });
        HandleKey(new InputEventKey { PhysicalKeycode = Key.X });
        Expect(first.Path.Count > 0 && second.Path.Count > 0 && first.Goal != second.Goal,
            "X assigns distinct nearby scatter destinations to the selected infantry");
        GD.Print("PASS: shared infantry queue, primary exit, paid transfer, and blocked-door garrison.");
    }

    private static void VerifyCrushing()
    {
        const float step = (float)MatchTempo.Step;
        const float tile = Campaign.Tile;
        var start = new Point(420, 612);
        var crossing = new Point(start.X + 2 * tile, start.Y);
        var destination = new Point(start.X + 4 * tile, start.Y);
        foreach (var chassis in new[] { "tank", "arty", "mcv", "miner", "ifv" })
        {
            var battle = new Campaign(tutorial: true);
            var vehicle = battle.Spawn(chassis, 0, start.X, start.Y);
            var enemy = battle.Spawn("engineer", 1, crossing.X, crossing.Y);
            var adjacent = battle.Spawn("engineer", 1, crossing.X, crossing.Y + tile);
            Expect(battle.Move(vehicle, destination, false), $"Issue an ordinary move to {chassis}");
            for (int i = 0; i < 240 && vehicle.Path.Count > 0; i++)
                battle.MoveHarborVehicle(vehicle, step, followTraffic: false);
            Expect(adjacent.Alive && enemy.Alive == (chassis == "ifv"),
                $"{chassis} only crushes eligible infantry on its track");
            if (chassis != "ifv")
                Expect(Campaign.Dist(vehicle.P, destination) < .1f, $"{chassis} continues to its destination");
        }

        var defense = new Campaign(tutorial: true);
        var tank = defense.Spawn("tank", 0, start.X, start.Y);
        var defender = defense.Spawn("at", 1, crossing.X, crossing.Y);
        defender.Entrenched = true;
        defender.Stance = 1;
        var friendly = defense.Spawn("squad", 0, crossing.X, crossing.Y + tile);
        var neutral = defense.Spawn("miner", -1, crossing.X, crossing.Y - tile);
        Expect(!Campaign.CanCrush(tank, friendly) && !Campaign.CanCrush(tank, neutral) &&
            !Campaign.CanCrush(tank, defender), "Friendly infantry, neutral vehicles and deployed Guardian GIs are protected");
        Expect(defense.Move(tank, destination, false), "Issue move near the deployed Guardian GI");
        for (int i = 0; i < 120 && tank.Path.Count > 0; i++)
            defense.MoveHarborVehicle(tank, step, followTraffic: false);
        Expect(defender.Alive, "A real movement step does not crush the deployed defender");
        defense.Scatter(new[] { defender });
        Expect(defender.Entrenched && defender.Path.Count == 0, "Scatter leaves deployed infantry in place");

        var dodge = new Campaign(tutorial: true);
        var movingTank = dodge.Spawn("tank", 1, start.X, start.Y);
        var escaping = dodge.Spawn("engineer", 0, crossing.X, crossing.Y);
        Expect(dodge.Move(movingTank, destination, false), "Set up a tank approaching the infantry");
        dodge.Scatter(new[] { escaping });
        Expect(escaping.Path.Count > 0 && Campaign.Dist(escaping.Goal, crossing) <= tile * 1.42f,
            "Scatter picks an open adjacent cell");
        for (int i = 0; i < 240 && movingTank.Path.Count > 0; i++)
        {
            dodge.MoveHarborVehicle(escaping, step, followTraffic: false);
            dodge.MoveHarborVehicle(movingTank, step, followTraffic: false);
        }
        Expect(escaping.Alive && Campaign.Dist(movingTank.P, destination) < .1f,
            "An infantry unit that scatters in time can clear the tank's track");

        var ai = new Campaign(tutorial: true);
        var attacker = ai.Spawn("tank", 1, start.X, start.Y);
        var target = ai.Spawn("engineer", 0, start.X + 1.5f * tile, start.Y);
        ai.Attack(attacker, target);
        ai.Tick(step);
        Expect(attacker.Path.Count > 0 && attacker.Path.Last() == target.P && ai.Shots.Count == 0,
            "An AI tank closes on a nearby crushable target");
        for (int i = 0; i < 120 && target.Alive; i++) ai.Tick(step);
        Expect(!target.Alive && attacker.FiredAt < 0, "The AI finishes the close overrun by contact");
        GD.Print("PASS: ordinary movement crush, vehicle profiles, flank clearance, deployment immunity, scatter, and AI close overrun.");
    }

    private static void VerifyAutomaticTargeting()
    {
        var battle = new Campaign(tutorial: true);
        const float tile = Campaign.Tile;
        var tank = battle.Spawn("tank", 0, 360, 480);
        var power = battle.Spawn("power", 1, tank.P.X + 3 * tile, tank.P.Y);
        var bunker = battle.Spawn("bunker", 1, tank.P.X - 3 * tile, tank.P.Y);
        void Advance(float seconds)
        {
            for (int step = 0; step < seconds / MatchTempo.Step; step++)
                battle.Tick((float)MatchTempo.Step);
        }

        Advance(1);
        Expect(tank.Target == null && battle.Explored(0, power.P), "An idle tank ignores a revealed, unarmed building");
        battle.Guard(tank);
        Advance(1);
        Expect(tank.Target == null, "Guard ignores an unarmed building and an empty bunker");
        foreach (bool attackMove in new[] { false, true })
        {
            var destination = new Point(tank.P.X, tank.P.Y - tile * 3);
            Expect(battle.Move(tank, destination, attackMove), "The test route passes the unarmed building");
            float travel = tank.Travel;
            Advance(.9f);
            Expect(tank.Travel > travel && tank.Target == null && tank.MovingFireTarget == null && power.Hp == power.T.Hp,
                "Moving and attack-moving tanks ignore unarmed buildings");
        }

        battle.Attack(tank, power);
        Advance(6);
        Expect(power.Hp < power.T.Hp, "Direct orders still attack an unarmed building");
        battle.Guard(tank);
        var turret = battle.Spawn("post", 1, tank.P.X + 2 * tile, tank.P.Y);
        turret.Cool = 30;
        Advance(.5f);
        Expect(tank.Target == turret && tank.Contact == ContactOrder.Automatic, "Guard acquires a hostile defense without waiting for it to fire");

        turret.Hp = 0;
        bunker.P = new Point(tank.P.X - 3 * tile, tank.P.Y);
        var occupant = battle.Spawn("squad", 1, bunker.P.X, bunker.P.Y);
        occupant.Carrier = bunker;
        occupant.Cool = 30;
        Advance(.5f);
        Expect(tank.Target == bunker && tank.Contact == ContactOrder.Automatic, "An armed garrison makes its bunker an automatic target");
        occupant.Hp = 0;
        Advance(.5f);
        Expect(tank.Target == null, "An empty bunker stops being an automatic target");

        var nearby = battle.Spawn("tank", 1, tank.P.X, tank.P.Y - 2.5f * tile);
        nearby.Hp = nearby.T.Hp * .25f;
        var farther = battle.Spawn("tank", 1, tank.P.X, tank.P.Y - 4 * tile);
        nearby.Cool = farther.Cool = tank.Cool = 30;
        Advance(.5f);
        Expect(tank.Target == farther, "Threat scoring prefers the stronger of two identical tanks in range");
        battle.Attack(nearby, tank);
        nearby.Cool = 0;
        float health = tank.Hp;
        Advance(3);
        Expect(tank.Hp < health && tank.Target == farther, "Incoming fire does not replace the player's existing target");
        GD.Print("PASS: passive targeting / movement, directed fire, defenses, garrisons, threat scores and target retention.");
    }

    private void VerifyBasin()
    {
        LaunchChapter(CampaignChapter.Basin);
        var operation = world.BasinOperation;
        var oil = operation.OilDerrick;
        var oilSite = oil.P;
        int startingFunds = world.Funds[0];
        var startingEnemies = world.Units.Where(unit => unit.Side > 0).ToArray();
        var startingHealth = startingEnemies.Sum(unit => unit.Hp);
        Expect(world.FactionCount == 6 && !sequence.TakeCommand(), "Basin opening has five opponents and a live handoff");
        Expect(Enumerable.Range(1, 5).All(side => world.Live(side).Any(unit =>
            unit.T.Key == "bunker" && Campaign.Dist(unit.P, RiverbendMap.PortPoint(
                world.River.BasinPost(side).X, world.River.BasinPost(side).Y)) <= 2 * Campaign.Tile)),
            "All five checkpoints begin with their own faction's bunker");
        var wallCells = new System.Collections.Generic.HashSet<System.Drawing.Point>();
        foreach (var wall in startingEnemies.Where(unit => unit.T.IsWall))
        {
            var cell = new System.Drawing.Point((int)(wall.P.X / Campaign.Tile), (int)(wall.P.Y / Campaign.Tile));
            var ground = world.River.At(cell.X, cell.Y);
            Expect(ground is { Cliff: false, Tree: false } &&
                ground.Railroad == operation.RailBarricades.Contains(wall) &&
                ground.Ground is not (RiverbendMap.Surface.Water or RiverbendMap.Surface.Bridge) &&
                wallCells.Add(cell) && world.WallConnections(wall) != 0 &&
                !world.Units.Any(unit => unit != wall && unit.T.Building && Campaign.Footprint(unit.T, unit.P).Contains(wall.P)),
                "Only the designated wall runs cross the rails; all walls are linked and clear of buildings");
        }
        Expect(Enumerable.Range(1, 5).All(side => startingEnemies.Count(unit => unit.Side == side && unit.T.IsWall) >= 6),
            "All five checkpoints have walls in their own faction colors");
        var basinSequence = (BasinSequence)sequence;
        for (int side = 1; side < world.FactionCount; side++)
        {
            Expect(!world.Explored(0, basinSequence.CheckpointGate(side)), "Surveillance reveals each gate in order");
            WaitFor(() => basinSequence.ReconSide == side && basinSequence.OpeningShade == 0, 65, "Show the next checkpoint");
            Expect(operation.Freight.Loads == 2 && world.Explored(0, basinSequence.CheckpointGate(side)) &&
                world.Explored(0, world.Home(side)) && operation.RailBarricades.Any(wall => wall.Side == side && wall.Alive),
                "Each shot reveals its garrison and a live barrier on the railroad after loading");
            UpdateInterface();
            Expect(fieldRadioSpeaker.Text.Contains("先遣侦察组") &&
                fieldRadioText.Text.Contains($"[color=#{FactionInk(side).ToHtml(false)}]"),
                "Scouting reports identify the correct speaker and consistently color all five factions");
        }
        WaitFor(() => sequence.CanTakeCommand, 20, "Basin opening reaches handoff");
        Expect(startingEnemies.All(unit => unit.FiredAt < 0) &&
            startingEnemies.Sum(unit => Math.Max(0, unit.Hp)) == startingHealth, "The opening does not stage enemy combat");
        var trucks = world.Units.Where(unit => operation.IsStoryTruck(unit)).ToArray();
        Expect(trucks.Select((unit, index) => unit.Travel > 12 * Campaign.Tile &&
            Campaign.Dist(unit.P, operation.Freight.LoadingBay(index)) <= Campaign.Tile * .5f && unit.Cargo == 0)
            .All(docked => docked) &&
            operation.ConvoyParked && operation.Freight.Loads == 2,
            "Both delivery trucks park and their crates move onto the train before handoff");
        float parked = trucks.Sum(unit => unit.Travel);
        TakeCommand();
        Expect(basinObjective.Visible && basinRemaining.Text.Contains("5 / 5"), "Five factions remain after the opening");
        Expect(world.Live(0).Count(unit => !unit.T.Building) == 4 &&
            RiverbendMap.BasinWidth == RiverbendMap.IslandColumns && RiverbendMap.BasinHeight == RiverbendMap.IslandRows,
            "Basin starts with an economy-building force on the larger map");
        Expect(oil is { Side: 0, Deployed: true } && oil.P == oilSite && world.OreReceipts[0] == 0 &&
            world.Funds[0] == startingFunds && !world.CanSell(oil), "The oil derrick is active at the yard without pre-handoff cash");
        WaitFor(() => world.Funds[0] > startingFunds, BasinOperation.OilPayoutSeconds + 1, "The oil derrick provides the first payout without a power plant");
        Expect(world.Funds[0] == startingFunds + BasinOperation.OilPayout &&
            oil.LastReceipt == BasinOperation.OilPayout && world.OreReceipts[0] == 0,
            "Oil is credited and displayed separately from ore deliveries");
        Expect(world.CanDeploy(operation.CommandVehicle), "The player can deploy at the upper-left entrance");
        SetDiplomacy(true);
        Expect(factionLabels[5].Visible && factionLabels[5].Text == "边境独立旅", "All six factions appear in the roster");
        SetDiplomacy(false);
        var starts = world.River.Starts;
        for (int i = 0; i < starts.Length; i++)
            for (int j = i + 1; j < starts.Length; j++)
                Expect(Campaign.Dist(starts[i], starts[j]) > 72, "Basin headquarters have separate territories");
        Expect(starts.All(start => world.River.Connected(starts[0], start)), "All camps are reachable on land");
        var easternRail = new System.Drawing.Point(282, 139);
        Expect(world.River.Connected(starts[0], easternRail, wallCells.Contains),
            "The rail valley is traversable from the terminus to the east pass with all checkpoint walls in place");
        foreach (var pass in RiverbendMap.BasinChokes)
            Expect(!world.River.Connected(starts[0], easternRail, tile => Campaign.Dist(tile, pass) <= 13),
                $"The cliffs prevent a bypass around the pass at {pass}");
        Expect(world.River.Cells.Any(cell => cell.Railroad && cell.Ground == RiverbendMap.Surface.Bridge) &&
            world.River.Cells.Any(cell => cell.Ground == RiverbendMap.Surface.Marsh) &&
            world.River.Cells.Any(cell => cell.Height > 2), "The line crosses real river and elevated terrain tiles");
        Expect(world.River.Houses.All(house => Enumerable.Range(house.Lot.Left, house.Lot.Width).All(x =>
            Enumerable.Range(house.Lot.Top, house.Lot.Height).All(y => !world.River.At(x, y).Railroad))), "Neutral houses leave the tracks clear");
        Expect(world.Live(1).Any(unit => unit.T.Key == "factory") && world.Live(5).Any(unit => unit.T.Key == "refinery"),
            "Opponents open with independent production and mining");

        WaitFor(() => Enumerable.Range(1, world.FactionCount - 1).All(side => world.OreReceipts[side] > 0),
            125, "Each opposing camp mines and delivers ore under normal simulation");
        Expect(trucks.All(unit => unit.Alive) && trucks.Sum(unit => unit.Travel) == parked, "Parked trucks are scenery during battle");
        var whiteStone = world.Live(2).First(unit => world.Armed(unit));
        world.CombatKill(oil, whiteStone);
        int fundsAfterOilLoss = world.Funds[0];
        Simulate(BasinOperation.OilPayoutSeconds + 1);
        Expect(world.Funds[0] == fundsAfterOilLoss, "Destroying the oil derrick stops the payouts");
        foreach (var enemy in world.Units.Where(unit => unit.Side == 1 && unit.Alive).ToArray())
            world.CombatKill(enemy, whiteStone);
        Simulate((float)MatchTempo.Step);
        Expect(world.Remaining(1) == 0 && operation.Orders.UnreadCount == 0,
            "AI receives its defeated opponent's order without interrupting the player");
        Expect(operation.Orders.Defeats.Last().Winner == 2 && operation.Orders.Defeats.Last().Defeated == 1,
            "The faction defeat records the actual opposing victor");
        var player = world.Live(0).First(unit => world.Armed(unit));
        foreach (var enemy in world.Units.Where(unit => unit.Side == 2 && unit.Alive).ToArray())
            world.CombatKill(enemy, player);
        Simulate((float)MatchTempo.Step);
        UpdateInterface();
        Expect(roadOrderOverlay.Visible && operation.Orders.UnreadCount == 2 && operation.Orders.Current == (2, 2),
            "Defeating the holder presents both orders, one sheet at a time");
        Expect(roadOrderRadio.Visible && roadOrderRadioText.Text.Contains("刚从") &&
            roadOrderRadioText.Text.Contains($"[color=#{FactionInk(2).ToHtml(false)}]") &&
            roadOrderCheckpoint.Text.Contains($"[color=#{FactionInk(2, true).ToHtml(false)}]"),
            "The first recovered order gets Xu's reaction and the holder uses the correct radio and paper colors");
        float slowingAt = world.Time;
        FinishOrderAnimation();
        Expect(world.Time > slowingAt && world.Time - slowingAt < .55f, "The battle decelerates before the paper is ready");
        float frozenAt = world.Time;
        AdvanceRoadOrder(.5f);
        Expect(world.Time == frozenAt && !world.Won && trucks.Sum(unit => unit.Travel) == parked,
            "The battle and convoy stay paused while reading an order");
        AcknowledgeRoadOrder();
        Expect(operation.Orders.Current == (2, 2), "The old sheet remains until it slides out");
        FinishOrderAnimation();
        Expect(operation.Orders.Current == (1, 2), "Inherited road order credits the faction that held it");
        Expect(!roadOrderRadio.Visible && roadOrderOrigin.Text.Contains($"[color=#{FactionInk(1, true).ToHtml(false)}]") &&
            roadOrderOrigin.Text.Contains($"[color=#{FactionInk(2, true).ToHtml(false)}]"),
            "The radio only appears once, and inherited orders color the holder and recipient separately");
        AcknowledgeRoadOrder();
        Expect(!CanControl, "The battle stays still while the last page leaves");
        FinishOrderAnimation();
        Expect(CanControl, "Reading all seized orders returns to the battle");
        var blockers = Enumerable.Range(0, 5).Select(index =>
        {
            var point = RiverbendMap.PortPoint(27, 75 + index * 2);
            return world.Spawn(index % 2 == 0 ? "tank" : "ifv", 0, point.X, point.Y);
        }).ToArray();
        Expect(blockers.All(unit => operation.Freight.IsOnTrack(unit.P)), "The stationary vehicles start inside the train corridor");
        foreach (var enemy in world.Units.Where(unit => unit.Side > 0 && unit.Alive && !unit.T.IsWall).ToArray())
            world.CombatKill(enemy, player);
        Simulate((float)MatchTempo.Step);
        Expect(world.Won && trucks.Sum(unit => unit.Travel) == parked, "The last enemy is the victory trigger");
        Simulate(.5f);
        Expect(!sequence.Departing && operation.Orders.UnreadCount == 3, "Final documents must be read before departure");
        ReadRemainingOrders();
        Expect(operation.Orders.ReadCount == 5, "All five unique road orders reach the player");
        WaitFor(() => operation.Freight.Travel > 0, 20, "Victory clears standing vehicles before release");
        UpdateInterface();
        Expect(fieldRadio.Visible && fieldRadioSpeaker.Text.Contains("换装站值班员"), "Train clearance is read by the station dispatcher");
        Expect(blockers.All(unit => !operation.Freight.IsOnTrack(unit.P)) && operation.Freight.RouteClear && basinSequence.Elapsed < 3.2f,
            "The vehicles move onto the shoulder quickly before the train begins to move");
        Expect(operation.RailBarricades.All(wall => !wall.Alive), "Captured barricades are opened before release");
        WaitFor(() => sequence.Caption == BasinOperation.LeadRadio, 20, "Zhou compares the received orders in the departure");
        UpdateInterface();
        Expect(fieldRadio.Visible && fieldRadioSpeaker.Text.Contains("周砚"), "The document finding belongs to the engineering officer");
        WaitFor(() => operation.Freight.DepartureProgress >= .49f, 75, "The train approaches the midpoint");
        Expect(!Finished && sequence.Departing, "Results stay hidden before the midpoint");
        WaitFor(() => Finished, 3, "Basin results appear at the midpoint");
        Expect(operation.Freight.DepartureProgress is >= .5f and < .51f && !sequence.Complete,
            "Results appear while the departure is still in progress");
        var resultCamera = sequence.Camera();
        UpdateInterface();
        Expect(messagePanel.Visible && resultTitle.Visible && takeCommand.Visible && !messageSpeaker.Visible &&
            !nextChapter.Visible && resultMission.Text == "盆地岔口", "Basin result shows usable controls alongside the departure");
        WaitFor(() => sequence.Caption == BasinOperation.GoToRelayRadio, 15, "Xu's last radio line plays during the result");
        UpdateInterface();
        Expect(fieldRadio.Visible && messagePanel.Visible && fieldRadio.Position.Y + fieldRadio.Size.Y < messagePanel.Position.Y,
            "The closing radio and result have separate space on screen");
        WaitFor(() => sequence.Complete, 120, "The closing scene finishes behind the result");
        Expect(Finished && operation.Freight.AtEnd && !world.Battlefield.Contains(operation.Freight.Wagon(1).Position) &&
            Campaign.Dist(resultCamera, basinSequence.Camera()) > 70 * Campaign.Tile,
            "The camera and train continue moving after the result, until the last wagon leaves");
        UpdateInterface();
        Expect(trucks.Sum(unit => unit.Travel) == parked && trucks.All(unit => unit.Cargo == 0) &&
            operation.Freight?.Travel > 24 * Campaign.Tile,
            "The trucks stay at the terminal while the loaded train departs along the track");
        GD.Print("PASS: Basin chapter / checkpoints, oil supply, rail clearing, and moving train behind the midpoint results.");
    }

    private void FinishOrderAnimation()
    {
        for (int frame = 0; frame < 60 && ReadingRoadOrder && roadOrderPhase != RoadOrderPhase.Ready; frame++)
            AdvanceRoadOrder(1f / 60);
        Expect(!ReadingRoadOrder || roadOrderPhase == RoadOrderPhase.Ready, "Road order animation reaches a readable or hidden state");
    }

    private void ReadRemainingOrders()
    {
        while (ReadingRoadOrder)
        {
            FinishOrderAnimation();
            AcknowledgeRoadOrder();
        }
    }

    private void BuildForVerification(string key, int x, int y)
    {
        var type = Campaign.Info(key);
        ProductionClick(type, false);
        WaitFor(() => world.Construction(type)?.Ready == true, type.Seconds * 2 + 8, "Prepare " + key);
        var site = SuggestedSite(type, RiverbendMap.PortPoint(x, y));
        Expect(!site.IsEmpty, "Find placement for " + key);
        ProductionClick(type, false);
        CancelMapMode();
        Expect(placing == null && world.Construction(type)?.Ready == true, "Cancel preview preserves the ready " + key);
        ProductionClick(type, false);
        MapClick(view.Screen(site), false, false, false);
        WaitFor(() => world.Live(0).Any(p => p.T == type), 8, "Place " + key);
    }

    private Point SuggestedSite(TypeInfo type, Point preferred)
    {
        for (int n = 0; n < 10; n++)
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != n) continue;
            var p = new Point(preferred.X + x * Campaign.Tile, preferred.Y + y * Campaign.Tile);
            if (world.CanPlace(type, p, null)) return Campaign.Middle(Campaign.Footprint(type, p));
        }
        return Point.Empty;
    }

    private async void RunInputVerification()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var viewport = GetViewport();
            viewport.NotifyMouseEntered();
            Click(archiveEntry.GetGlobalRect().GetCenter());
            Expect(archivePanel.IsVisibleInTree() && !startPanel.IsVisibleInTree(), "Campaign archive opens from the main menu");
            for (int i = 0; i < archiveCards.Length; i++)
            {
                Click(archiveCards[i].GetGlobalRect().GetCenter());
                var record = FactionArchive.Records[i];
                Expect(archiveIndex == i && archiveName.Text == record.Name &&
                    archiveMark.Texture != null && archiveMark.Texture == archiveMarks[i] &&
                    archiveAccent.Color == BattleArt.Faction(i) && archiveArticle.Text.Contains(record.People) &&
                    archiveArticle.Text.Contains(record.Position), "Archive card shows the leadership, history and team color");
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Click(archiveArticle.GetGlobalRect().GetCenter(), MouseButton.WheelDown);
            Expect(archiveArticle.GetVScrollBar().Value > 0, "Long archive can be read using the mouse wheel");
            viewport.PushInput(new InputEventKey { Keycode = Key.Left, Pressed = true }, true);
            Expect(archiveIndex == archiveCards.Length - 2, "Arrow key selects the adjacent archive card");
            Expect(archiveArticle.GetVScrollBar().Value == 0, "Switching faction returns the article to its beginning");
            viewport.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
            Expect(archivePanel.IsVisibleInTree() && !missionPanel.IsVisibleInTree(), "Enter does not open mission selection from the archive");
            viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            Expect(startPanel.IsVisibleInTree(), "Escape returns from archive to main menu");
            Click(new Vector2(170, 620));
            Expect(rulesPanel.Visible && manualPages[0].Visible, "Field manual opens");
            Click(manualTabs[1].GetGlobalRect().GetCenter());
            Expect(manualPages[1].Visible && !manualPages[0].Visible, "Manual switches to unit roster");
            viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            Click(new Vector2(200, 550));
            Expect(missionPanel.Visible, "Click starts mission selection");
            Click(new Vector2(1215, 650));
            Expect(started, "Click starts the opening");
            focused = false;
            Click(new Vector2(1390, 23));
            Expect(paused && scenePause.IsVisibleInTree(), "Cinematic opens its compact pause dialog");
            Click(new Vector2(618, 434));
            Expect(!paused, "Compact dialog resumes cinematic");
            WaitFor(() => sequence.CanTakeCommand, 180, "Wait for opening handoff");
            UpdateInterface();
            Click(takeCommand.GetGlobalRect().GetCenter());
            Expect(CanControl, "Click transfers control to the player");

            var vehicle = world.HarborOperation.CommandVehicle;
            var water = view.Screen(RiverbendMap.PortPoint(12, 46));
            Click(water);
            Expect(selected.Single() == vehicle && vehicle.Path.Count == 0, "Water click preserves the selection");
            Click(view.Screen(vehicle.P));
            WaitFor(() => world.Lesson == 1, 14, "Click expands the command vehicle");
            UpdateInterface();

            var slot = tiles.Single(tile => tile.Type?.Key == "power");
            Click(slot.Plate.GetGlobalRect().GetCenter());
            var power = Campaign.Info("power");
            WaitFor(() => world.Construction(power)?.Ready == true, power.Seconds + 8, "Click orders a power plant");
            UpdateInterface();
            Click(slot.Plate.GetGlobalRect().GetCenter());
            Expect(placing == power, "Second click previews the ready building");
            Click(new Vector2(490, 460), MouseButton.Right);
            Expect(placing == null && world.Construction(power)?.Ready == true, "Right click cancels placement");
            Click(slot.Plate.GetGlobalRect().GetCenter());
            Click(view.Screen(SuggestedSite(power, RiverbendMap.PortPoint(20, 38))));
            Expect(placing == null, "Successful placement exits preview");
            WaitFor(() => world.Live(0).Any(p => p.T == power), 8, "Click places the ready building");

            var infantry = world.Live(0).First(p => p.T.Foot);
            Click(view.Screen(infantry.P));
            Expect(selected.SingleOrDefault() == infantry, "Select an infantry member for the toolbar");
            Click(teamOne.GetGlobalRect().GetCenter());
            Expect(HasGroup(1), "Toolbar creates a team");
            Click(new Vector2(490, 460), MouseButton.Right);
            Click(teamOne.GetGlobalRect().GetCenter());
            Expect(selected.Single() == infantry, "Toolbar recalls a team");
            Click(routeCommand.GetGlobalRect().GetCenter());
            var pathOne = RiverbendMap.PortPoint(20.5f, 41.5f);
            var pathTwo = RiverbendMap.PortPoint(23.5f, 40.5f);
            Click(view.Screen(pathOne));
            Click(view.Screen(pathTwo));
            Expect(plannedRoutes.TryGetValue(infantry, out var waypoints) && waypoints.Count == 2, "Toolbar previews successive waypoints");
            Click(routeCommand.GetGlobalRect().GetCenter());
            Expect(!PlanningRoute && infantry.Waypoints.Count == 2, "Toolbar commits planned waypoints");
            Click(beaconCommand.GetGlobalRect().GetCenter());
            Click(view.Screen(pathOne));
            Expect(!beaconMode && view.Beacons.Count == 1, "Toolbar places a one-shot marker");
            viewport.PushInput(new InputEventKey { PhysicalKeycode = Key.Delete, Pressed = true }, true);
            Expect(view.Beacons.Count == 0, "Delete removes the selected marker");

            var dragAt = new Vector2(620, 350);
            var dragOffset = new Vector2(64, 34);
            var oldCamera = view.CameraPosition;
            ButtonAt(dragAt, MouseButton.Right, true);
            viewport.PushInput(new InputEventMouseMotion
            {
                Position = dragAt + dragOffset, GlobalPosition = dragAt + dragOffset, Relative = dragOffset,
                ButtonMask = MouseButtonMask.Right
            }, true);
            ButtonAt(dragAt + dragOffset, MouseButton.Right, false);
            Expect(Campaign.Dist(oldCamera, view.CameraPosition) > 1, "Right dragging moves the camera");
            var zoom = view.Zoom;
            Click(dragAt, MouseButton.WheelUp);
            Expect(view.Zoom > zoom, "Wheel zooms the playfield");
            Click(new Vector2(1350, 57));
            Expect(paused && showObjectives && battlePause.IsVisibleInTree(), "Objectives opens its mission dialog");
            viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            Expect(!paused && CanControl, "Escape closes objectives and returns to the battlefield");
            Click(new Vector2(1420, 57));
            Expect(paused && !showObjectives && battlePause.IsVisibleInTree(), "Options opens the battle pause dialog");
            Click(rateButtons[3].GetGlobalRect().GetCenter());
            Expect(multiplier == 4 && paused, "Pause dialog changes game speed");
            Click(new Vector2(925, 633));
            Expect(!paused && queueOpen, "Pause dialog opens production overview");
            SetPaused(true);
            Click(new Vector2(500, 630));
            Expect(keyboardOpen && keyboardPanel.Visible, "Pause opens keyboard settings");
            Click(keyboardRows[0].GetGlobalRect().GetCenter());
            viewport.PushInput(new InputEventKey { PhysicalKeycode = Key.J, Pressed = true }, true);
            Expect(draftBindings[Key.D] == Key.J, "Keyboard captures a new key");
            CloseKeyboard();
            Expect(BoundAction(Key.D) == Key.D, "Cancelling keeps the current binding");
            ReturnToMissions();
            Click(chapterButtons[1].GetGlobalRect().GetCenter());
            Expect(chosenChapter == CampaignChapter.Riverbend && briefTitle.Text == "河湾镇", "Second chapter has its own briefing");
            Click(new Vector2(180, 600));
            Expect(mapViewer.IsVisibleInTree() && expandedMap.Texture == briefMap.Texture, "Chosen map opens enlarged");
            Click(new Vector2(270, 690));
            Click(launchChapterButton.GetGlobalRect().GetCenter());
            Expect(SceneRunning && !messagePanel.Visible && !riverbendHandoff.Visible, "Riverbend begins without narration");
            WaitFor(() => sequence.CanTakeCommand, 65, "Second chapter completes opening");
            UpdateInterface();
            Click(riverbendHandoff.GetGlobalRect().GetCenter());
            Expect(CanControl && fieldRadio.Visible, "Second chapter hands off from its own button");
            var rover = selected.Single();
            var road = RiverbendMap.PortPoint(27, 34);
            RadarOrder(road, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
            Expect(rover.Path.Count > 0 && Campaign.Dist(rover.Goal, road) < Campaign.Tile, "Radar left click orders a selected unit");
            Expect(view.DisplayedMoveTarget(rover) == road, "Move line points to the player's click");
            RadarOrder(RiverbendMap.PortPoint(61, 34), new InputEventMouseButton { ButtonIndex = MouseButton.Right });
            Expect(selected.Single() == rover && rover.Path.Count > 0, "Radar right click centers without changing orders");
            viewport.PushInput(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = true }, true);
            Expect(diplomacyOpen && factionLabels[2].Visible && factionLabels[2].Text.Contains("白石"), "Faction window lists both opponents");
            SetDiplomacy(false);
            var fighters = world.Live(0).Where(unit => world.Armed(unit)).Take(2).ToArray();
            var targetPoint = RiverbendMap.PortPoint(29, 33);
            var target = world.Spawn("power", 1, targetPoint.X, targetPoint.Y);
            world.RevealBasinScene(target.P, Campaign.Tile * 2);
            view.Center(target.P);
            selected.Clear();
            selected.AddRange(fighters);
            Click(view.Screen(target.P));
            Expect(fighters.All(unit => view.DisplayedAttackTarget(unit) == target.P) && view.MarkedTarget == target,
                "Clicking an enemy building gives every selected attacker a direct line and flashes the target");
            if (!string.IsNullOrEmpty(OS.GetEnvironment("GREYHAVEN_CAPTURE_DIR")))
            {
                view.Selection = fighters;
                await SaveFrame("attack-lines");
            }
            foreach (var unit in fighters) world.Halt(unit);
            Expect(fighters.All(unit => view.DisplayedAttackTarget(unit) == null), "Stopping units clears their attack lines");
            var bunkerSite = RiverbendMap.PortPoint(23, 46);
            var bunker = world.Spawn("bunker", 0, bunkerSite.X, bunkerSite.Y);
            var footSite = RiverbendMap.PortPoint(18, 46);
            var foot = world.Spawn("squad", 0, footSite.X, footSite.Y);
            world.RevealBasinScene(bunker.P, Campaign.Tile * 8);
            view.Center(bunker.P);
            selected.Clear();
            Click(view.Screen(foot.P, .19f));
            Expect(selected.SingleOrDefault() == foot, "A click on a small rifleman selects it");
            Click(view.Screen(bunker.P, .55f));
            Expect(foot.Entering == bunker, "Clicking the visible bunker assigns the rifleman's garrison order");
            var wall = Campaign.Info("wall");
            var yard = RiverbendMap.PortPoint(15, 37);
            world.Spawn("hq", 0, yard.X, yard.Y);
            var barracksSite = RiverbendMap.PortPoint(18, 38);
            world.Spawn("barracks", 0, barracksSite.X, barracksSite.Y);
            var wallSite = RiverbendMap.PortPoint(23.5f, 42.5f);
            world.RevealBasinScene(wallSite, Campaign.Tile * 8);
            view.Center(wallSite);
            Press(Key.W);
            UpdateInterface();
            var wallCard = tiles.Single(tile => tile.Type == wall);
            Expect(wallCard.Portrait.Texture != null, "The defense tab shows the wall with its faction-colored art");
            Click(wallCard.Plate.GetGlobalRect().GetCenter());
            WaitFor(() => world.Construction(wall)?.Ready == true, wall.Seconds * 2 + 8, "Prepare the wall from its card");
            UpdateInterface();
            Click(wallCard.Plate.GetGlobalRect().GetCenter());
            var site = SuggestedSite(wall, wallSite);
            Click(view.Screen(site));
            var placedWall = world.Live(0).Single(p => p.T.IsWall);
            Expect(placing == null && view.Pick(view.Screen(placedWall.P)) == placedWall, "A finished wall can be placed and hit-tested");
            selected.Clear();
            Click(view.Screen(placedWall.P));
            Expect(selected.Count == 0, "A wall is not selectable");
            VerifyEdgeScroll();
            LaunchChapter(CampaignChapter.Basin);
            WaitFor(() => sequence.CanTakeCommand, 110, "Basin opens for dossier input checks");
            TakeCommand();
            var oil = world.BasinOperation.OilDerrick;
            view.Center(oil.P);
            Click(view.Screen(oil.P));
            UpdateInterface();
            Expect(selected.SingleOrDefault() == oil && selectionText.Text.Contains("+120") &&
                view.Art.OilDerrick(0).GetSize().X > 0 &&
                view.Art.OilDerrick(0, .5f) != view.Art.OilDerrick(0),
                "The starting oil derrick is selectable, shows its income and has animated art");
            Press(Key.F9);
            Expect(basinTestPanel.IsVisibleInTree() && paused, "F9 opens the local test panel during chapter three");
            Click(basinTestReinforce.GetGlobalRect().GetCenter());
            Expect(CanControl && selected.Count == Campaign.BasinTestReinforcementCount && selected.All(unit => unit.T.Key == "arty") &&
                selected.Select(unit => unit.P).Distinct().Count() == selected.Count, "The test panel places and selects sixteen self-propelled artillery units");
            Press(Key.Escape);
            Click(basinTestEntry.GetGlobalRect().GetCenter());
            Click(basinTestRivalry.GetGlobalRect().GetCenter());
            Expect(world.Remaining(1) == 0 && !ReadingRoadOrder && CanControl, "The rivalry button awards the first order to White Stone");
            Expect(basinDefeatFeed.Visible && basinDefeatLines.Text ==
                FactionText("白石矿务警卫队 击败了 北线驻防军"),
                "The left-side feed names the actual victor and loser in faction colors");
            Press(Key.F9);
            Click(basinTestDefeat[1].GetGlobalRect().GetCenter());
            Expect(roadOrderOverlay.IsVisibleInTree() && !CanControl, "A visible dossier blocks battlefield input");
            Expect(roadOrderReference.Text.EndsWith("042") && roadOrderDirective.Text.StartsWith("换装站待发"),
                "The direct defeat button presents the order for White Stone's train at the terminus");
            FinishOrderAnimation();
            Press(Key.Enter);
            FinishOrderAnimation();
            Expect(ReadingRoadOrder && roadOrderReference.Text.EndsWith("041"), "Enter reveals the inherited North Line document");
            Click(roadOrderNext.GetGlobalRect().GetCenter());
            FinishOrderAnimation();
            Expect(CanControl && !roadOrderOverlay.Visible, "The last dossier button resumes the battle");
            GD.Print("PASS: viewport input / chapters, briefing, map, handoff, building, camera, pause, controls settings and diplomacy.");
            GetTree().Quit();

            void Press(Key key) => viewport.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }, true);

            void Click(Vector2 at, MouseButton button = MouseButton.Left)
            {
                viewport.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
                ButtonAt(at, button, true);
                ButtonAt(at, button, false);
            }

            void ButtonAt(Vector2 at, MouseButton button, bool down) =>
                viewport.PushInput(new InputEventMouseButton
                {
                    Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = down, Factor = 1
                }, true);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void VerifyEdgeScroll()
    {
        LaunchChapter(CampaignChapter.Basin);
        WaitFor(() => sequence.CanTakeCommand, 110, "Basin opens for edge scrolling");
        TakeCommand();
        view.Center(new Point(3200, 2600));
        var bounds = GetViewport().GetVisibleRect();
        var before = view.CameraPosition;
        var center = bounds.GetCenter();
        PanAtEdge(center, bounds, .16f);
        PanAtEdge(new Vector2(view.Area.End.X + 64, center.Y), bounds, .16f);
        Expect(view.CameraPosition == before, "Inside the sidebar the camera remains still");
        PanAtEdge(new Vector2(view.Area.End.X - 5, center.Y), bounds, .16f);
        Expect(view.CameraPosition.X > before.X && view.CameraPosition.Y < before.Y, "The battlefield's right edge scrolls past the sidebar");
        before = view.CameraPosition;
        PanAtEdge(new Vector2(bounds.End.X + 25, center.Y), bounds, .16f);
        Expect(view.CameraPosition.X > before.X && view.CameraPosition.Y < before.Y, "Moving beyond the right window edge still scrolls");
        before = view.CameraPosition;
        PanAtEdge(new Vector2(center.X, bounds.Position.Y - 10), bounds, .16f);
        Expect(view.CameraPosition.X < before.X && view.CameraPosition.Y < before.Y, "Moving beyond the top window edge still scrolls");
        before = view.CameraPosition;
        PanAtEdge(center, bounds, .16f);
        Expect(view.CameraPosition == before, "Returning to the middle immediately stops scrolling");
    }

    private async void RunMemoryVerification()
    {
        try
        {
            focused = false;
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            for (int run = 1; run <= 3; run++)
            {
                LaunchChapter(CampaignChapter.Basin);
                WaitFor(() => sequence.CanTakeCommand, 110, "Basin opens for memory checks");
                TakeCommand();
                long cachePeak = 0, workingPeak = 0;
                const int stepsPerDraw = 15;
                for (int frame = 0; frame < 320; frame++)
                {
                    // Visit each faction in turn so both animated buildings and its units are drawn.
                    if (frame % 8 == 0)
                    {
                        var scene = world.Home(frame / 8 % world.FactionCount);
                        world.RevealBasinScene(scene, Campaign.Tile * 14);
                        view.Center(scene);
                    }
                    for (int tick = 0; tick < stepsPerDraw; tick++)
                        Simulate((float)MatchTempo.Step);
                    view.QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    cachePeak = Math.Max(cachePeak, view.Art.CachedFrameBytes);
                    process.Refresh();
                    workingPeak = Math.Max(workingPeak, process.WorkingSet64);
                }
                Expect(cachePeak > 0 && cachePeak <= BattleArt.FrameBudget * 3 / 2,
                    "Repeated battlefield draws keep the frame cache within its budget plus the active view");
                float time = world.Time;
                ReturnToMissions();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                process.Refresh();
                Expect(!view.Visible && view.Art.CachedFrameBytes == 0, "Returning to missions releases battlefield frames");
                GD.Print($"Basin memory run {run}: {time:F0}s simulated, frame cache peak {cachePeak / 1048576.0:F1} MiB, " +
                    $"working set peak {workingPeak / 1048576.0:F1} MiB, {process.WorkingSet64 / 1048576.0:F1} MiB after returning.");
            }
            GD.Print("PASS: battlefield texture budget and release across three Basin restarts.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async void CapturePreview()
    {
        try
        {
            GetWindow().Size = new Vector2I(1472, 819);
            focused = false;
            await SaveFrame("00-menu");
            OpenArchive();
            SelectArchive(0);
            await SaveFrame("00-campaign-archive");
            CloseArchive();
            OpenFront(FrontPage.Missions);
            await SaveFrame("00-briefing");
            ResetChapter(true);
            await CaptureAt(() => world.HarborOperation.Scene == HarborScene.QuayWork && world.HarborOperation.Elapsed >= 3.55f, "01-quay");
            SetPaused(true);
            await SaveFrame("06-scene-pause");
            SetPaused(false);
            await CaptureAt(() => world.HarborOperation.Scene == HarborScene.Checkpoint && world.HarborOperation.Elapsed >= 5.05f, "02-checkpoint");
            await CaptureAt(() => world.HarborOperation.Scene == HarborScene.LowerRamp && world.HarborOperation.Elapsed >= 1.30f, "03-landing");
            WaitFor(() => sequence.CanTakeCommand, 60, "Capture handoff");
            TakeCommand();
            await SaveFrame("04-handoff");
            Deploy(world.HarborOperation.CommandVehicle);
            WaitFor(() => world.Lesson == 1, 14, "Deploy for capture");
            BuildForVerification("power", 20, 38);
            BuildForVerification("refinery", 29, 37);
            WaitFor(() => world.OreReceipts[0] >= Campaign.LoadValue, 175, "Capture first real delivery");
            view.Center(world.Home(0));
            UpdateInterface();
            await SaveFrame("05-first-ore");
            SetPaused(true);
            await SaveFrame("07-battle-pause");
            SetPaused(false);
            showObjectives = true;
            SetPaused(true);
            await SaveFrame("08-objectives");
            ReturnToMissions();
            ChooseChapter(CampaignChapter.Riverbend);
            await SaveFrame("09-riverbend-briefing");
            LaunchChapter(CampaignChapter.Riverbend);
            var river = world.RiverbendOperation;
            await CaptureAt(() => river.Elapsed >= 9.4f, "10-riverbend-front");
            WaitFor(() => sequence.CanTakeCommand, 65, "Capture Riverbend handoff");
            TakeCommand();
            await SaveFrame("11-riverbend-handoff");
            var hidden = RiverbendMap.PortPoint(74, 55);
            Expect(!world.Explored(0, hidden), "East camp remains outside explored vision");
            foreach (float delta in new[] { 0f, 3f, 17f, 41f, 73f })
            {
                view.Center(new Point(hidden.X + delta, hidden.Y - delta));
                await VerifyFogPixel(hidden);
            }
            view.Center(world.Home(0));
            SetPaused(false);
            selected.Clear();
            selected.AddRange(world.Live(0).Where(p => !p.T.Building).Take(4));
            view.Selection = selected.ToArray();
            var clickedRoad = RiverbendMap.PortPoint(36, 33);
            MapClick(view.Screen(clickedRoad), false, false, false);
            await SaveFrame("14-riverbend-orders");
            Deploy(world.RiverbendOperation.CommandVehicle);
            WaitFor(() => world.RiverbendOperation.CommandVehicle.T.Key == "hq" && world.RiverbendOperation.CommandVehicle.Deployed, 14, "Deploy for repair preview");
            var hq = world.RiverbendOperation.CommandVehicle;
            hq.Hp *= .42f;
            world.ToggleRepair(hq);
            hq.RepairAt = world.VisualTime - .06f;
            selected.Clear();
            selected.Add(hq);
            view.Selection = selected.ToArray();
            await SaveFrame("15-riverbend-repair");
            SetDiplomacy(true);
            await SaveFrame("12-riverbend-factions");
            SetDiplomacy(false);
            SetPaused(true);
            OpenKeyboard();
            await SaveFrame("13-keyboard");
            ReturnToMissions();
            ChooseChapter(CampaignChapter.Basin);
            await SaveFrame("16-basin-briefing");
            OpenMapViewer();
            await SaveFrame("19-basin-full-map");
            CloseMapViewer();
            LaunchChapter(CampaignChapter.Basin);
            await CaptureAt(() => ((BasinSequence)sequence).Elapsed >= 1.2f, "16b-basin-arrival");
            await CaptureAt(() => world.BasinOperation.ConvoyParked && world.BasinOperation.Freight.Loads == 1, "16c-basin-loading");
            for (int side = 1; side < world.FactionCount; side++)
                await CaptureAt(() => ((BasinSequence)sequence).ReconSide == side &&
                    ((BasinSequence)sequence).OpeningShade == 0, $"17-basin-checkpoint-{side}");
            WaitFor(() => sequence.CanTakeCommand, 50, "Basin handoff for preview");
            TakeCommand();
            await SaveFrame("18-basin-handoff");
            var tankSite = RiverbendMap.PortPoint(19, 118);
            var ifvSite = RiverbendMap.PortPoint(23, 117);
            world.Spawn("tank", 0, tankSite.X, tankSite.Y);
            world.Spawn("ifv", 0, ifvSite.X, ifvSite.Y);
            var comparisonCenter = HarborOperation.Glide(tankSite, world.BasinOperation.Freight.Wagon(0).Position, .5f);
            view.Center(comparisonCenter);
            await SaveFrame("18b-basin-model-comparison");
            foreach (var enemy in world.Units.Where(unit => unit.Side > 0)) enemy.Hp = 0;
            Simulate((float)MatchTempo.Step);
            UpdateInterface();
            FinishOrderAnimation();
            await SaveFrame("20-basin-road-order");
            AcknowledgeRoadOrder();
            AdvanceRoadOrder(.10f);
            await SaveFrame("20b-basin-road-order-turn");
            ReadRemainingOrders();
            await CaptureAt(() => sequence.Departing && ((BasinSequence)sequence).Elapsed >= 4.0f, "22-basin-departure");
            await CaptureAt(() => sequence.Departing && ((BasinSequence)sequence).Elapsed >= 9.0f, "21-basin-closing-radio");
            await CaptureAt(() => Finished, "23-basin-result-train");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task CaptureAt(Func<bool> condition, string name)
    {
        WaitFor(condition, 150, "Capture " + name);
        CenterSequence();
        UpdateInterface();
        await SaveFrame(name);
    }

    private async System.Threading.Tasks.Task VerifyFogPixel(Point point)
    {
        view.QueueRedraw();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var frame = GetViewport().GetTexture().GetImage();
        var pixel = view.Screen(point) / GetViewport().GetVisibleRect().Size * new Vector2(frame.GetWidth(), frame.GetHeight());
        var color = frame.GetPixel((int)pixel.X, (int)pixel.Y);
        Expect(color.R < .11f && color.G < .17f && color.B < .17f, "Unexplored terrain stays dark during camera motion");
    }

    private async System.Threading.Tasks.Task SaveFrame(string name)
    {
        view.QueueRedraw();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var folder = OS.GetEnvironment("GREYHAVEN_CAPTURE_DIR");
        if (string.IsNullOrEmpty(folder)) folder = ProjectSettings.GlobalizePath("user://");
        System.IO.Directory.CreateDirectory(folder);
        var result = image.SavePng(System.IO.Path.Combine(folder, name + ".png"));
        Expect(result == Error.Ok, "Write preview " + name + ": " + result);
    }
}
