using Godot;
using Greyhaven;
using System;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private enum PointerKind { Arrow, Select, Move, Blocked, Attack, AttackMove, Guard, Escort, Harvest, Unload, Rally, Build, ForceMove, Pan, Waypoint, Partial, Repair, Deploy, Sell }
    private readonly record struct PointerHint(PointerKind Kind, string Text = "");
    private PointerKind pointerKind = PointerKind.Arrow;
    private Panel pointerHelp;
    private Label pointerText;
    private double pointerTime;

    private void MakePointer()
    {
        pointerHelp = Strip(ui, "#101b20ec", "#6b7a70");
        pointerHelp.MouseFilter = Control.MouseFilterEnum.Ignore;
        pointerText = LabelAt(pointerHelp, "", 7, 2, 261, 22, 12);
        pointerHelp.Hide();
    }

    private bool OverBattlefield(Vector2 position) =>
        view.Area.HasPoint(position) &&
        !(fieldRadio.Visible && fieldRadio.GetRect().HasPoint(position)) &&
        !(commandBar.Visible && commandBar.GetRect().HasPoint(position)) &&
        !(coach.Visible && coach.GetRect().HasPoint(position)) &&
        !(receipt.Visible && receipt.GetRect().HasPoint(position));

    private void UpdatePointer(double elapsed)
    {
        if (DisplayServer.GetName() == "headless") return;
        pointerTime -= elapsed;
        if (pointerTime > 0 && CanControl) return;
        pointerTime = .07;
        var position = view.GetGlobalMousePosition();
        bool overMap = CanControl && OverBattlefield(position);
        bool ctrl = Input.IsKeyPressed(Key.Ctrl), shift = Input.IsKeyPressed(Key.Shift), alt = Input.IsKeyPressed(Key.Alt);
        bool hovered = overMap && !rightHeld && !dragStart.HasValue;
        view.Hovered = hovered ? PickOrderTarget(position, shift, ctrl, alt) : null;
        var hint = CanControl && (rightHeld || overMap)
            ? DescribePointer(position, ctrl, shift, alt, hovered)
            : new PointerHint(PointerKind.Arrow);
        if (pointerKind != hint.Kind)
        {
            pointerKind = hint.Kind;
            Texture2D art = hint.Kind == PointerKind.Arrow ? null : GD.Load<Texture2D>($"res://assets/cursors/{hint.Kind}.png");
            Input.SetCustomMouseCursor(art, Input.CursorShape.Arrow, new Vector2(20, 20));
        }
        pointerHelp.Visible = hint.Kind is PointerKind.Blocked or PointerKind.Partial && !dragStart.HasValue;
        if (!pointerHelp.Visible) return;
        pointerText.Text = hint.Text;
        pointerText.ClipText = true;
        Position(pointerHelp, Math.Min(view.Area.End.X - 278, position.X + 22), Math.Max(4, position.Y - 30), 272, 25);
        pointerText.Modulate = hint.Kind == PointerKind.Blocked ? new Color("#ff9c82") : Gold;
    }

    private PointerHint DescribePointer(Vector2 cursor, bool ctrl, bool shift, bool alt, bool useHovered = false)
    {
        if (rightHeld && panned) return new(PointerKind.Pan);
        if (dragStart.HasValue && view.DragBox.HasValue) return new(PointerKind.Select);

        var ground = view.Ground(cursor);
        if (placing != null)
        {
            var placement = world.AssessPlacement(placing, ground, requireBase: true);
            return placement.Valid ? new(PointerKind.Build) : new(PointerKind.Blocked, placement.Summary);
        }

        bool minersOnly = !ctrl && !shift && !alt && selected.Count > 0 && selected.All(p => p.T.Key == "miner");
        var hit = useHovered ? view.Hovered : PickOrderTarget(cursor, shift, ctrl, alt);
        if (structureTool != StructureTool.None)
        {
            bool canSell = structureTool == StructureTool.Sell && world.CanSell(hit);
            bool canRepair = structureTool == StructureTool.Repair && hit is { Side: 0, Deployed: true } &&
                hit.T.Building && !hit.T.IsWall && (hit.Repairing || hit.Hp < hit.T.Hp);
            return canSell ? new(PointerKind.Sell) : canRepair ? new(PointerKind.Repair) : new(PointerKind.Blocked, "此处不能执行建筑工具");
        }

        if (beaconMode) return new(PointerKind.Waypoint);
        if (PlanningRoute)
            return hit?.Side == 0 && IsMobile(hit) ||
                   plannedRoutes.Values.Any(points => points.Any(p => view.Screen(p).DistanceTo(cursor) < 10))
                ? new(PointerKind.Select) : MoveHint(ground, PointerKind.Waypoint);

        if (ctrl && alt)
            return hit?.Side == 0
                ? selected.Any(p => p != hit && p.Deployed && !p.T.Building) ? new(PointerKind.Escort) : new(PointerKind.Blocked, "请选其他部队进行护卫")
                : MoveHint(ground, PointerKind.Guard);
        if (alt) return MoveHint(ForceMoveDestination(ground, hit), PointerKind.ForceMove);
        if (hit?.T.IsWall == true)
        {
            if (selected.Count == 0) return new(PointerKind.Arrow);
            if (!ctrl && hit.Side <= 0) return new(PointerKind.Blocked, "围墙占用");
            return selected.Any(p => p.Deployed && world.CanAttackTarget(p, hit))
                ? new(PointerKind.Attack) : new(PointerKind.Blocked, "需要能破坏围墙的武器");
        }
        if (ctrl && !shift)
            return selected.Any(p => p.Deployed && world.Armed(p)) && world.Battlefield.Contains(ground)
                ? new(PointerKind.Attack) : new(PointerKind.Blocked, "无法强制开火");

        if (minersOnly)
        {
            if (hit?.T.Key == "refinery" && hit.Side == 0 && hit.Deployed)
                return MoveHint(Campaign.DockPoint(hit), PointerKind.Unload);
            if (hit == null && world.Explored(0, ground) && world.OreAt(ground) is OrePatch ore)
                return MoveHint(ore.P, PointerKind.Harvest);
        }

        if (hit != null && !ctrl && !shift)
        {
            if (selected.Count == 1 && selected[0] == hit)
            {
                if (hit.T.Key == "mcv")
                    return world.CanDeploy(hit) ? new(PointerKind.Deploy) : new(PointerKind.Blocked, "空间不足，无法展开");
                if (Campaign.CanDeployInfantry(hit) || hit.Deployed && world.HouseCount(hit) > 0)
                    return new(PointerKind.Deploy);
            }
            if (selected.Any(p => world.CanEngineer(p, hit))) return new(hit.Side == 0 ? PointerKind.Repair : PointerKind.Unload);
            if (selected.Any(p => world.CanRepairVehicle(p, hit))) return new(PointerKind.Repair);
            if (selected.Any(p => world.CanBoard(p, hit) || world.CanEnterHouse(p, hit))) return new(PointerKind.Unload);
            if (hit.T.Garrison && hit.Side < 0 && selected.Any(p => p.T.Foot))
                return new(PointerKind.Blocked, "需要空闲工兵");
        }

        if (hit?.Side == 0 && !(ctrl && shift)) return new(PointerKind.Select);
        if (selected.Count == 0) return new(PointerKind.Arrow);
        if (Campaign.IsRelayStation(hit)) return new(PointerKind.Blocked, "任务设施需工兵占领");
        if (hit?.Side > 0) return selected.Any(p => p.Deployed && world.Armed(p))
            ? new(PointerKind.Attack) : new(PointerKind.Blocked, "所选单位没有武器");
        if (selected.Count == 1 && selected[0].T.Building)
        {
            if (hit == null && !ctrl && world.CanSetRally(selected[0]))
                return world.CanRallyTo(selected[0], ground) ? new(PointerKind.Rally) : new(PointerKind.Blocked, "无法设置集结点");
            return new(PointerKind.Arrow);
        }
        return MoveHint(ground, ctrl && shift ? PointerKind.AttackMove : PointerKind.Move);
    }

    private PointerHint MoveHint(Point goal, PointerKind action)
    {
        var units = selected.Where(p => p.Deployed && !p.T.Building).ToArray();
        int count = units.Count(p => world.CanMoveTo(p, goal));
        return count == 0 ? new(PointerKind.Blocked, "不可达 · 请换一个落点")
            : count < units.Length ? new(PointerKind.Partial, $"部分单位可达 · {count} / {units.Length}") : new(action);
    }

    private void RejectDestination() => world.Radio = "此处不可达，已保留选择与原命令，请换一个落点。";
}
