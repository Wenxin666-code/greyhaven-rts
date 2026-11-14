using Godot;
using Greyhaven;
using System.Collections.Generic;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private Control commandBar, commandActions;
    private Button collapseCommands, teamOne, teamTwo, sameType, deployCommand, guardCommand, routeCommand, beaconCommand;
    private bool commandsOpen = true, routePinned, routeHeld, beaconMode;
    private int lastGroup = -1;
    private ulong lastGroupRecall;
    private readonly Dictionary<Piece, List<Point>> plannedRoutes = new();
    private readonly Dictionary<Piece, int> plannedLoops = new();
    private Piece routePointOwner;
    private int routePointIndex = -1, selectedBeacon = -1;

    private bool PlanningRoute => routePinned || routeHeld;

    private void MakeCommandBar()
    {
        commandBar = SteelPanel(ui, 4, 772, 385, 43);
        collapseCommands = AddButton(commandBar, "‹", 4, 6, 24, 30, () =>
        {
            commandsOpen = !commandsOpen;
            UpdateInterface();
        });
        collapseCommands.TooltipText = "收起或展开快捷指令";
        commandActions = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(35, 4) };
        commandBar.AddChild(commandActions);
        teamOne = AddCommand(0, "1", "编组 1 · 左键创建或召回，右键清除", () => ClickGroup(1), 1);
        teamTwo = AddCommand(1, "2", "编组 2 · 左键创建或召回，右键清除", () => ClickGroup(2), 2);
        sameType = AddCommand(2, "type", "同类选择 [T]", SelectMatchingType);
        deployCommand = AddCommand(3, "deploy", "部署／收起／卸载 [D]", DeploySelection);
        guardCommand = AddCommand(4, "guard", "原地警戒 [G]", GuardSelection);
        routeCommand = AddCommand(5, "route", "路径规划 [Z] · 再点执行", () =>
        {
            if (PlanningRoute) CommitRoute();
            else
            {
                routePinned = true;
                view.Previews = plannedRoutes;
                world.Radio = "路径规划：可为多支部队设点，再点路径按钮执行。";
            }
        });
        beaconCommand = AddCommand(6, "beacon", "战场标记 · Delete 删除选中的标记", ToggleBeacon);
    }

    private void ToggleBeacon()
    {
        beaconMode = !beaconMode;
        placing = null;
        structureTool = StructureTool.None;
        world.Radio = beaconMode ? "点击地图放置战场标记。" : "已退出标记模式。";
    }

    private Button AddCommand(int column, string icon, string hint, System.Action action, int group = 0)
    {
        var button = AddButton(commandActions, group == 0 ? "" : icon, column * 48, 0, 44, 35, () =>
        {
            action();
            UpdateInterface();
        }, 17, compact: true);
        button.TooltipText = hint;
        if (group == 0)
        {
            var graphic = new TextureRect
            {
                Texture = GD.Load<Texture2D>($"res://assets/commands/{icon}.svg"),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            button.AddChild(graphic);
            Position(graphic, 10, 5, 24, 24);
        }
        else
        {
            button.GuiInput += input =>
            {
                if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
                groups.Remove(group);
                button.AcceptEvent();
                UpdateInterface();
            };
        }
        return button;
    }

    private void UpdateCommands()
    {
        commandBar.Visible = started && !SceneRunning && !Finished && !WaitingForDeparture;
        if (!CanControl) return;
        commandActions.Visible = commandsOpen;
        commandBar.Position = new Vector2(4, commandsOpen ? 772 : 787);
        commandBar.Size = new Vector2(commandsOpen ? 385 : 32, commandsOpen ? 43 : 28);
        collapseCommands.Text = commandsOpen ? "‹" : "›";
        teamOne.Disabled = !HasGroup(1) && !selected.Any(IsMobile);
        teamTwo.Disabled = !HasGroup(2) && !selected.Any(IsMobile);
        sameType.Disabled = selected.Count == 0;
        deployCommand.Disabled = !selected.Any(CanDeployFromBar);
        guardCommand.Disabled = !selected.Any(IsMobile);
        SetLit(teamOne, HasGroup(1));
        SetLit(teamTwo, HasGroup(2));
        SetLit(routeCommand, PlanningRoute);
        SetLit(beaconCommand, beaconMode);
        foreach (var button in new[] { sameType, deployCommand, guardCommand })
            button.GetChild<Control>(0).Modulate = button.Disabled ? new Color("#7d918c") : Colors.White;
    }

    private static bool IsMobile(Piece unit) => unit.Deployed && !unit.T.Building;
    private bool HasGroup(int number) => groups.TryGetValue(number, out var units) && units.Any(p => p.Deployed);

    private void ClickGroup(int number)
    {
        if (HasGroup(number)) RecallGroup(number, false, false);
        else SaveGroup(number);
    }

    private void SaveGroup(int number)
    {
        var units = selected.Where(IsMobile).ToArray();
        if (units.Length == 0) return;
        foreach (int other in groups.Keys.ToArray())
            groups[other] = groups[other].Except(units).ToArray();
        groups[number] = units;
    }

    private void RecallGroup(int number, bool add, bool center)
    {
        if (!HasGroup(number)) return;
        var ready = groups[number].Where(p => p.Deployed).ToArray();
        if (!center)
        {
            if (!add) selected.Clear();
            selected.AddRange(ready.Where(p => !selected.Contains(p)));
        }
        ulong now = Time.GetTicksMsec();
        if (center || !add && lastGroup == number && now - lastGroupRecall < 400)
            view.Center(new Point(ready.Average(p => p.P.X), ready.Average(p => p.P.Y)));
        lastGroup = number;
        lastGroupRecall = now;
    }

    private bool CanDeployFromBar(Piece unit) => unit.Deployed &&
        (unit.T.DeployableFoot || unit.T.Key is "mcv" or "hq" ||
         unit.T.Garrison && world.HouseCount(unit) > 0 || unit.T.Key == "ifv" && unit.Passenger != null);

    private void DeploySelection()
    {
        foreach (var unit in selected.Where(CanDeployFromBar).ToArray()) Deploy(unit);
    }

    private void GuardSelection()
    {
        foreach (var unit in selected.Where(IsMobile)) world.Guard(unit);
        world.Radio = "警戒此处：迎敌后返回驻守位置。";
    }

    private bool RouteOrBeaconClick(Vector2 cursor, Point ground, Piece target, bool add)
    {
        if (beaconMode)
        {
            if (!world.Battlefield.Contains(ground)) return true;
            selectedBeacon = view.Beacons.Count;
            view.Beacons.Add(ground);
            view.SelectedBeacon = selectedBeacon;
            beaconMode = false;
            world.Radio = "战场标记已放置。";
            return true;
        }
        if (!PlanningRoute)
        {
            selectedBeacon = view.Beacons.FindLastIndex(p => view.Screen(p).DistanceTo(cursor) < 13);
            view.SelectedBeacon = selectedBeacon;
            return selectedBeacon >= 0 && target == null;
        }
        foreach (var (unit, points) in plannedRoutes)
        {
            int index = points.FindIndex(p => view.Screen(p).DistanceTo(cursor) < 10);
            if (index < 0) continue;
            routePointOwner = unit;
            routePointIndex = index;
            if (selected.Contains(unit) && index < points.Count - 1) plannedLoops[unit] = index;
            return true;
        }
        if (target?.Side == 0 && IsMobile(target))
        {
            if (!add) selected.Clear();
            if (!selected.Contains(target)) selected.Add(target);
            return true;
        }
        var movers = selected.Where(unit => world.CanMoveTo(unit, ground)).ToArray();
        if (movers.Length == 0) RejectDestination();
        foreach (var unit in movers)
        {
            if (!plannedRoutes.TryGetValue(unit, out var points))
            {
                points = new List<Point>();
                plannedRoutes.Add(unit, points);
            }
            points.Add(ground);
        }
        return true;
    }

    private void CommitRoute()
    {
        int queued = plannedRoutes.Count(pair => world.SetWaypoints(pair.Key, pair.Value.ToArray(),
            plannedLoops.ContainsKey(pair.Key), plannedLoops.GetValueOrDefault(pair.Key)));
        world.Radio = queued > 0 ? $"已派出 {queued} 个单位执行规划路线。" : "没有可执行的路径点，保留原命令。";
        routePinned = routeHeld = false;
        plannedRoutes.Clear();
        plannedLoops.Clear();
        routePointOwner = null;
        routePointIndex = -1;
        view.Previews = null;
        UpdateInterface();
    }

    private void RemoveMapMark()
    {
        if (PlanningRoute && routePointOwner != null && routePointIndex >= 0 &&
            plannedRoutes.TryGetValue(routePointOwner, out var points) && routePointIndex < points.Count)
        {
            points.RemoveAt(routePointIndex);
            if (points.Count == 0) plannedRoutes.Remove(routePointOwner);
            plannedLoops.Remove(routePointOwner);
            routePointIndex = -1;
        }
        else if (selectedBeacon >= 0)
        {
            view.Beacons.RemoveAt(selectedBeacon);
            view.SelectedBeacon = selectedBeacon = -1;
        }
    }
}
