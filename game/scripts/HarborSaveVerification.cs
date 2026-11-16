using Godot;
using Greyhaven;
using System;
using System.IO;
using System.Linq;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private void RunSaveVerification()
    {
        chapterSaveDirectory = Path.Combine(Path.GetTempPath(), "greyhaven-save-test-" + Guid.NewGuid().ToString("N"));
        chapterSavesEnabled = true;
        try
        {
            VerifyOpeningProgress();
            VerifyBattleProgress();
            VerifyDispatchProgress();
            GD.Print("PASS: independent chapter saves, opening, battle orders, dispatches, closing, restart and invalid files.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            foreach (var chapter in CampaignChapter.Playable) File.Delete(ChapterProgressPath(chapter));
            if (Directory.Exists(chapterSaveDirectory)) Directory.Delete(chapterSaveDirectory);
        }
    }

    private void VerifyOpeningProgress()
    {
        LaunchChapter(CampaignChapter.Harbor);
        for (int i = 0; i < 80; i++) Simulate((float)MatchTempo.Step);
        var scene = world.HarborOperation.Scene;
        float elapsed = world.HarborOperation.Elapsed;
        ReturnToMissions();
        Expect(!started && resumeChapterButton.Visible && launchChapterButton.Text == "开始新进度", "Leaving the opening offers both progress choices");
        OpenChosenChapter();
        Expect(started && sequence.Opening && world.HarborOperation.Scene == scene &&
            world.HarborOperation.Elapsed == elapsed, "Continue resumes the opening at the saved scene and time");
        Simulate((float)MatchTempo.Step);
        Expect(world.HarborOperation.Elapsed > elapsed, "The recovered opening advances");
    }

    private void VerifyBattleProgress()
    {
        ReturnToMissions();
        LaunchChapter(CampaignChapter.Riverbend);
        WaitFor(() => sequence.CanTakeCommand, 80, "Riverbend handoff for save test");
        TakeCommand();
        var point = world.Home(0);
        var vehicle = world.Spawn("ifv", 0, point.X, point.Y);
        var passenger = world.Spawn("engineer", 0, point.X, point.Y);
        passenger.Carrier = vehicle;
        var factory = world.Spawn("factory", 0, point.X - 80, point.Y - 80);
        factory.Queue.Enqueue(Campaign.Info("tank"));
        factory.Work = 7.25f;
        factory.WorkPaid = 174;
        vehicle.Waypoints.Add(new Point(point.X + 40, point.Y));
        world.Funds[0] = 4321;
        SetSpeed(3);
        selected.Clear();
        selected.Add(vehicle);
        SaveGroup(3);
        view.Beacons.Add(point);
        view.Center(point, 1.8f);
        battleSeconds = 95;
        float time = world.Time;
        ReturnToMissions();
        Expect(HasChapterProgress(CampaignChapter.Harbor) && HasChapterProgress(CampaignChapter.Riverbend),
            "Harbor and Riverbend use independent slots");
        ResumeChosenChapter();
        var restored = world.Live(0).Single(p => p.Id == vehicle.Id);
        var works = world.Live(0).Single(p => p.Id == factory.Id);
        Expect(CanControl && battleSeconds == 95 && world.Time == time && multiplier == 3 &&
            world.Funds[0] == 4321 && view.Zoom == 1.8f && view.Beacons.Single() == point,
            "Continuing restores control, time, funds, camera and beacon");
        Expect(ReferenceEquals(restored.Passenger.Carrier, restored) && restored.Passenger.Id == passenger.Id &&
            ReferenceEquals(groups[3].Single(), restored) && ReferenceEquals(selected.Single(), restored) &&
            restored.Waypoints.SequenceEqual(vehicle.Waypoints), "Units, orders, passengers and control groups keep their links");
        Expect(ReferenceEquals(works.Queue.Peek(), Campaign.Info("tank")) && works.WorkPaid == 174 && works.Work == 7.25f,
            "The shared production queue and paid progress are restored");

        ReturnToMissions();
        LaunchChapter(CampaignChapter.Riverbend);
        Expect(sequence.Opening && world.Time == 0 && world.Funds[0] != 4321 &&
            ReadChapterProgress(activeChapter, ChapterProgressPath(activeChapter)).Sequence.Opening,
            "Starting new progress replaces the previous checkpoint");
    }

    private void VerifyDispatchProgress()
    {
        ReturnToMissions();
        LaunchChapter(CampaignChapter.Basin);
        WaitFor(() => sequence.CanTakeCommand, 130, "Basin handoff for save test");
        TakeCommand();
        var player = world.Live(0).First(p => world.Armed(p));
        foreach (var enemy in world.Units.Where(p => p.Alive && p.Side == 1).ToArray()) world.CombatKill(enemy, player);
        Simulate((float)MatchTempo.Step);
        UpdateInterface();
        Expect(ReadingRoadOrder, "A recovered order is open");
        var current = world.BasinOperation.Orders.Current;
        ReturnToMissions();
        ResumeChosenChapter();
        Expect(ReadingRoadOrder && world.BasinOperation.Orders.Current == current, "The same unread order reopens");
        ReadRemainingOrders();
        player = world.Live(0).First(p => world.Armed(p));
        foreach (var enemy in world.Units.Where(p => p.Alive && p.Side > 0 && !p.T.IsWall).ToArray())
            world.CombatKill(enemy, player);
        Simulate((float)MatchTempo.Step);
        UpdateInterface();
        ReadRemainingOrders();
        WaitFor(() => world.BasinOperation.Freight.Travel > Campaign.Tile, 20, "Train leaves before the closing checkpoint");
        float travel = world.BasinOperation.Freight.Travel;
        ReturnToMissions();
        ResumeChosenChapter();
        Expect(sequence.Departing && world.BasinOperation.Freight.Travel == travel &&
            world.BasinOperation.Orders.ReadCount == 5, "The train and acknowledged documents resume together");
        WaitFor(() => sequence.Complete, 100, "The recovered closing advances to the result");
        ReturnToMissions();
        Expect(!HasChapterProgress(CampaignChapter.Basin), "Finished missions no longer advertise an unfinished checkpoint");

        string path = ChapterProgressPath(CampaignChapter.Basin);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        bool rejected = false;
        try { ReadChapterProgress(CampaignChapter.Basin, path); }
        catch (Exception error) when (ProgressError(error)) { rejected = true; }
        Expect(rejected && !started && File.Exists(path), "An invalid save is rejected without changing the current session or file");
    }
}
