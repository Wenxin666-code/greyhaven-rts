using System;
using System.Collections.Generic;
using System.Drawing;

namespace Greyhaven {
public sealed partial class RiverbendMap {
    internal const int BasinWidth = 270, BasinHeight = 180;
    internal bool IsBasin { get; private set; }
    internal static readonly Point TransferLot = new Point(19, 121);
    internal static readonly PointF BasinRoadEntry = new PointF(-68, 104);
    internal static readonly PointF BasinLayby = new PointF(16, 114);

    internal static Point[] BasinStarts() => new[] { new Point(9, 123), new Point(55, 44),
        new Point(145, 31), new Point(158, 106), new Point(79, 164), new Point(241, 167) };
    static readonly Point[] basinPosts = { new Point(47, 36), new Point(175, 26),
        new Point(145, 97), new Point(54, 127), new Point(251, 155) };
    internal static readonly Point[] BasinChokes = { new(40, 37), new(175, 20), new(145, 91), new(48, 124), new(253, 149) };
    internal Point BasinPost(int side) => basinPosts[side - 1];

    // Cross-track barriers stop trains. Nearby gaps leave the shoulders usable by ground forces.
    static readonly Rectangle[][] basinWallRuns = {
        new[] { new Rectangle(31, 36, 4, 1), new Rectangle(43, 37, 4, 1) },
        new[] { new Rectangle(174, 13, 1, 5), new Rectangle(174, 26, 1, 3) },
        new[] { new Rectangle(144, 82, 1, 7), new Rectangle(144, 96, 1, 3) },
        new[] { new Rectangle(40, 124, 6, 1), new Rectangle(54, 124, 3, 1) },
        new[] { new Rectangle(253, 141, 1, 5), new Rectangle(253, 154, 1, 2) }
    };
    static readonly Rectangle[] basinRailBarriers = {
        new(38, 37, 5, 1), new(174, 18, 1, 5), new(144, 89, 1, 4), new(46, 124, 5, 1), new(253, 146, 1, 5)
    };
    internal IReadOnlyList<Rectangle> BasinWallRuns(int side) => basinWallRuns[side - 1];
    internal Rectangle BasinRailBarrier(int side) => basinRailBarriers[side - 1];

    internal static readonly IReadOnlyList<PointF> BasinRail = RoundBends(new[] {
        new PointF(-4, 109), new PointF(10, 110), new PointF(20, 106), new PointF(26, 91),
        new PointF(27, 72), new PointF(31, 49), new PointF(45, 32), new PointF(59, 23),
        new PointF(101, 20), new PointF(171, 20), new PointF(207, 24), new PointF(233, 35),
        new PointF(246, 51), new PointF(244, 63), new PointF(226, 78), new PointF(192, 87),
        new PointF(145, 91), new PointF(102, 92), new PointF(73, 97), new PointF(53, 111),
        new PointF(47, 126), new PointF(62, 145), new PointF(97, 153), new PointF(148, 152),
        new PointF(196, 148), new PointF(230, 152), new PointF(264, 147), new PointF(308, 128),
        new PointF(350, 109), new PointF(364, 103)
    });
    static readonly IReadOnlyList<PointF> basinRoad = RoundBends(new[] {
        new PointF(-90, 102), BasinRoadEntry, new PointF(-40, 116), new PointF(-12, 116),
        new PointF(9, 115), new PointF(20, 116), new PointF(25, 117)
    });

    readonly HashSet<long> basinRails = new HashSet<long>();
    readonly HashSet<long> basinRoads = new HashSet<long>();
    readonly HashSet<long> basinValley = new HashSet<long>();
    readonly HashSet<long> basinLowerRidge = new HashSet<long>();
    readonly HashSet<long> basinUpperRidge = new HashSet<long>();
    readonly HashSet<long> basinRiver = new HashSet<long>();
    readonly HashSet<long> basinFloodplain = new HashSet<long>();

    internal static RiverbendMap Basin() => new RiverbendMap(false, false, null, basin: true);

    static IReadOnlyList<PointF> RoundBends(IReadOnlyList<PointF> path) {
        var result = new List<PointF> { path[0] };
        for (int i = 1; i < path.Count - 1; i++) {
            var pivot = path[i];
            PointF Approach(PointF neighbor) {
                float fraction = Math.Min(.35f, 6 / Campaign.Dist(neighbor, pivot));
                return new PointF(pivot.X + (neighbor.X - pivot.X) * fraction, pivot.Y + (neighbor.Y - pivot.Y) * fraction);
            }
            var entry = Approach(path[i - 1]);
            var exit = Approach(path[i + 1]);
            for (int step = 0; step <= 5; step++) {
                float t = step / 5f, lead = 1 - t;
                result.Add(new PointF(lead * lead * entry.X + 2 * lead * t * pivot.X + t * t * exit.X,
                    lead * lead * entry.Y + 2 * lead * t * pivot.Y + t * t * exit.Y));
            }
        }
        result.Add(path[path.Count - 1]);
        return result.AsReadOnly();
    }

    static void Trace(HashSet<long> cells, IReadOnlyList<PointF> path, float radius) {
        float radiusSquared = radius * radius;
        for (int i = 1; i < path.Count; i++) {
            var a = path[i - 1];
            var b = path[i];
            float dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
            if (length <= 0) continue;
            int left = (int)Math.Floor(Math.Min(a.X, b.X) - radius), right = (int)Math.Ceiling(Math.Max(a.X, b.X) + radius);
            int top = (int)Math.Floor(Math.Min(a.Y, b.Y) - radius), bottom = (int)Math.Ceiling(Math.Max(a.Y, b.Y) + radius);
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
                float px = x + .5f - a.X, py = y + .5f - a.Y;
                float fraction = Math.Clamp((px * dx + py * dy) / length, 0, 1);
                float horizontal = px - dx * fraction, vertical = py - dy * fraction;
                if (horizontal * horizontal + vertical * vertical <= radiusSquared) cells.Add(CellKey(x, y));
            }
        }
    }

    Cell BasinGround(int x, int y) {
        var key = CellKey(x, y);
        int seed = Hash(x + 593, y + 211);
        bool rail = basinRails.Contains(key), road = basinRoads.Contains(key);
        bool water = basinRiver.Contains(key);
        var cell = new Cell { Ground = Surface.Meadow, Variation = seed, Railroad = rail };
        if (rail || road) {
            cell.Ground = water ? Surface.Bridge : rail && !road ? Surface.Square : Surface.Road;
            return cell;
        }
        if (!basinValley.Contains(key)) {
            float ridge = .55f * MathF.Sin(x * .096f + y * .047f) + .38f * MathF.Sin(y * .143f - x * .036f);
            cell.Cliff = true;
            cell.Ground = Surface.Rock;
            cell.Height = basinLowerRidge.Contains(key) ? 1.45f + ridge * .28f
                : basinUpperRidge.Contains(key) ? 3.15f + ridge * .55f : 5.8f + ridge * 1.4f;
            return cell;
        }
        if (water) { cell.Ground = Surface.Water; return cell; }
        if (basinFloodplain.Contains(key)) {
            cell.Ground = Surface.Marsh;
            return cell;
        }
        if (seed % 43 == 0) cell.Ground = Surface.Soil;
        return cell;
    }

    void GenerateBasin() {
        Trace(basinRails, BasinRail, 2.15f);
        Trace(basinRoads, basinRoad, 3.15f);
        Trace(basinValley, BasinRail, 8.6f);
        Trace(basinLowerRidge, BasinRail, 13);
        Trace(basinUpperRidge, BasinRail, 20);
        Trace(basinValley, basinRoad, 5.1f);
        Trace(basinLowerRidge, basinRoad, 10);
        Trace(basinUpperRidge, basinRoad, 17);
        foreach (var start in Starts) OpenBasinPocket(start.X, start.Y, 17, 14);
        OpenBasinPocket(20, 117, 12, 10);
        foreach (var post in basinPosts) OpenBasinPocket(post.X, post.Y, 5, 5);
        foreach (var creek in new[] {
            new[] { new PointF(109, 134), new PointF(114, 150), new PointF(117, 173) },
            new[] { new PointF(224, 53), new PointF(246, 51), new PointF(264, 49) }
        }) {
            Trace(basinRiver, creek, 2.2f);
            Trace(basinFloodplain, creek, 4.2f);
        }

        for (int y = 0; y < HeightCells; y++) for (int x = 0; x < WidthCells; x++) Cells[y * WidthCells + x] = BasinGround(x, y);
        coastReady = true;

        foreach (var start in Starts) {
            FlattenBasinLot(new Rectangle(start.X - 9, start.Y - 8, 19, 17), Surface.Meadow);
            FlattenBasinLot(new Rectangle(start.X - 5, start.Y - 4, 10, 8), Surface.Square);
        }
        FlattenBasinLot(new Rectangle(14, 116, 16, 10), Surface.Square);
        for (int side = 1; side <= basinPosts.Length; side++) {
            var post = BasinPost(side);
            FlattenBasinLot(new Rectangle(post.X - 1, post.Y, 5, 4), Surface.Square);
            Label(BasinRoadOrder.All[side - 1].Checkpoint, post.X + 2, post.Y + 3, side);
        }
        AddHouse("shop", "货运仓房", 26, 120, 3, 2, 6);
        AddHouse("residence", "北坡巡道房", 58, 35, 2, 2, 4);
        AddHouse("shop", "白石装矿台", 160, 30, 3, 2, 6);
        AddHouse("shop", "铁路检修库", 152, 96, 3, 2, 6);
        AddHouse("shop", "南堤备料场", 65, 155, 3, 2, 6);
        AddHouse("residence", "石门巡检所", 258, 153, 2, 2, 4);
        foreach (var field in new[] { new Point(2, 129), new Point(42, 48), new Point(132, 38), new Point(145, 111),
            new Point(66, 167), new Point(228, 170), new Point(85, 23), new Point(195, 89), new Point(130, 154) }) {
            var area = new Rectangle(field.X, field.Y, 7, 5);
            FlattenBasinLot(area, Surface.Soil);
            for (int y = area.Top; y < area.Bottom; y++) for (int x = area.Left; x < area.Right; x++)
                if (At(x, y)?.Ground == Surface.Soil) Ore.Add(new Point(x, y));
        }
        Label("河湾来路", BasinRoadEntry.X + 13, BasinRoadEntry.Y, 0);
        Label("盆地换装站", TransferLot.X + 1, TransferLot.Y + 1, 0);
        Label("石门方向", 296, 129, 0);
    }

    void OpenBasinPocket(int centerX, int centerY, int width, int height) {
        for (int y = centerY - height - 8; y <= centerY + height + 8; y++)
        for (int x = centerX - width - 8; x <= centerX + width + 8; x++) {
            long key = CellKey(x, y);
            if (Ellipse(x, y, centerX, centerY, width, height) <= 1) basinValley.Add(key);
            if (Ellipse(x, y, centerX, centerY, width + 3, height + 3) <= 1) basinLowerRidge.Add(key);
            if (Ellipse(x, y, centerX, centerY, width + 8, height + 8) <= 1) basinUpperRidge.Add(key);
        }
    }

    void FlattenBasinLot(Rectangle lot, Surface ground) {
        for (int y = lot.Top; y < lot.Bottom; y++) for (int x = lot.Left; x < lot.Right; x++) {
            var cell = At(x, y);
            if (cell == null || cell.Cliff || cell.Railroad || cell.Ground == Surface.Road || cell.Ground == Surface.Bridge || cell.Ground == Surface.Water) continue;
            cell.Ground = ground;
            cell.Height = 0;
            cell.Tree = false;
        }
    }
}
}
