using System;
using System.Linq;
using Godot;
using Greyhaven;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private static void VerifyWalls()
    {
        var battle = new Campaign(tutorial: true);
        var type = Campaign.Info("wall");
        const int step = Campaign.Tile;
        static Point Cell(int x, int y) => new((x + .5f) * step, (y + .5f) * step);
        Piece At(string key, int side, int x, int y)
        {
            var location = Cell(x, y);
            return battle.Spawn(key, side, location.X, location.Y);
        }
        void Advance(float seconds)
        {
            for (int frame = 0; frame < seconds / MatchTempo.Step; frame++)
                battle.Tick((float)MatchTempo.Step);
        }
        void Place(Point point)
        {
            int money = battle.Funds[0];
            Expect(battle.StartConstruction(type), "Wall can be ordered in the defense line");
            Advance(type.Seconds + 1);
            Expect(battle.Construction(type)?.Ready == true && battle.Build(type, point), "Paid wall can be placed");
            Expect(battle.Funds[0] == money - 100, "Wall and its extensions use one purchase");
        }

        At("hq", 0, 16, 22);
        Expect(battle.Require(type, 0) != null, "A barracks is required to unlock walls");
        At("barracks", 0, 14, 24);
        At("power", 0, 13, 21);
        At("engineer", 0, 24, 22);
        Advance(.5f);
        var first = Cell(19, 20);
        var last = Cell(24, 20);
        Place(first);
        var blocker = At("squad", 0, 21, 20);
        var stopped = battle.AssessPlacement(type, last);
        Expect(stopped.Valid && stopped.WallExtensions.Count == 0, "An intervening unit stops wall extension");
        blocker.P = Cell(21, 24);
        var clear = battle.AssessPlacement(type, last);
        Expect(clear.Valid && clear.WallExtensions.Count == 4, "Five cells apart preview four free wall pieces");
        Place(last);
        var walls = battle.Live(0).Where(p => p.T.IsWall).OrderBy(p => p.P.X).ToArray();
        Expect(walls.Length == 6 && battle.WallConnections(walls[0]) == 2 && battle.WallConnections(walls[5]) == 8 &&
            walls.Skip(1).Take(4).All(w => battle.WallConnections(w) == 10), "The line has joins and distinct end caps");

        var truck = At("miner", 0, 22, 18);
        truck.Mining = false;
        var destination = Cell(22, 22);
        Expect(battle.Move(truck, destination, false) && truck.Path.Any(p => Math.Abs(p.X - destination.X) > step * 2),
            "A mining truck routes around a continuous wall");
        var breaker = At("tank", 0, 21, 22);
        var gap = walls[3];
        battle.Attack(breaker, gap, true);
        Advance(24);
        Expect(!gap.Alive && battle.WallConnections(walls[2]) == 8 && battle.WallConnections(walls[4]) == 2,
            "A tank can breach one segment and the adjacent frames update");
        truck.P = Cell(22, 18);
        Expect(battle.Move(truck, destination, false) && truck.Path.All(p => Math.Abs(p.X - destination.X) < step * .51f),
            "A one-cell breach immediately opens a route");
        Advance(12);
        Expect(Campaign.Dist(truck.P, destination) < step * .6f, "The mining truck passes through the breach");

        var screen = At("wall", 0, 30, 24);
        var infantry = At("squad", 0, 28, 24);
        var target = At("power", 1, 32, 24);
        battle.Attack(infantry, target);
        Advance(5);
        Expect(target.Hp == target.T.Hp && screen.Hp == screen.T.Hp &&
            battle.Shots.Any(shot => shot.SourceId == infantry.Id && shot.TargetId == screen.Id),
            "Small-arms fire impacts the wall without damaging it or the building beyond it");
        Expect(!battle.CanAttackTarget(infantry, screen) && !battle.ToggleRepair(screen), "Walls cannot be attacked by infantry or repaired");
        var howitzer = At("arty", 0, 25, 24);
        battle.Attack(howitzer, target);
        Advance(10);
        Expect(target.Hp < target.T.Hp && screen.Hp == screen.T.Hp, "Arcing artillery can shoot over a wall");

        int credits = battle.Funds[0];
        Expect(battle.Sell(walls[0]) && battle.Sell(walls[1]) && battle.Funds[0] == credits + 50,
            "Dismantling free connectors cannot generate money");
        GD.Print("PASS: walls / paid construction, valid auto-links, projectile screening, breaches, navigation and refunds.");
    }
}
