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
        Expect(Finished && !nextChapter.Visible && resultMission.Text == "河湾镇", "Chapter two result returns to selection");
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
            Click(new Vector2(170, 641));
            Expect(rulesPanel.Visible && manualPages[0].Visible, "Field manual opens");
            Click(manualTabs[1].GetGlobalRect().GetCenter());
            Expect(manualPages[1].Visible && !manualPages[0].Visible, "Manual switches to unit roster");
            viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            Click(new Vector2(200, 575));
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
            GD.Print("PASS: viewport input / chapters, briefing, map, handoff, building, camera, pause, controls settings and diplomacy.");
            GetTree().Quit();

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

    private async void CapturePreview()
    {
        try
        {
            GetWindow().Size = new Vector2I(1472, 819);
            focused = false;
            await SaveFrame("00-menu");
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
        view.Center(sequence.Camera());
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
