using System;
using System.Drawing;

namespace Greyhaven {
public sealed partial class RiverbendMap {
    internal const int ChapterOneWidth = 88, ChapterOneHeight = 58;
    internal static readonly Point LandingBase = new Point(21, 44);
    internal static readonly Point HarborOffice = new Point(45, 23);
    internal const int InlandRoadExit = 103;
    internal static readonly Rectangle LandingYard = new Rectangle(18, 34, 16, 12);
    internal static readonly Rectangle PortRoad = new Rectangle(29, 29, 91, 6);
    internal static readonly RectangleF HarborBarrier = new RectangleF(58 * Campaign.Tile, 29 * Campaign.Tile, 9, 6 * Campaign.Tile);
    internal static readonly PointF HarborGateCenter = Campaign.Middle(HarborBarrier);
    internal static readonly RectangleF[] HarborGatePosts = { HarborGateBase(-1), HarborGateBase(1) };

    internal static PointF HarborGatePost(int side) {
        return new PointF(HarborGateCenter.X, HarborGateCenter.Y + side * 3.04f * Campaign.Tile);
    }

    static RectangleF HarborGateBase(int side) {
        var post = HarborGatePost(side);
        return new RectangleF(post.X - .405f * Campaign.Tile, post.Y - .32f * Campaign.Tile,
            .81f * Campaign.Tile, .64f * Campaign.Tile);
    }

    internal bool IsChapterOne { get; private set; }
    internal static RiverbendMap ChapterOne() { return new RiverbendMap(false, false, null, false, true); }

    internal static PointF PortPoint(float x, float y) {
        return new PointF((x + .5f) * Campaign.Tile, (y + .5f) * Campaign.Tile);
    }

    void GenerateChapterOne() {
        for (int y = 0; y < HeightCells; y++)
            for (int x = 0; x < WidthCells; x++)
                Cells[y * WidthCells + x] = ChapterOneGround(x, y);
        coastReady = true;

        for (int y = 35; y <= 38; y++)
            for (int x = 32; x <= 36; x++)
                if (x >= 34 || y >= 37) Ore.Add(new Point(x, y));
        for (int y = 38; y <= 42; y++)
            for (int x = 68; x <= 74; x++)
                if (x >= 70 || y >= 40) Ore.Add(new Point(x, y));

        Label("先遣卸载坡道", 17, 45, 1);
        Label("滩头展开区", LandingBase.X, LandingBase.Y, 1);
        Label("近矿", 34, 37, 0);
        Label("旧码头", 45, 23, 5);
        Label("港区货运闸", 58, 31, 5);
        Label("往河湾镇", 86, 31, 0);
    }

    Cell ChapterOneGround(int x, int y) {
        int detail = Hash(x + 1207, y + 171);
        // Both berths face the same bay. Wet, shore, and dock tiles are shared
        // by the battle view, placement, radar, and navigation.
        double northShore = 18.7 + Math.Sin(x * .22) * 1.25 - Math.Max(0, x - 54) * .66;
        double westShore = 7.4 + (y - 30) * .24 + Math.Max(0, y - 42) * .63 + Math.Sin(y * .36) * 1.55;
        double coast = Math.Min(y + .5 - northShore, x + .5 - westShore);
        var cell = new Cell { Variation = detail };
        if (PortRoad.Contains(x, y) || x >= 25 && x < 31 && y >= 32 && y < 37)
            cell.Ground = Surface.Road;
        else if (LandingYard.Contains(x, y) || x >= 37 && x < 56 && y >= 19 && y < 29 ||
                 x >= 40 && x < 49 && y >= 14 && y < 20)
            cell.Ground = Surface.Square;
        else if (x >= 16 && x < 21 && y >= 42 && y < 48)
            cell.Ground = Surface.Ramp;
        else if (coast < 0 || x < 16 && y >= 44 && y < 49 || x >= 49 && x < 61 && y < 18) cell.Ground = Surface.Water;
        else if (coast < 2.1) cell.Ground = Surface.Sand;
        else {
            bool nearOre = x >= 31 && x <= 37 && y >= 35 && y <= 40 ||
                           x >= 67 && x <= 75 && y >= 38 && y <= 43;
            bool wooded = x >= 35 && x <= 53 && y >= 40 && y <= 52 ||
                          x >= 62 && x <= 81 && y >= 16 && y <= 26 ||
                          x >= 81 && x <= 89 && y >= 39 && y <= 52;
            bool bare = Math.Sin(x * .31 + y * .16) + Math.Cos(y * .43 - x * .09) > 1.58;
            cell.Ground = nearOre || bare && detail % 6 != 0 ? Surface.Soil : Surface.Meadow;
            cell.Tree = wooded && !nearOre && detail % 7 < 2;
        }
        return cell;
    }
}
}
