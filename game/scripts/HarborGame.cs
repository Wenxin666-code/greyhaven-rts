using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class HarborGame : Node
{
    private readonly SimulationStepper stepper = new();
    private readonly List<Piece> selected = new();
    private readonly Dictionary<int, Piece[]> groups = new();
    private double lastTypeSelection = -10;
    private Campaign world;
    private BattlefieldView view;
    private ChapterSequence sequence;
    private CampaignChapter activeChapter = CampaignChapter.Harbor, chosenChapter = CampaignChapter.Harbor;
    private TypeInfo placing;
    private bool paused, started, focused = true, rightHeld, panned;
    private bool edgeArmed, desktopMousePosition;
    private float edgeDwell;
    private Vector2? dragStart;
    private int multiplier = 1;
    private double nextHud;
    private static readonly Color Ink = HarborSkin.Ink, Pale = HarborSkin.Pale, Gold = HarborSkin.Gold;

    public override void _Ready()
    {
        view = GetNode<BattlefieldView>("Battlefield");
        desktopMousePosition = DisplayServer.GetName() is "Windows" or "macOS" or "X11";
        string[] options = OS.GetCmdlineUserArgs();
        chapterSavesEnabled = !options.Any(option => option is "--verify" or "--verify-ui" or "--verify-saves" or "--verify-memory" or "--capture");
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestExitGame;
        if (DisplayServer.GetName() != "headless" && !options.Contains("--capture") &&
            !options.Contains("--verify-ui") && !options.Contains("--verify-memory") && !options.Contains("--windowed"))
            GetWindow().Mode = Window.ModeEnum.Fullscreen;
        GetViewport().SizeChanged += Layout;
        GetWindow().FocusEntered += () => focused = true;
        GetWindow().FocusExited += () =>
        {
            focused = false;
            CancelDrag();
            rightHeld = false;
            edgeArmed = false;
            edgeDwell = 0;
            stepper.Reset();
        };
        if (chapterSavesEnabled && DisplayServer.GetName() != "headless")
        {
            useChapterPreload = true;
            ShowStartupLogo();
            return;
        }

        MakeFrontInterface();
        MakeBattleInterface();
        ResetChapter(false);
        if (options.Contains("--verify"))
        {
            CallDeferred(MethodName.RunVerification);
        }
        else if (options.Contains("--verify-ui"))
        {
            CallDeferred(MethodName.RunInputVerification);
        }
        else if (options.Contains("--verify-saves"))
        {
            CallDeferred(MethodName.RunSaveVerification);
        }
        else if (options.Contains("--verify-memory"))
        {
            CallDeferred(MethodName.RunMemoryVerification);
        }
        else if (options.Contains("--capture"))
        {
            CallDeferred(MethodName.CapturePreview);
        }
    }

    private void ResetChapter(bool play, CampaignChapter chapter = null, ChapterProgress progress = null)
    {
        paused = false;
        edgeArmed = false;
        edgeDwell = 0;
        viewingMap = false;
        keyboardOpen = false;
        diplomacyOpen = false;
        basinTestOpen = false;
        ChangeRoadOrderPhase(RoadOrderPhase.Hidden);
        capturingKey = physicalRouteKey = Key.None;
        showObjectives = false;
        frontPage = FrontPage.Menu;
        started = play;
        queueOpen = false;
        structureTool = StructureTool.None;
        activeTab = 0;
        seenCards.Clear();
        battleSeconds = 0;
        multiplier = 1;
        placing = null;
        selected.Clear();
        groups.Clear();
        lastGroup = -1;
        plannedRoutes.Clear();
        plannedLoops.Clear();
        beaconMode = routePinned = routeHeld = false;
        routePointOwner = null;
        routePointIndex = selectedBeacon = -1;
        lastTypeSelection = -10;
        stepper.Reset();
        activeChapter = chapter ?? (play ? chosenChapter : CampaignChapter.Harbor);
        world = progress?.World ?? activeChapter.Open();
        if (play) world.PrepareBattleNavigation();
        sequence = progress?.Sequence ?? activeChapter.CreateSequence(world);
        view.Cinematic = !play || SceneRunning || sequence.Complete;
        view.FogStrength = view.Cinematic ? sequence.Fog : 1;
        view.SetWorld(world, play);
        radar.World = world;
        Layout();
        if (progress != null) RestorePlayerView(progress);
        else CenterSequence();
        UpdateInterface();
    }

    private bool SceneRunning => sequence.Opening || sequence.Departing;
    private bool Finished => sequence.ResultReady || world.Lost && world.OutcomeReady;
    private bool WaitingForDeparture => world.Won && !SceneRunning && !sequence.Complete;
    private bool CanControl => started && !paused && exitConfirmation == null && !SceneRunning && !ReadingRoadOrder &&
        !world.Lost && !Finished && !WaitingForDeparture;

    public override void _Process(double elapsed)
    {
        if (world == null || chapterLoading != null || exitConfirmation != null) return;
        if (ReadingRoadOrder) AdvanceRoadOrder((float)Math.Min(elapsed, .08));
        else if (started && focused && !paused)
        {
            double previous = world.VisualTime;
            bool sceneFrame = SceneRunning || world.Won || world.Lost;
            stepper.Advance(elapsed, MatchTempo.DefaultRate * (sceneFrame ? 1 : multiplier), Simulate);
            if (ReadingRoadOrder)
            {
                stepper.Reset();
                CancelDrag();
                rightHeld = false;
                UpdateRoadOrderPanel();
            }
            if (SceneRunning || sequence.Complete) CenterSequence();
            view.FogStrength = SceneRunning || sequence.Complete ? sequence.Fog : 1;
            if (CanControl)
            {
                battleSeconds += elapsed;
                PollCamera((float)Math.Min(elapsed, .12));
                var mouse = view.GetGlobalMousePosition();
                view.Placement = placing == null || !OverBattlefield(mouse) ? null
                    : world.AssessPlacement(placing, view.Ground(mouse), requireBase: true);
                view.PlacementRange = view.Placement != null && placing.Defense
                    ? placing.Key == "bunker" ? ReferenceBalance.WUCPara.Range : placing.Range : 0;
            }
            if (world.VisualTime != previous || CanControl) view.QueueRedraw();
        }

        float cut = started && sequence is BasinSequence basin ? basin.OpeningShade : 0;
        basinCut.Visible = cut > .001f;
        if (basinCut.Visible) basinCut.Color = new Color("#111617", cut);
        selected.RemoveAll(p => !p.Alive || p.Carrier != null);
        view.Selection = selected.ToArray();
        UpdatePointer(elapsed);
        var cine = !started || SceneRunning || sequence.Complete;
        if (cine != view.Cinematic)
        {
            view.Cinematic = cine;
            Layout();
        }

        nextHud -= elapsed;
        if (nextHud > 0) return;
        nextHud = .1;
        UpdateInterface();
    }

    private void Simulate(float step)
    {
        if (ReadingRoadOrder) return;
        if (SceneRunning || sequence.Complete)
        {
            sequence.Tick(step);
            return;
        }
        if (world.Won)
        {
            world.TickVisuals(step);
            if (sequence.BeginDeparture(view.CameraPosition))
            {
                selected.Clear();
                placing = null;
                view.Placement = null;
            }
            return;
        }
        if (!world.Lost) world.Tick(step);
        else if (!world.OutcomeReady) world.TickVisuals(step);
    }

    private void TakeCommand()
    {
        if (paused || !sequence.TakeCommand()) return;
        selected.Clear();
        if (sequence.CommandVehicle.Deployed) selected.Add(sequence.CommandVehicle);
        view.Selection = selected.ToArray();
        view.Cinematic = false;
        view.FogStrength = 1;
        Layout();
        view.Center(CommandCamera(), BattlefieldView.DefaultZoom);
        UpdateInterface();
        view.QueueRedraw();
    }

    private void CenterSequence() => view.Center(sequence.Camera(),
        sequence is BasinSequence basin ? basin.CameraZoom : null);

    private Point CommandCamera() => world.FirstChapter ? world.Home(0) : sequence.CommandVehicle.P;

    private void PollCamera(float dt)
    {
        if (dragStart.HasValue || rightHeld)
        {
            edgeDwell = 0;
            return;
        }
        float x = (Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
        float y = (Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
        if (x != 0 || y != 0)
        {
            edgeDwell = 0;
            view.Pan(new Vector2(x, y).Normalized() * (660 * dt));
            return;
        }
        var viewport = GetViewport();
        var mouse = desktopMousePosition
            ? viewport.GetScreenTransform().AffineInverse() * (Vector2)(DisplayServer.MouseGetPosition() -
                DisplayServer.WindowGetPosition(GetWindow().GetWindowId()))
            : viewport.GetMousePosition();
        PanAtEdge(mouse, viewport.GetVisibleRect(), dt);
    }

    private void PanAtEdge(Vector2 mouse, Rect2 viewport, float dt)
    {
        edgeArmed |= viewport.HasPoint(mouse);
        if (!edgeArmed) return;
        const float rim = 3;
        var direction = new Vector2(mouse.X < viewport.Position.X + rim ? -1 : mouse.X >= viewport.End.X - rim ? 1 : 0,
            mouse.Y < viewport.Position.Y + rim ? -1 : mouse.Y >= viewport.End.Y - rim ? 1 : 0);
        if (direction == Vector2.Zero && OverBattlefield(mouse))
        {
            var area = view.Area;
            direction = new Vector2(EdgeSpeed(mouse.X - area.Position.X, area.Size.X),
                EdgeSpeed(mouse.Y - area.Position.Y, area.Size.Y));
        }
        if (direction == Vector2.Zero)
        {
            edgeDwell = 0;
            return;
        }
        edgeDwell += dt;
        float ramp = Mathf.Clamp((edgeDwell - .035f) / .11f, 0, 1);
        if (ramp > 0) view.Pan(direction.LimitLength() * (740 * dt * ramp));
    }

    private static float EdgeSpeed(float position, float length)
    {
        const float band = 28;
        return position < band ? position / band - 1 : Math.Max(0, (position - length + band) / band);
    }

    public override void _Input(InputEvent input)
    {
        if (exitConfirmation != null)
        {
            if (exitFadeStarted || input is InputEventKey { Keycode: Key.Escape })
            {
                if (!exitFadeStarted && input is InputEventKey { Pressed: true, Echo: false }) CancelExitGame();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (startupLogo != null || chapterLoading != null)
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (ReadingRoadOrder && input is InputEventKey dossierKey)
        {
            if (dossierKey.Pressed && !dossierKey.Echo && dossierKey.Keycode == Key.Enter) AcknowledgeRoadOrder();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (diplomacyOpen && input is InputEventKey { Pressed: true, Echo: false } diplomacyKey)
        {
            if (diplomacyKey.Keycode is Key.Escape or Key.Enter || BoundAction(diplomacyKey.PhysicalKeycode) == Key.Tab)
                SetDiplomacy(false);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (keyboardOpen && input is InputEventKey { Pressed: true, Echo: false } binding)
        {
            CaptureBinding(binding);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventKey { Pressed: true, Echo: false } testKey &&
            (basinTestOpen && testKey.Keycode == Key.Escape || testKey.Keycode == Key.F9 &&
             !testKey.CtrlPressed && !testKey.ShiftPressed && !testKey.AltPressed &&
             BoundAction(Key.F9) == Key.None && CanOpenBasinTest))
        {
            SetBasinTest(!basinTestOpen);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (basinTestOpen && input is InputEventKey { Keycode: Key.Enter })
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventKey { Pressed: false } release && routeHeld && release.PhysicalKeycode == physicalRouteKey)
        {
            if (CanControl && !routePinned) CommitRoute();
            routeHeld = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.F11 || key.AltPressed && key.Keycode == Key.Enter))
        {
            var window = GetWindow();
            window.Mode = window.Mode == Window.ModeEnum.Fullscreen ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (ReadingArchive && input is InputEventKey { Pressed: true, Echo: false } archiveKey)
        {
            switch (archiveKey.Keycode)
            {
                case Key.Escape: CloseArchive(); break;
                case Key.Left when archiveIndex % ArchiveColumns != 0: SelectArchive(archiveIndex - 1); break;
                case Key.Right when archiveIndex % ArchiveColumns == 0: SelectArchive(archiveIndex + 1); break;
                case Key.Up when archiveIndex >= ArchiveColumns: SelectArchive(archiveIndex - ArchiveColumns); break;
                case Key.Down when archiveIndex + ArchiveColumns < archiveCards.Length: SelectArchive(archiveIndex + ArchiveColumns); break;
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventKey state && state.Pressed && !state.Echo && started &&
            (state.Keycode == Key.Escape || state.Keycode == Key.Enter && (paused || sequence.CanTakeCommand)))
        {
            if (state.Keycode == Key.Escape) SetPaused(!paused);
            else if (paused) SetPaused(false);
            else TakeCommand();
            GetViewport().SetInputAsHandled();
            return;
        }
        // Keep pointer capture across the sidebar while a map gesture is active.
        if (input is InputEventKey frontKey && frontKey.Pressed && !frontKey.Echo && !started)
        {
            if (viewingMap && frontKey.Keycode is Key.Escape or Key.Enter) CloseMapViewer();
            else if (frontPage == FrontPage.Rules && frontKey.Keycode is Key.Left or Key.Right)
                ShowManualPage(manualIndex + (frontKey.Keycode == Key.Right ? 1 : -1));
            else if (frontKey.Keycode == Key.Enter) { if (frontPage == FrontPage.Missions) OpenChosenChapter(); else OpenFront(FrontPage.Missions); }
            else if (frontKey.Keycode == Key.Escape) { if (frontPage != FrontPage.Menu) OpenFront(FrontPage.Menu); }
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!CanControl || input is not InputEventMouse mouse || !rightHeld && !dragStart.HasValue) return;
        if (input is InputEventMouseMotion motion)
        {
            if (rightHeld && (panned || motion.Position.DistanceTo(pointerStart) >= 5))
            {
                panned = true;
                view.Pan(-motion.Relative);
            }
            if (dragStart is Vector2 anchor) view.DragBox = new Rect2(anchor, mouse.Position - anchor).Abs();
        }
        else if (input is InputEventMouseButton button && !button.Pressed)
        {
            if (button.ButtonIndex == MouseButton.Right && rightHeld)
            {
                rightHeld = false;
                if (!panned) CancelMapMode();
            }
            else if (button.ButtonIndex == MouseButton.Left && dragStart is Vector2 start)
            {
                var end = mouse.Position;
                var box = new Rect2(start, end - start).Abs();
                CancelDrag();
                if (box.Size.Length() > 8 && placing == null)
                    BoxSelect(box, button.ShiftPressed);
                else if (OverBattlefield(end))
                    MapClick(end, button.ShiftPressed, button.CtrlPressed, button.AltPressed);
                UpdateCommands();
            }
        }
        GetViewport().SetInputAsHandled();
        view.QueueRedraw();
    }

    private Vector2 pointerStart;

    public override void _UnhandledInput(InputEvent input)
    {
        if (!CanControl) return;
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (HandleKey(key)) GetViewport().SetInputAsHandled();
            return;
        }
        if (input is not InputEventMouseButton button || !button.Pressed || !OverBattlefield(button.Position)) return;
        if (button.ButtonIndex == MouseButton.Left && button.DoubleClick && !button.ShiftPressed && !button.CtrlPressed &&
            !button.AltPressed && placing == null && structureTool == StructureTool.None && !PlanningRoute && !beaconMode &&
            view.Pick(button.Position) is { Side: 0 } facility && facility.T.Key is "barracks" or "factory" && world.SetPrimary(facility))
        {
            selected.Clear();
            selected.Add(facility);
            UpdateInterface();
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (button.ButtonIndex)
        {
            case MouseButton.Left: dragStart = button.Position; break;
            case MouseButton.Right: rightHeld = true; panned = false; pointerStart = button.Position; break;
            case MouseButton.WheelUp: view.AdjustZoom(button.Position, 1.10f); break;
            case MouseButton.WheelDown: view.AdjustZoom(button.Position, 1 / 1.10f); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
        view.QueueRedraw();
    }

    private bool HandleKey(InputEventKey key)
    {
        int digit = (int)key.PhysicalKeycode - (int)Key.Key0;
        if (digit is >= 0 and <= 9)
        {
            SelectGroup(digit, key);
            UpdateInterface();
            return true;
        }
        switch (BoundAction(key.PhysicalKeycode))
        {
            case Key.D: DeploySelection(); break;
            case Key.S: foreach (var unit in selected) world.Halt(unit); break;
            case Key.G: GuardSelection(); break;
            case Key.Z: routeHeld = true; physicalRouteKey = key.PhysicalKeycode; view.Previews = plannedRoutes; break;
            case Key.B: ToggleBeacon(); break;
            case Key.Tab: SetDiplomacy(true); break;
            case Key.Delete: RemoveMapMark(); break;
            case Key.M: CycleMobile(-1); break;
            case Key.N: CycleMobile(1); break;
            case Key.X: world.Scatter(selected.Where(p => !p.T.Building && p.Deployed).ToArray()); break;
            case Key.H: view.Center(world.Live(0).FirstOrDefault(p => p.T.Key == "hq" || p.T.Key == "mcv")?.P ?? world.Home(0)); break;
            case Key.Q: SetTab(0); break;
            case Key.W: SetTab(1); break;
            case Key.E: SetTab(2); break;
            case Key.R: SetTab(3); break;
            case Key.K: ToggleStructureTool(StructureTool.Repair); break;
            case Key.L: ToggleStructureTool(StructureTool.Sell); break;
            case Key.Space: if (world.LastEvent is Point eventPoint) view.Center(eventPoint); break;
            case Key.T: SelectMatchingType(); break;
            case Key.P:
                selected.Clear();
                selected.AddRange(world.Live(0).Where(p => !p.T.Building && world.Armed(p)));
                break;
            default: return false;
        }
        UpdateInterface();
        return true;
    }

    private void SelectGroup(int digit, InputEventKey key)
    {
        if (key.CtrlPressed) SaveGroup(digit);
        else RecallGroup(digit, key.ShiftPressed, key.AltPressed);
    }

    private void CycleMobile(int direction)
    {
        var units = world.Live(0).Where(IsMobile).OrderBy(p => p.Id).ToArray();
        if (units.Length == 0) return;
        int current = Array.IndexOf(units, selected.FirstOrDefault());
        int next = current < 0 ? (direction > 0 ? 0 : units.Length - 1)
            : (current + direction + units.Length) % units.Length;
        selected.Clear();
        selected.Add(units[next]);
    }

    private void SelectMatchingType()
    {
        if (selected.Count == 0) return;
        double now = Time.GetTicksMsec() / 1000.0;
        bool wholeMap = now - lastTypeSelection < .35;
        lastTypeSelection = now;
        var types = selected.Select(p => p.T).ToHashSet();
        selected.Clear();
        selected.AddRange(world.Live(0).Where(p => types.Contains(p.T) &&
            (wholeMap || view.Area.HasPoint(view.Screen(p.P)))));
    }

    private TypeInfo ReadyConstruction(int category)
    {
        var construction = world.Construction(0, category);
        return construction?.Ready == true ? construction.Type : null;
    }

    private void SetSpeed(int value)
    {
        multiplier = value;
        stepper.Reset();
    }

    private void Deploy(Piece unit)
    {
        switch (unit.T.Key)
        {
            case "mcv": world.Deploy(unit); break;
            case "hq": world.Relocate(unit, unit.P); break;
            case "ifv": world.Load(unit); break;
            default:
                if (unit.T.Garrison) world.EvacuateHouse(unit);
                else world.ToggleInfantry(unit);
                break;
        }
    }

    private void BoxSelect(Rect2 box, bool add)
    {
        if (!add) selected.Clear();
        foreach (var unit in world.Live(0).Where(p => !p.T.Building && box.Intersects(view.PickBody(p))))
            if (!selected.Contains(unit)) selected.Add(unit);
    }

    private void MapClick(Vector2 click, bool shift, bool ctrl, bool alt)
    {
        var ground = view.Ground(click);
        if (structureTool != StructureTool.None)
        {
            var building = view.Pick(click);
            if (structureTool == StructureTool.Repair) world.ToggleRepair(building);
            else world.Sell(building);
            return;
        }
        if (placing != null)
        {
            if (world.Build(placing, ground)) placing = null;
            return;
        }

        var hit = PickOrderTarget(click, shift, ctrl, alt);
        IssueMapCommand(click, ground, hit, shift, ctrl, alt);
    }

    private Piece PickOrderTarget(Vector2 position, bool shift, bool ctrl, bool alt) => view.Pick(position,
        !shift && !ctrl && !alt && selected.Count > 0 && selected.All(p => p.T.Key == "miner"),
        !shift && !ctrl && !alt && selected.Any(p => p.Deployed && p.T.Foot && p.T.Key != "sniper"));

    private void IssueMapCommand(Vector2 click, Point ground, Piece hit, bool shift, bool ctrl, bool alt)
    {
        if (RouteOrBeaconClick(click, ground, hit, shift)) return;
        if (ctrl && alt)
        {
            foreach (var unit in selected)
                if (hit?.Side == 0) world.EscortUnit(unit, hit);
                else world.GuardAt(unit, ground);
            return;
        }
        if (alt)
        {
            var destination = ForceMoveDestination(ground, hit);
            foreach (var unit in selected)
                if (world.Move(unit, destination, false)) view.ShowMoveOrder(unit, destination);
            return;
        }
        if (ctrl && !shift)
        {
            if (hit?.T.IsWall == true && !selected.Any(p => p.Deployed && world.CanAttackTarget(p, hit))) return;
            foreach (var unit in selected)
                if (hit != null && hit != unit) world.Attack(unit, hit, true);
                else world.Suppress(unit, ground);
            Mark(ground, hit);
            return;
        }

        if (IssueContext(hit, ground, shift, ctrl)) return;
        var movers = selected.Where(p => p.Deployed && !p.T.Building && world.CanMoveTo(p, ground)).ToArray();
        if (world.MarchTo(movers, ground, ctrl && shift, true, unit => view.ShowMoveOrder(unit, ground)) > 0) Mark(ground);
        else if (selected.Count > 0) RejectDestination();
    }

    private static Point ForceMoveDestination(Point ground, Piece hit) => hit is { Side: > 0, Deployed: true } && hit.T.Foot
        ? hit.P : ground;

    private bool IssueContext(Piece hit, Point ground, bool shift, bool advance)
    {
        if (hit?.T.IsWall == true)
        {
            if (hit.Side == 0) return true;
            if (hit.Side < 0) return true;
            var breakers = selected.Where(p => p.Deployed && world.CanAttackTarget(p, hit)).ToArray();
            foreach (var unit in breakers) world.Attack(unit, hit);
            if (breakers.Length > 0) Mark(hit.P, hit);
            return true;
        }
        if (!shift && !advance && selected.Count > 0 && selected.All(p => p.T.Key == "miner"))
        {
            var ore = hit == null && world.Explored(0, ground) ? world.OreAt(ground) : null;
            var plant = hit?.T.Key == "refinery" && hit.Side == 0 && hit.Deployed ? hit : null;
            if (ore != null || plant != null)
            {
                var destination = plant == null ? ore.P : Campaign.DockPoint(plant);
                var available = selected.Where(truck => world.CanMoveTo(truck, destination)).ToArray();
                if (available.Length == 0) RejectDestination();
                else foreach (var truck in available) world.MineOrder(truck, ore, plant);
                return true;
            }
        }

        if (shift && !advance && hit?.Side == 0)
        {
            if (!selected.Remove(hit)) selected.Add(hit);
            return true;
        }

        if (hit?.Side > 0)
        {
            var fighters = selected.Where(p => p.Deployed && world.Armed(p)).ToArray();
            if (!shift && !advance && fighters.Length == 0 && selected.FirstOrDefault(p => world.CanEngineer(p, hit)) is { } engineer)
            {
                if (!world.EngineerOrder(engineer, hit)) RejectDestination();
                return true;
            }
            if (fighters.Length > 0 && !Campaign.IsRelayStation(hit))
            {
                foreach (var unit in fighters) world.Attack(unit, hit);
                Mark(hit.P, hit);
            }
            return true;
        }

        if (hit != null && !advance)
        {
            foreach (var unit in selected.ToArray())
            {
                if (world.CanEngineer(unit, hit)) { if (!world.EngineerOrder(unit, hit)) RejectDestination(); return true; }
                if (world.CanBoard(unit, hit)) { if (!world.Board(unit, hit)) RejectDestination(); return true; }
                if (world.CanRepairVehicle(unit, hit)) { world.RepairOrder(unit, hit); return true; }
            }
            if (hit.T.Garrison && hit.Side == 0 && selected.Any(p => world.CanEnterHouse(p, hit)))
            {
                int entered = selected.Count(p => world.EnterHouse(p, hit));
                if (entered == 0) world.Radio = "没有空闲的驻防入口。";
                return true;
            }
            if (hit.T.Garrison && hit.Side < 0 && selected.Any(p => p.T.Foot))
            {
                world.Radio = "需要空闲工兵才能占领中立房屋。";
                return true;
            }

            if (hit.Side == 0)
            {
                if (selected.Count == 1 && selected[0] == hit)
                {
                    if (hit.T.Key == "mcv" || hit.T.DeployableFoot ||
                        hit.T.Key == "ifv" && hit.Passenger != null || hit.T.Garrison && world.HouseCount(hit) > 0)
                        Deploy(hit);
                    else selected.Clear();
                }
                else
                {
                    selected.Clear();
                    selected.Add(hit);
                }
                return true;
            }
        }

        if (hit == null && selected.Count == 1 && world.CanSetRally(selected[0]))
        {
            if (!world.SetRally(selected[0], ground)) world.Radio = "集结点不可达，已保留原集结点。";
            return true;
        }
        return false;
    }

    private void Mark(Point ground, Piece target = null)
    {
        view.Mark = ground;
        view.MarkedTarget = target;
        view.MarkTime = world.VisualTime;
    }

    private void CancelDrag()
    {
        dragStart = null;
        view.DragBox = null;
    }

    private void SetPaused(bool value)
    {
        paused = value;
        if (!value) showObjectives = false;
        CancelDrag();
        rightHeld = false;
        stepper.Reset();
        UpdateInterface();
    }

    private void ProductionClick(TypeInfo type, bool alternate, int count = 1)
    {
        if (!CanControl) return;
        if (type.Building)
        {
            if (alternate) { world.HoldConstruction(type); if (placing == type && world.Construction(type) == null) placing = null; }
            else if (world.Construction(type)?.Ready == true) { placing = type; structureTool = StructureTool.None; }
            else world.StartConstruction(type);
        }
        else if (!alternate)
        {
            var outlet = CardProducer(type);
            if (outlet != null && outlet.ProductionPaused && outlet.Queue.Count > 0 && outlet.Queue.Peek() == type)
                outlet.ProductionPaused = false;
            else for (int i = 0; i < count; i++)
                if (!world.Train(type, 0, selected.FirstOrDefault())) break;
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                var outlet = CardProducer(type);
                if (outlet == null) break;
                int slot = Array.LastIndexOf(outlet.Queue.ToArray(), type);
                if (slot < 0) break;
                if (slot == 0 && !outlet.ProductionPaused)
                {
                    outlet.ProductionPaused = true;
                    world.Radio = "生产已暂停，左键继续，右键取消。";
                    break;
                }
                world.CancelOrder(outlet, slot);
            }
        }
        UpdateInterface();
    }

    private void AcceptMessage()
    {
        if (Finished) ReturnToMissions();
        else TakeCommand();
    }

    private void RadarOrder(Point point, InputEventMouseButton click)
    {
        if (!CanControl || !world.Battlefield.Contains(point)) return;
        if (click.ButtonIndex == MouseButton.Right || selected.Count == 0)
        {
            view.Center(point);
            view.QueueRedraw();
            return;
        }
        var target = world.Units.Where(p => p.Alive && p.Carrier == null && world.Visible(p) &&
            (p.T.Building ? Campaign.Footprint(p.T, p.P).Contains(point) : Campaign.Dist(p.P, point) < 15))
            .OrderBy(p => Campaign.Dist(p.P, point)).FirstOrDefault();
        IssueMapCommand(view.Screen(point), point, target, click.ShiftPressed, click.CtrlPressed, click.AltPressed);
    }

    private void LocateLesson()
    {
        if (!CanControl) return;
        var key = world.LessonKey;
        if (key == "squad") SetTab(2);
        else if (key == "ifv") SetTab(3);
        else if (IsBuildingLesson) SetTab(0);
        var origin = world.Home(0);
        if (key == "eliminate")
        {
            selected.Clear();
            selected.AddRange(world.Live(0).Where(p => !p.T.Building && world.Armed(p)));
            origin = RiverbendMap.PortPoint(52, 30);
        }
        else if (key is "ore" or "receipt")
        {
            var truck = world.Live(0).FirstOrDefault(p => p.T.Key == "miner");
            if (truck != null) origin = truck.P;
        }
        else if (key == "hq")
        {
            var vehicle = world.Live(0).FirstOrDefault(p => p.T.Key == "mcv");
            if (vehicle != null)
            {
                selected.Clear();
                selected.Add(vehicle);
                origin = vehicle.P;
            }
        }
        else
        {
            var type = Campaign.Info(key);
            var building = type.Building ? type : Campaign.Info(type.Producer);
            var existing = world.Live(0).FirstOrDefault(p => p.T == building);
            var preferred = key switch
            {
                "power" => RiverbendMap.PortPoint(20, 38), "refinery" => RiverbendMap.PortPoint(29, 37),
                "barracks" => RiverbendMap.PortPoint(23, 43), "factory" => RiverbendMap.PortPoint(28, 43),
                _ => origin
            };
            var suggested = existing?.P ?? SuggestedSite(building, preferred);
            if (!suggested.IsEmpty) origin = suggested;
        }
        view.Center(origin);
        Mark(origin);
        view.LessonFocus = origin;
        view.QueueRedraw();
    }
}
