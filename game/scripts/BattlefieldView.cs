using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Point = System.Drawing.PointF;
using WorldRect = System.Drawing.RectangleF;

public partial class BattlefieldView : Node2D
{
    internal const float Axis = .87f, Rise = .435f;
    internal const float DefaultZoom = 1.55f;
    internal Campaign World { get; private set; }
    internal Point CameraPosition { get; private set; }
    internal float Zoom { get; private set; } = DefaultZoom;
    internal Rect2 Area;
    internal bool Cinematic;
    internal float FogStrength;
    internal Piece[] Selection = Array.Empty<Piece>();
    internal Piece Hovered;
    internal PlacementAssessment Placement;
    internal float PlacementRange;
    internal Point? LessonFocus;
    internal IReadOnlyDictionary<Piece, List<Point>> Previews;
    internal readonly List<Point> Beacons = new();
    internal int SelectedBeacon = -1;
    internal Font CaptionFont;
    internal Rect2? DragBox;
    internal Point? Mark;
    internal Piece MarkedTarget;
    internal float MarkTime;
    private readonly Dictionary<Piece, (Point Destination, Point Slot)> moveOrders = new();
    private BattleArt art;
    private Texture2D oreSprite;
    internal BattleArt Art => art;
    internal const int SidebarWidth = 168;
    internal Rect2 ProjectionArea => new(Area.Position, new Vector2(GetViewportRect().Size.X - SidebarWidth, Area.Size.Y));
    private TerrainMesh terrain;
    private BattlefieldShroud shroud;
    private Vector2[] tankMounts, tankMuzzles;
    private static readonly Color Gold = new("#c6b17b"), Aqua = new("#90cabf");
    private static readonly Color AttackLineColor = new("#ff0000");
    private static readonly Color HarborCoat = new("#abb5ac");

    public override void _Ready()
    {
        art = new BattleArt();
        oreSprite = GD.Load<Texture2D>("res://assets/ore.png");
        TextureFilter = TextureFilterEnum.Linear;
        tankMounts = new Vector2[64];
        tankMuzzles = new Vector2[64];
        using var reader = new StreamReader(typeof(BattleArt).Assembly.GetManifestResourceStream("tank-sockets.csv"));
        reader.ReadLine();
        for (int i = 0; i < 64; i++)
        {
            var parts = reader.ReadLine().Split(',');
            tankMounts[i] = new Vector2(Parse(parts[1]), Parse(parts[2]));
            tankMuzzles[i] = new Vector2(Parse(parts[3]), Parse(parts[4]));
        }
    }

    private static float Parse(string number) => float.Parse(number, CultureInfo.InvariantCulture);

    internal void SetWorld(Campaign world, bool playable)
    {
        Hide();
        terrain?.Dispose();
        shroud?.Dispose();
        art.ReleaseBattleFrames();
        visibleUnits.Clear();
        World = world;
        if (world.BasinOperation == null)
        {
            freightSprites?.Dispose();
            freightSprites = null;
        }
        else if (playable) freightSprites ??= new FreightSprites();
        terrain = playable ? new TerrainMesh(world.River, art.Static("ground.png"), GD.Load<Texture2D>("res://assets/sand.png")) : null;
        shroud = playable ? new BattlefieldShroud(world) : null;
        Zoom = DefaultZoom;
        Mark = null;
        Hovered = null;
        moveOrders.Clear();
        Placement = null;
        LessonFocus = null;
        Previews = null;
        Beacons.Clear();
        SelectedBeacon = -1;
        DragBox = null;
        MarkedTarget = null;
        Selection = Array.Empty<Piece>();
        Center(world.Home(0));
        Visible = playable;
        QueueRedraw();
    }

    internal void ShowMoveOrder(Piece unit, Point destination) => moveOrders[unit] = (destination, unit.Goal);

    internal Point? DisplayedMoveTarget(Piece unit)
    {
        if (moveOrders.TryGetValue(unit, out var order) && unit.Deployed && unit.Goal == order.Slot &&
            unit.Contact != ContactOrder.Directed && !unit.FirePoint.HasValue && (unit.Path.Count > 0 || unit.Advancing)) return order.Destination;
        return null;
    }

    internal Point? DisplayedAttackTarget(Piece unit)
    {
        if (!unit.Deployed || !World.Armed(unit)) return null;
        if (unit.Contact == ContactOrder.Directed && unit.Target is { Alive: true, Carrier: null } target && World.Visible(target))
            return target.P;
        return unit.FirePoint;
    }

    public override void _ExitTree()
    {
        terrain?.Dispose();
        shroud?.Dispose();
        freightSprites?.Dispose();
        art?.Dispose();
    }

    internal void Center(Point target, float? zoom = null)
    {
        if (zoom.HasValue) Zoom = zoom.Value;
        CameraPosition = World.Battlefield.ClampCenter(target, ProjectionArea.Size.X / (2 * Axis * Zoom), Area.Size.Y / (2 * Rise * Zoom));
    }

    internal Vector2 Screen(Point p, float extraHeight = 0) =>
        ProjectionArea.GetCenter() + Zoom * (TerrainMesh.Project(p.X, p.Y, World.GroundHeight(p) + extraHeight * Campaign.Tile / 15)
                                   - TerrainMesh.Project(CameraPosition.X, CameraPosition.Y));

    internal Point Ground(Vector2 p)
    {
        var flat = FlatGround(p);
        p.Y += World.GroundHeight(flat) * 15 * Zoom;
        return FlatGround(p);
    }

    private Point FlatGround(Vector2 p)
    {
        var delta = (p - ProjectionArea.GetCenter()) / Zoom;
        float u = delta.X / Axis, v = delta.Y / Rise;
        return new Point(CameraPosition.X + (u + v) * .5f, CameraPosition.Y + (v - u) * .5f);
    }

    internal void Pan(Vector2 pixels) => Center(FlatGround(ProjectionArea.GetCenter() + pixels));

    internal void AdjustZoom(Vector2 mouse, float factor)
    {
        var anchor = FlatGround(mouse);
        Zoom = Math.Clamp(Zoom * factor, 1.05f, 2.4f);
        var after = FlatGround(mouse);
        Center(new Point(CameraPosition.X + anchor.X - after.X, CameraPosition.Y + anchor.Y - after.Y));
    }

    internal Piece Pick(Vector2 point, bool preferOre = false, bool preferGarrison = false)
    {
        var ground = Ground(point);
        Piece exact = null, near = null, foundation = null, garrison = null;
        float closest = float.MaxValue;
        float exactDepth = float.NegativeInfinity, nearDepth = exactDepth, foundationDepth = exactDepth, garrisonDepth = exactDepth;
        foreach (var unit in World.Units)
        {
            if (!unit.Alive || unit.Carrier != null || !World.Visible(unit)) continue;
            float depth = unit.P.X + unit.P.Y;
            var anchor = Screen(unit.P);
            if (unit.T.Building)
            {
                bool onFoundation = Campaign.Footprint(unit.T, unit.P).Contains(ground);
                bool onBuilding = PickInk(unit, point, anchor);
                if (depth > exactDepth && onBuilding) { exact = unit; exactDepth = depth; }
                if (depth > foundationDepth && onFoundation) { foundation = unit; foundationDepth = depth; }
                if (preferGarrison && depth > garrisonDepth && unit.Deployed && unit.Side <= 0 && unit.T.Garrison &&
                    (onBuilding || onFoundation)) { garrison = unit; garrisonDepth = depth; }
                continue;
            }
            var bounds = PickBody(unit, anchor);
            if (depth > exactDepth && bounds.HasPoint(point) && PickInk(unit, point, anchor))
            { exact = unit; exactDepth = depth; }
            if (!bounds.Grow(unit.T.Foot ? 8 : 6).HasPoint(point)) continue;
            float score = bounds.GetCenter().DistanceSquaredTo(point);
            if (score < closest || score == closest && depth > nearDepth)
            { near = unit; closest = score; nearDepth = depth; }
        }
        if (exact is { T.Building: false }) return exact;
        if (garrison != null) return garrison;
        if (exact != null) return exact;
        if (preferOre && near?.T.Key == "miner" && World.Explored(0, ground) && World.OreAt(ground) != null) return foundation;
        return near ?? foundation;
    }

    internal Rect2 PickBody(Piece p) => PickBody(p, Screen(p.P));

    private Rect2 PickBody(Piece p, Vector2 screen)
    {
        float width = (p.T.Building ? (p.T.FoundationWidth + p.T.FoundationHeight) * 12
            : p.T.Foot ? 19 : 31) * Zoom;
        float height = (p.T.Building ? 36 : p.T.Foot ? 21 : 19) * Zoom;
        return new Rect2(screen - new Vector2(width * .5f, height * .92f), new Vector2(width, height * 1.15f));
    }

    private bool PickInk(Piece unit, Vector2 cursor, Vector2 anchor)
    {
        int direction = (unit.Dir + 14) % 16;
        if (unit.T.Building)
        {
            var box = BuildingBox(unit, anchor);
            if (!box.HasPoint(cursor)) return false;
            if (unit.T.IsWall)
            {
                var wall = art.Wall(World.WallConnections(unit), unit.Side);
                return art.Opaque(wall, box, cursor);
            }
            var texture = unit.T.IsOilDerrick ? art.OilDerrick(unit.Side) : art.Frame(BuildingFrame(unit), false);
            return texture != null && art.Opaque(texture, box, cursor);
        }
        if (unit.T.Foot)
        {
            float size = unit.T.Size * .54f * Zoom;
            var box = new Rect2(anchor - new Vector2(.5f, .91f) * size, Vector2.One * size);
            string key = unit.T.Key == "at" ? "at-mobile" : unit.T.Key;
            return box.HasPoint(cursor) && art.Opaque(art.Cell(key + "-base", direction / 2), box, cursor);
        }
        var vehicle = VehicleBox(unit.T, anchor);
        if (unit.T.Key != "tank")
        {
            string key = unit.T.Key == "miner" && unit.Cargo > 10 ? "miner-full" : World.RepairVehicle(unit) ? "ifv-repair" : unit.T.Key;
            return vehicle.HasPoint(cursor) && art.Opaque(art.Cell(key + "-base", direction), vehicle, cursor);
        }
        int hull = TankFrame(unit.Turn);
        var turret = new Rect2(vehicle.Position + tankMounts[hull] * vehicle.Size, vehicle.Size);
        return (vehicle.HasPoint(cursor) && art.Opaque(art.Cell("tank-hull-base", hull), vehicle, cursor)) ||
            (turret.HasPoint(cursor) && art.Opaque(art.Cell("tank-turret-base", TankFrame(unit.TurretTurn)), turret, cursor));
    }

    private Rect2 BuildingBox(Piece p, Vector2 at)
    {
        float k = (p.T.FoundationWidth + p.T.FoundationHeight) * Campaign.Tile * Axis * Zoom / 224;
        return new Rect2(at - new Vector2(160, 220) * k, Vector2.One * (320 * k));
    }

    private readonly List<(Piece Unit, Vector2 Screen)> visibleUnits = new();

    public override void _Draw()
    {
        if (terrain == null) return;
        art.Trim();
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color("#355e68"));
        var origin = ProjectionArea.GetCenter() - TerrainMesh.Project(CameraPosition.X, CameraPosition.Y) * Zoom;
        DrawSetTransform(origin, 0, Vector2.One * Zoom);
        TextureRepeat = TextureRepeatEnum.Mirror;
        terrain.Draw(this, new Rect2((Area.Position - origin) / Zoom, Area.Size / Zoom).Grow(60));
        DrawSetTransform(Vector2.Zero);

        DrawBridgeRails();
        if (World.River.IsBasin) DrawBasinRailway();
        DrawScenery();
        if (FogStrength > 0) DrawFog(origin);
        DrawOre();

        var harbor = World.HarborOperation;
        bool raisedGate = harbor?.GateLift >= 1 && InShot(RiverbendMap.HarborGateCenter);
        if (harbor != null) DrawHarbor(harbor, raisedGate);
        else if (World.RiverbendOperation != null)
        {
            var location = RiverbendMap.RiverExitSign;
            var sign = RiverbendMap.PortPoint(location.X, location.Y);
            if (InShot(sign)) DrawRoadSign(sign, "矿业盆地  ›");
        }
        else if (World.BasinOperation is { } basin)
        {
            DrawBasinFreight(basin.Freight);
        }
        visibleUnits.Clear();
        var unitArea = Area.Grow(150);
        bool revealScene = FullSceneVisibility;
        foreach (var unit in World.Units)
        {
            if (unit.Carrier != null || !revealScene && !World.Visible(unit)) continue;
            var at = Screen(unit.P);
            if (unitArea.HasPoint(at)) visibleUnits.Add((unit, at));
        }
        visibleUnits.Sort(static (a, b) =>
        {
            int depth = (a.Unit.P.X + a.Unit.P.Y).CompareTo(b.Unit.P.X + b.Unit.P.Y);
            return depth != 0 ? depth : a.Unit.Id.CompareTo(b.Unit.Id);
        });
        int nextPost = -1;
        foreach (var (unit, at) in visibleUnits)
        {
            while (raisedGate && nextPost <= 1)
            {
                var post = RiverbendMap.HarborGatePost(nextPost);
                if (post.X + post.Y > unit.P.X + unit.P.Y) break;
                DrawRaisedGatePost(nextPost);
                nextPost += 2;
            }
            DrawUnit(unit, at);
            DrawDamageEffects(unit, at);
        }
        while (raisedGate && nextPost <= 1)
        {
            DrawRaisedGatePost(nextPost);
            nextPost += 2;
        }

        DrawShots();
        if (!Cinematic)
        {
            DrawMoveOrders();
            DrawBattleFeedback();
            DrawMapAnnotations();
            if (Placement != null)
            {
                if (PlacementRange > 0)
                {
                    var circle = ReferenceBalance.Circle(Campaign.Middle(Placement.Bounds), PlacementRange, 96);
                    DrawPolyline(circle.Append(circle[0]).Select(p => Screen(p)).ToArray(),
                        Placement.Valid ? new Color("#83cbb9", .85f) : new Color("#dc7b65", .85f), 1);
                }
                foreach (var tile in Placement.Tiles)
                    GroundOutline(tile.Bounds, tile.Valid && Placement.Global == PlacementBlock.None
                        ? new Color("#89d49a") : new Color("#e86e60"), true);
                foreach (var cell in Placement.WallExtensions)
                    GroundOutline(cell, new Color("#89d49a", .65f), true);
            }
            if (DragBox is Rect2 drag)
            {
                DrawRect(drag, new Color(Aqua, .1f));
                DrawRect(drag, Aqua, false, 1);
            }
        }
        else if (harbor?.Darkness > 0)
            DrawRect(Area, new Color("#0b1729", harbor.Darkness * .85f));
    }

    private void DrawMoveOrders()
    {
        foreach (var unit in moveOrders.Keys.Where(unit => DisplayedMoveTarget(unit) == null).ToArray())
            moveOrders.Remove(unit);
        foreach (var unit in Selection)
        {
            if (DisplayedAttackTarget(unit) is Point target)
            {
                var start = Screen(unit.P, unit.T.Foot ? .22f : .30f);
                var end = Screen(target);
                DrawLine(start, end, AttackLineColor);
                DrawRect(new Rect2(start - Vector2.One, Vector2.One * 3), AttackLineColor);
                DrawRect(new Rect2(end - Vector2.One, Vector2.One * 3), AttackLineColor);
                continue;
            }
            if (DisplayedMoveTarget(unit) is not Point destination) continue;
            var from = Screen(unit.P);
            var to = Screen(destination);
            if (from.DistanceSquaredTo(to) < 5) continue;
            DrawLine(from, to, new Color("#418957", .85f), 2.25f, true);
            DrawLine(from, to, new Color("#a7df9d", .94f), 1, true);
            DrawRect(new Rect2(to - Vector2.One * 2, Vector2.One * 4), new Color("#a7df9d"), false, 1);
        }
    }

    private void DrawBridgeRails()
    {
        foreach (var bridge in World.River.Bridges)
        foreach (float side in new[] { bridge.Top + .15f, bridge.Bottom - .15f })
        {
            Vector2 At(int x) => Screen(new Point(x * Campaign.Tile, side * Campaign.Tile), 6 / (float)Campaign.Tile);
            var a = At(bridge.Left);
            var b = At(bridge.Right);
            if (!new Rect2(a, b - a).Grow(12).Intersects(Area)) continue;
            var lift = new Vector2(0, 3 * Zoom);
            DrawLine(a, b, new Color("#4b5550"), MathF.Max(1, 3 * Zoom), true);
            for (int x = bridge.Left; x <= bridge.Right; x += 2)
                DrawLine(At(x) + lift, At(x) - lift, new Color("#4b5550"), MathF.Max(1, Zoom), true);
            DrawLine(a - lift, b - lift, new Color("#c2bda4"), MathF.Max(1, 1.7f * Zoom), true);
        }
    }

    private void DrawBasinRailway()
    {
        const float gauge = .45f;
        const float sleeperSpacing = .83f;
        var brown = new Color("#544239");
        var steel = new Color("#d7cbb4");
        var visible = Area.Grow(22);
        var path = RiverbendMap.BasinRail;
        float sleeperAt = .4f;
        var lastOffset = Vector2.Zero;
        Vector2 Cell(int index) => new(path[index].X, path[index].Y);
        Vector2 DrawAt(Vector2 p) => Screen(new Point(p.X * Campaign.Tile, p.Y * Campaign.Tile));
        for (int i = 1; i < path.Count; i++)
        {
            var a = Cell(i - 1);
            var b = Cell(i);
            var direction = (b - a).Normalized();
            var normal = new Vector2(-direction.Y, direction.X);
            if (i == 1) lastOffset = normal * gauge;
            var offset = normal * gauge;
            if (i + 1 < path.Count)
            {
                var next = (Cell(i + 1) - b).Normalized();
                var miter = (normal + new Vector2(-next.Y, next.X)).Normalized();
                offset = miter * (gauge / Math.Max(.65f, miter.Dot(normal)));
            }
            var start = DrawAt(a);
            var end = DrawAt(b);
            float length = a.DistanceTo(b);
            if (visible.Intersects(new Rect2(start.Min(end), start.Max(end) - start.Min(end)).Grow(12)))
            {
                for (; sleeperAt < length; sleeperAt += sleeperSpacing)
                {
                    var point = a + direction * sleeperAt;
                    DrawLine(DrawAt(point - normal * .84f), DrawAt(point + normal * .84f), brown, Math.Max(1, 2.7f * Zoom));
                }
                DrawLine(DrawAt(a + lastOffset), DrawAt(b + offset), steel, Math.Max(1, 1.45f * Zoom), true);
                DrawLine(DrawAt(a - lastOffset), DrawAt(b - offset), steel, Math.Max(1, 1.45f * Zoom), true);
            }
            else if (sleeperAt < length)
                sleeperAt += MathF.Ceiling((length - sleeperAt) / sleeperSpacing) * sleeperSpacing;
            sleeperAt -= length;
            lastOffset = offset;
        }
    }

    private void DrawMapAnnotations()
    {
        for (int i = 0; i < Beacons.Count; i++)
        {
            var at = Screen(Beacons[i]);
            if (!Area.HasPoint(at)) continue;
            var color = i == SelectedBeacon ? Gold : Aqua;
            var tip = at - new Vector2(0, 23);
            DrawLine(at, tip, color, 2);
            DrawColoredPolygon(new[] { tip, tip + new Vector2(14, 4), tip + new Vector2(0, 9) }, color);
        }
        if (Previews == null) return;
        foreach (var (unit, points) in Previews)
        {
            if (!unit.Deployed) continue;
            var from = Screen(unit.P);
            for (int i = 0; i < points.Count; i++)
            {
                var to = Screen(points[i]);
                DrawLine(from, to, Gold, 1.5f);
                DrawRect(new Rect2(to - new Vector2(2, 2), new Vector2(5, 5)), Gold);
                DrawString(CaptionFont, to - new Vector2(-5, 5), (i + 1).ToString(), fontSize: 12, modulate: Gold);
                from = to;
            }
        }
    }

    private void DrawScenery()
    {
        var corners = new[] { FlatGround(Area.Position), FlatGround(Area.End),
            FlatGround(new Vector2(Area.End.X, Area.Position.Y)), FlatGround(new Vector2(Area.Position.X, Area.End.Y)) };
        int left = (int)MathF.Floor(corners.Min(p => p.X) / Campaign.Tile) - 3;
        int right = (int)MathF.Ceiling(corners.Max(p => p.X) / Campaign.Tile) + 3;
        int top = (int)MathF.Floor(corners.Min(p => p.Y) / Campaign.Tile) - 3;
        int bottom = (int)MathF.Ceiling(corners.Max(p => p.Y) / Campaign.Tile) + 3;
        for (int y = top; y < bottom; y++)
        for (int x = left; x < right; x++)
        {
            var cell = World.River.At(x, y);
            if (cell == null) continue;
            var p = new Point((x + .5f) * Campaign.Tile, (y + .5f) * Campaign.Tile);
            if (cell.Tree)
            {
                var foot = Screen(p);
                if (!Area.Grow(32).HasPoint(foot)) continue;
                float height = Zoom * (17 + cell.Variation % 12), width = Zoom * (8 + cell.Variation % 4);
                ContactShadow(foot, width * 1.9f, height * .24f);
                DrawLine(foot, foot - new Vector2(0, height * .48f), new Color("#675940"), 2 * Zoom);
                for (int i = 0; i < 3; i++)
                {
                    var tip = foot - new Vector2(0, height * (1 - .23f * i));
                    DrawColoredPolygon(new[] { tip, tip + new Vector2(width * (i + 2) * .24f, height * .43f),
                        tip + new Vector2(0, height * .53f), tip + new Vector2(-width * (i + 2) * .24f, height * .41f) },
                        i == 0 ? new Color("#6f8457") : new Color("#405a41"));
                }
            }
            else if (IsRoad(cell))
            {
                var edge = new Color("#b1ad91", .55f);
                float half = Campaign.Tile * .5f;
                if (!IsRoad(World.River.At(x + 1, y)))
                    DrawLine(Screen(new Point(p.X + half, p.Y - half)), Screen(new Point(p.X + half, p.Y + half)), edge, 1);
                if (!IsRoad(World.River.At(x, y + 1)))
                    DrawLine(Screen(new Point(p.X - half, p.Y + half)), Screen(new Point(p.X + half, p.Y + half)), edge, 1);
                if (y % 5 == 2 && x % 3 != 0)
                    DrawLine(Screen(new Point(p.X - 5, p.Y)), Screen(new Point(p.X + 5, p.Y)), edge, 1);
            }
        }
    }

    private static bool IsRoad(RiverbendMap.Cell cell) => cell?.Ground is
        RiverbendMap.Surface.Road or RiverbendMap.Surface.Bridge or RiverbendMap.Surface.Ramp;

    private void DrawFog(Vector2 origin)
    {
        var limits = new[] { FlatGround(Area.Position), FlatGround(Area.End),
            FlatGround(new Vector2(Area.End.X, Area.Position.Y)), FlatGround(new Vector2(Area.Position.X, Area.End.Y)) };
        const int padding = 16;
        int x1 = Mathf.FloorToInt(World.FogColumn(limits.Min(p => p.X)) / (float)padding) * padding - padding;
        int x2 = Mathf.CeilToInt(World.FogColumn(limits.Max(p => p.X)) / (float)padding) * padding + padding;
        int y1 = Mathf.FloorToInt(World.FogRow(limits.Min(p => p.Y)) / (float)padding) * padding - padding;
        int y2 = Mathf.CeilToInt(World.FogRow(limits.Max(p => p.Y)) / (float)padding) * padding + padding;
        shroud.Draw(this, origin, Zoom, new Rect2I(x1, y1, x2 - x1, y2 - y1), FogStrength);
    }

    private void DrawOre()
    {
        const int stages = 12;
        float size = Campaign.Tile * Axis * 2 * Zoom / 92;
        foreach (var patch in World.Ore)
        {
            if (patch.Stock <= .01f || !FullSceneVisibility && !World.Explored(0, patch.P)) continue;
            var p = Screen(patch.P);
            if (!Area.Grow(28).HasPoint(p)) continue;
            int stage = Math.Clamp((int)MathF.Ceiling(stages * patch.Stock / patch.Initial) - 1, 0, stages - 1);
            DrawTextureRectRegion(oreSprite, new Rect2(p - new Vector2(48, 37) * size, new Vector2(96, 64) * size),
                new Rect2(96 * stage, 0, 96, 64));
        }
    }

    private void GroundOutline(WorldRect rect, Color color, bool fill, bool outline = true)
    {
        float height = World.GroundHeight(Campaign.Middle(rect));
        Vector2 At(float x, float y) => ProjectionArea.GetCenter() +
            (TerrainMesh.Project(x, y, height) - TerrainMesh.Project(CameraPosition.X, CameraPosition.Y)) * Zoom;
        var polygon = new[] { At(rect.Left, rect.Top), At(rect.Right, rect.Top), At(rect.Right, rect.Bottom), At(rect.Left, rect.Bottom) };
        if (fill) DrawColoredPolygon(polygon, outline ? new Color(color, .24f) : color);
        if (outline) DrawPolyline(new[] { polygon[0], polygon[1], polygon[2], polygon[3], polygon[0] }, color, 1);
    }

    private void DrawHarbor(HarborOperation harbor, bool raisedGate)
    {
        if (!harbor.CargoOutside && InShot(harbor.CargoShip))
        {
            float turn = HarborOperation.Blend(0, 1, ((HarborOperation.CargoBerth.Y - harbor.CargoShip.Y) / Campaign.Tile - .65f) / 5.65f);
            VesselWake(harbor.CargoShip, turn, 4.45f, 1.35f, harbor.CargoWake);
            HarborFrame(HarborSprites.Supply, Screen(harbor.CargoShip, Bob(.027f)), turn, HarborCoat);
        }

        if (InShot(harbor.LandingHull))
        {
            float bob = Bob(.024f * (1 - harbor.RampOpen));
            VesselWake(harbor.LandingHull, 0, 2.31f, .91f, harbor.LandingWake);
            var anchor = Screen(harbor.LandingHull, bob);
            HarborFrame(HarborSprites.Landing, anchor, harbor.RampOpen, BattleArt.Faction(0));
            var aboard = World.Units.Where(harbor.AboardLandingCraft).OrderBy(p => p.P.X + p.P.Y).ToArray();
            foreach (var unit in aboard) DrawUnit(unit, Screen(unit.P, harbor.DeckHeight(unit) + bob));
            if (aboard.Length > 0) HarborFrame(HarborSprites.Cover, anchor, harbor.RampOpen, BattleArt.Faction(0));
        }

        var crane = RiverbendMap.PortPoint(47, 16.7f);
        if (InShot(crane))
        {
            ContactShadow(Screen(crane), 35 * Zoom, 13 * Zoom);
            var drop = RiverbendMap.PortPoint(43.847f, 20.857f);
            if (harbor.CargoOnQuay > 0 && harbor.Scene > HarborScene.Battle)
                HarborFrame(HarborSprites.Cargo, Screen(Offset(drop, -1.04f, .13f)), 0, null);
            if (harbor.CargoOnQuay >= (harbor.Scene > HarborScene.Battle ? 2 : 1))
                HarborFrame(HarborSprites.Cargo, Screen(drop), 0, null);
            float berthGap = (HarborOperation.CargoBerth.Y - harbor.CargoShip.Y) / Campaign.Tile;
            if (Math.Abs(berthGap) < .22f && harbor.CraneCycle < .18f)
                HarborFrame(HarborSprites.Cargo, Screen(Offset(crane, 5.156f, -.816f), .68f), 0, null);
            HarborFrame(HarborSprites.Crane, Screen(crane), harbor.CraneCycle, HarborCoat);
            HarborFrame(HarborSprites.Bollard, Screen(RiverbendMap.PortPoint(48.15f, 15.8f)), 0, null);
        }

        if (World.Harbor != null)
        {
            var office = Campaign.Middle(World.Harbor.Body);
            if (InShot(office))
            {
                ContactShadow(Screen(office), 63 * Zoom, 27 * Zoom);
                HarborFrame(HarborSprites.Office, Screen(office), harbor.PortOnline ? 1 : 0, HarborCoat);
            }
        }

        var gate = RiverbendMap.HarborGateCenter;
        if (InShot(gate))
        {
            foreach (int sign in new[] { -1, 1 }) ContactShadow(Screen(RiverbendMap.HarborGatePost(sign)), 23 * Zoom, 10 * Zoom);
            if (!raisedGate)
            {
                HarborFrame(HarborSprites.Gate, Screen(gate), harbor.GateLift, HarborCoat);
                GateLamp(-1, harbor.GateBlocked);
                GateLamp(1, harbor.GateBlocked);
            }
        }
        foreach (var bay in HarborOperation.QuayTruckBays)
        {
            if (!InShot(bay)) continue;
            var left = Screen(Offset(bay, -.62f, -.86f));
            var right = Screen(Offset(bay, .62f, -.86f));
            var paint = new Color("#c9ae68", .67f);
            DrawLine(left, right, paint, 1.2f);
            DrawLine(left, Screen(Offset(bay, -.62f, .86f)), paint, 1.2f);
            DrawLine(right, Screen(Offset(bay, .62f, .86f)), paint, 1.2f);
        }
        var signPosition = RiverbendMap.PortPoint(65.5f, 28);
        if (InShot(signPosition)) DrawRoadSign(signPosition);
    }

    private static Point Offset(Point p, float x, float y) => new(p.X + x * Campaign.Tile, p.Y + y * Campaign.Tile);

    private void VesselWake(Point center, float turn, float halfLength, float halfWidth, float motion)
    {
        float angle = turn * MathF.PI * .5f;
        var forward = new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
        var side = new Vector2(-forward.Y, forward.X);
        for (int ring = 0; ring < 3; ring++)
        {
            float drift = (World.VisualTime * .51f + ring / 3f) % 1;
            foreach (int sign in new[] { -1, 1 })
            {
                float across = sign * (halfWidth + .18f + drift * (.30f + .62f * motion));
                float wake = halfLength * .72f + motion * (.45f + drift * 1.14f);
                var start = side * across - forward * wake;
                var bend = side * (across + sign * .12f);
                var end = side * across + forward * (halfLength * .54f);
                var a = Offset(center, start.X, start.Y);
                var b = Offset(center, bend.X, bend.Y);
                var c = Offset(center, end.X, end.Y);
                if (!World.WaterAt(a) || !World.WaterAt(b) || !World.WaterAt(c)) continue;
                DrawPolyline(new[] { Screen(a), Screen(b), Screen(c) }, new Color("#b2d2c7", (1 - drift) * (.14f + .26f * motion)), 1.1f, true);
            }
        }
    }

    private void ContactShadow(Vector2 at, float width, float height)
    {
        DrawSetTransform(at + new Vector2(width * .16f, height * .18f), 0, new Vector2(width, height));
        for (int layer = 0; layer < 4; layer++)
            DrawCircle(Vector2.Zero, .51f - layer * .105f, new Color("#141b1a", .082f));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawRaisedGatePost(int sign)
    {
        var sprite = HarborSprites.Gate;
        var bounds = sprite.Bounds;
        float scale = Zoom / HarborSprites.Density;
        int split = -bounds.X;
        int left = sign > 0 ? 0 : split;
        int width = sign > 0 ? split : bounds.Width - split;
        var frame = "harbor/gate/" + (sprite.Frames - 1);
        var source = new Rect2(left, 0, width, bounds.Height);
        var target = new Rect2(Screen(RiverbendMap.HarborGateCenter) + new Vector2(bounds.X + left, bounds.Y) * scale, source.Size * scale);
        DrawLayered(frame, target, HarborCoat, source);
        GateLamp(sign, false);
    }

    private void GateLamp(int sign, bool blocked)
    {
        var at = Screen(Offset(RiverbendMap.HarborGatePost(sign), .228f, 0), .96f);
        var color = blocked ? new Color("#e47b53") : new Color("#9bd0a7");
        DrawCircle(at, 3.8f * Zoom, new Color(color, .24f));
        DrawRect(new Rect2(at - Vector2.One * Zoom, Vector2.One * (2 * Zoom)), color);
    }

    private void DrawRoadSign(Point point, string text = "河湾镇  ›")
    {
        var transform = new Transform2D(new Vector2(Axis, Rise) * Zoom, new Vector2(0, Zoom), Screen(point, .92f));
        DrawSetTransformMatrix(transform);
        DrawRect(new Rect2(-1.25f, 0, 2.5f, .92f * Campaign.Tile), new Color("#69766b"));
        DrawRect(new Rect2(-25, -6, 50, 12), new Color("#284a44"));
        DrawRect(new Rect2(-25, -6, 50, 12), new Color("#8da594"), false, 1);
        if (CaptionFont != null) DrawString(CaptionFont, new Vector2(-21, 3), text, fontSize: 8, modulate: new Color("#e9ead9"));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawLayered(string key, Rect2 target, Color tint, Rect2 source, string paintKey = null)
    {
        DrawTextureRectRegion(art.Frame(key), target, source);
        if (art.Frame(paintKey ?? key + "-paint", false) is Texture2D paint)
            DrawTextureRectRegion(paint, target, source, tint);
    }

    private bool FullSceneVisibility => Cinematic && World.FirstChapter && FogStrength < .995f;
    private bool InShot(Point p) => (FullSceneVisibility || World.Explored(0, p)) && Area.Grow(460).HasPoint(Screen(p));
    private float Bob(float height) => Mathf.Sin(World.VisualTime * 1.44f) * height;

    private void HarborFrame(HarborSprite sprite, Vector2 anchor, float amount, Color? tint)
    {
        int index = (int)MathF.Round(Math.Clamp(amount, 0, 1) * (sprite.Frames - 1));
        var key = "harbor/" + sprite.Name + "/" + index;
        float scale = Zoom / HarborSprites.Density;
        var rect = sprite.Bounds;
        DrawFrame(key, new Rect2(anchor + new Vector2(rect.X, rect.Y) * scale, new Vector2(rect.Width, rect.Height) * scale), tint);
    }

    private void DrawFrame(string key, Rect2 rect, Color? tint)
    {
        DrawTextureRect(art.Frame(key), rect, false);
        if (tint is Color color && art.Frame(key + "-paint", false) is Texture2D paint)
            DrawTextureRect(paint, rect, false, color);
    }

    private void DrawUnit(Piece p, Vector2 at)
    {
        string key = p.T.Key;
        var tint = World.RiverbendOperation?.IsSeizedTruck(p) == true ? BattleArt.Faction(2)
            : p == World.HarborOperation?.RoadVehicle ? new Color("#cf9c58") : BattleArt.Faction(p.Side);
        if (p.Alive && !p.T.Building && !Cinematic && Selection.Contains(p))
            DrawSelectionRing(p, at);

        if (p.T.Building)
        {
            if (p.Alive) ContactShadow(at, (p.T.FoundationWidth + p.T.FoundationHeight) * 10 * Zoom, 17 * Zoom);
            DrawBuilding(p, at, tint);
            return;
        }

        int dir = (p.Dir + 14) % 16;
        if (!p.Alive && (p.T.Foot || p.Dead > 2.8f)) return;
        if (p.Alive) ContactShadow(at, (p.T.Foot ? 12 : 24) * Zoom, (p.T.Foot ? 4 : 8) * Zoom);

        if (p.T.Foot)
        {
            DrawInfantry(p, at, dir / 2, tint);
            return;
        }

        var vehicle = VehicleBox(p.T, at);
        if (key == "tank")
        {
            int hull = TankFrame(p.Turn), turret = TankFrame(p.TurretTurn);
            if (!p.Alive)
            {
                DrawTextureRect(art.Cell("tank-wreck", hull), vehicle, false);
                return;
            }
            DrawCell("tank-hull", hull, vehicle, tint);
            var turretBox = new Rect2(vehicle.Position + tankMounts[hull] * vehicle.Size, vehicle.Size);
            DrawCell("tank-turret", turret, turretBox, tint);
            return;
        }

        if (!p.Alive) DrawTextureRect(art.Cell(key + "-wreck", dir), vehicle, false);
        else DrawCell(key == "miner" && p.Cargo > 10 ? "miner-full" : World.RepairVehicle(p) ? "ifv-repair" : key,
            dir, vehicle, tint);
        if (p.Alive && Cinematic && World.HarborOperation?.RoadVehicle == p)
        {
            var harbor = World.HarborOperation;
            if (harbor.Opening && harbor.Darkness > .32f) TruckLamps(p.P, -.49f, new Color("#fff0bf"));
            if (harbor.BrakeLights) TruckLamps(p.P, .49f, new Color("#ff714d"));
        }
    }

    private void TruckLamps(Point point, float bumper, Color color)
    {
        foreach (int sign in new[] { -1, 1 })
        {
            var p = Screen(Offset(point, bumper, sign * .31f), .26f);
            DrawCircle(p, 3.3f * Zoom, new Color(color, .23f));
            DrawRect(new Rect2(p - Vector2.One * Zoom * .6f, Vector2.One * Zoom * 1.2f), color);
        }
    }

    private void DrawBuilding(Piece p, Vector2 at, Color tint)
    {
        if (p.T.IsWall)
        {
            if (p.Alive) DrawTextureRect(art.Wall(World.WallConnections(p), p.Side), BuildingBox(p, at), false);
            return;
        }
        if (!p.Alive && p.Dead >= 1.43f) return;
        if (p.T.IsOilDerrick)
        {
            DrawTextureRect(art.OilDerrick(p.Side, p.Alive ? World.VisualTime : 0), BuildingBox(p, at), false);
            return;
        }
        float k = (p.T.FoundationWidth + p.T.FoundationHeight) * Campaign.Tile * Axis * Zoom / 224;
        var rect = BuildingBox(p, at);
        var key = p.T.Key;
        string frame = BuildingFrame(p);
        bool buildingInMotion = p.Packing > 0 || p.Ready < 1;
        if (art.Frame(frame, false) == null)
        {
            GroundOutline(Campaign.Footprint(p.T, p.P), tint, true);
            return;
        }
        DrawFrame(frame, rect, tint);
        if (!p.Alive || buildingInMotion || !BuildingV08.Specs.TryGetValue(key, out var spec)) return;
        string idle = $"b08/{key}/idle/{(int)(World.VisualTime * 24) % 96}";
        if (art.Frame(idle, false) != null)
            DrawFrame(idle, new Rect2(rect.Position + new Vector2(spec.Idle.X, spec.Idle.Y) * k,
                new Vector2(spec.Idle.Width, spec.Idle.Height) * k), tint);
        if (key == "factory")
        {
            float open = Math.Min(p.Door * 3, 1);
            if (p.Queue.TryPeek(out var next)) open = Math.Max(open, 1 - (next.Seconds - p.Work) / .6f);
            int door = (int)MathF.Round(Math.Clamp(open, 0, 1) * 15);
            string frameName = $"b08/{key}/door/{door}";
            if (art.Frame(frameName, false) != null)
                DrawFrame(frameName, new Rect2(rect.Position + new Vector2(spec.Door.X, spec.Door.Y) * k,
                    new Vector2(spec.Door.Width, spec.Door.Height) * k), tint);
        }
    }

    private string BuildingFrame(Piece p)
    {
        var key = p.T.Key;
        string frame = $"b08/{key}/background";
        int damage = p.Hp < p.T.Hp * .25f ? 3 : p.Hp < p.T.Hp * .50f ? 2 : p.Hp < p.T.Hp * .75f ? 1 : 0;
        if (p.Alive && (p.Packing > 0 || p.Ready < 1))
        {
            float progress = p.Packing > 0 ? p.Packing / Campaign.AssemblySeconds : p.Ready;
            string action = p.BaseTransition && !p.Selling ? "deploy" : "build";
            int last = action == "deploy" ? 95 : 71;
            frame = $"b08/{key}/{action}/{Math.Clamp((int)(progress * last), 0, last)}";
        }
        else if (damage > 0)
        {
            string damaged = p.Alive ? $"b08/{key}/damage-{damage}-background" : $"b08/{key}/damage-3";
            if (art.Frame(damaged, false) != null) frame = damaged;
        }
        if (art.Frame(frame, false) == null) frame = "iso-" + key;
        return frame;
    }

    private void DrawCell(string key, int frame, Rect2 box, Color tint)
    {
        DrawTextureRect(art.Cell(key + "-base", frame), box, false);
        DrawTextureRect(art.Cell(key + "-paint", frame), box, false, tint);
    }

    private Rect2 VehicleBox(TypeInfo t, Vector2 at)
    {
        var (radius, width) = t.Key switch
        {
            "tank" => (2.911f, 9.25f), "ifv" => (2.5493f, 7.5f), "arty" => (3.0698f, 9.2f),
            "mcv" => (2.8197f, 8.35f), _ => (3.018f, 8.1f)
        };
        float size = Zoom * t.VisualRadius * Axis * MathF.Sqrt(2) * width / radius;
        return new Rect2(at - new Vector2(.5f, 188f / 320) * size, Vector2.One * size);
    }

    private static int TankFrame(float turn) => ((int)MathF.Round(turn * 4) + 56) % 64;

    private void DrawBattleFeedback()
    {
        var area = Area.Grow(24);
        bool engineerSelected = Selection.Any(p => p.T.Key == "engineer");
        foreach (var (unit, at) in visibleUnits)
        {
            if (!unit.Alive || !area.HasPoint(at)) continue;
            bool selected = Selection.Contains(unit);
            if (!unit.T.IsWall && (selected || unit == Hovered || unit.Hp < unit.T.Hp || unit.Ready < 1 || unit.Repairing))
                DrawIndicators(unit, at);
            if (unit.T.Garrison && (unit.Side >= 0 || unit == Hovered || engineerSelected))
                DrawGarrisonCount(unit, at);
            if (unit == Hovered)
                DrawCornerMarks(VisibleBody(unit, at).Grow(3), unit.Side < 0 ? new Color("#f3eee0") : unit.Side == 0 ? Aqua : BattleArt.Faction(unit.Side));
            if (!selected) continue;
            if (unit.T.Building) DrawFoundationPins(unit);
            else DrawSelectionPips(unit, at);
            if (unit.Rally is Point rally)
            {
                var end = Screen(rally);
                DrawLine(at, end, new Color(Aqua, .54f), 1);
                DrawLine(end, end - new Vector2(0, 12), Aqua, 1);
                DrawColoredPolygon(new[] { end - new Vector2(0, 12), end - new Vector2(-9, 9), end - new Vector2(0, 6) }, Aqua);
            }
        }

        if (CaptionFont != null)
        {
            var training = World.Tutorial && World.LessonKey == "hq" ? World.Live(0).FirstOrDefault(p => p.T.Key == "mcv")?.P : LessonFocus;
            if (training is Point focus)
                DrawString(CaptionFont, Screen(focus) - new Vector2(38, 40), "▼ 训练目标", fontSize: 14, modulate: Gold);
            foreach (var source in World.Live(0).Where(p => p.LastReceipt > 0 && World.Time - p.ReceiptAt < 2.3f))
            {
                float age = World.Time - source.ReceiptAt;
                float lift = source.T.IsOilDerrick ? 33 * Zoom : 13;
                DrawString(CaptionFont, Screen(source.P) - new Vector2(15, lift + age * 8),
                    "+" + source.LastReceipt, fontSize: 15, modulate: Gold);
            }
        }

        if (Mark is Point marked && World.VisualTime - MarkTime < .62f)
        {
            if (MarkedTarget is Piece target && target.Alive && World.Visible(target))
                DrawCornerMarks(VisibleBody(target, Screen(target.P)).Grow(2),
                    new Color(Colors.White, 1 - (World.VisualTime - MarkTime) / .62f));
            else
            {
                var p = Screen(marked);
                DrawLine(p - new Vector2(5, 0), p + new Vector2(5, 0), Gold, 1);
                DrawLine(p - new Vector2(0, 4), p + new Vector2(0, 4), Gold, 1);
            }
        }
    }

    private void DrawShots()
    {
        foreach (var shot in World.Shots)
        {
            float age = World.VisualTime - shot.Born;
            if (age > .14f || (!FullSceneVisibility && (!World.Explored(0, shot.A) || !World.Explored(0, shot.B)))) continue;
            var from = Screen(shot.A) - new Vector2(0, 8 * Zoom);
            if (shot.Weapon == "tank")
            {
                var box = VehicleBox(Campaign.Info("tank"), Screen(shot.A));
                from = box.Position + (tankMounts[TankFrame(shot.Heading)] + tankMuzzles[TankFrame(shot.TurretHeading)]) * box.Size;
            }
            var to = Screen(shot.B) - new Vector2(0, shot.ImpactLift * Zoom);
            DrawLine(from, to, new Color("#e8cb90", 1 - age / .14f), shot.Shell ? 2 : 1);
            DrawCircle(from, (shot.Shell ? 3 : 1.7f) * Zoom, new Color("#ffdf9b"));
        }
    }
}
