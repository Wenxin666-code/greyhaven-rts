using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private const int ChapterProgressFormat = 1;
    private const int MaxViewRecords = 100_000;
    private bool chapterSavesEnabled;
    private string chapterSaveDirectory;
    private AcceptDialog chapterProgressError;

    private sealed record ChapterProgress(Campaign World, ChapterSequence Sequence, double BattleSeconds,
        int Speed, Point Camera, float Zoom, int Tab, int[] Selection, Dictionary<int, int[]> Groups,
        Point[] Beacons);

    private string ChapterProgressPath(CampaignChapter chapter) => Path.Combine(
        chapterSaveDirectory ?? ProjectSettings.GlobalizePath("user://chapter-saves"), chapter.SaveId + ".save");

    private bool HasChapterProgress(CampaignChapter chapter) =>
        chapterSavesEnabled && File.Exists(ChapterProgressPath(chapter));

    private void RefreshChapterProgressButtons()
    {
        bool saved = HasChapterProgress(chosenChapter);
        resumeChapterButton.Visible = saved;
        launchChapterButton.Text = saved ? "开始新进度" : chosenChapter == CampaignChapter.Harbor
            ? "开始序章" : "进入" + chosenChapter.Brief.Title;
        launchChapterButton.TooltipText = saved ? "开始新进度会替换本关存档。" : "";
        SetLit(launchChapterButton, !saved);
    }

    private void OpenChosenChapter()
    {
        if (HasChapterProgress(chosenChapter)) ResumeChosenChapter();
        else LaunchChapter(chosenChapter);
    }

    private void ProceedToNextChapter()
    {
        if (activeChapter.Next is not { } next || !SaveBeforeLeavingChapter()) return;
        if (!HasChapterProgress(next))
        {
            LaunchChapter(next);
            return;
        }
        chosenChapter = next;
        ResetChapter(false);
        OpenFront(FrontPage.Missions);
    }

    private bool SaveBeforeLeavingChapter()
    {
        if (!chapterSavesEnabled || !started) return true;
        try
        {
            string path = ChapterProgressPath(activeChapter);
            if (Finished)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else WriteChapterProgress(path);
            return true;
        }
        catch (Exception error) when (ProgressError(error))
        {
            ShowProgressError("存档保存失败。请检查剩余空间，确认存档目录可以写入后重试。", error);
            return false;
        }
    }

    private void ResumeChosenChapter() => EnterChapter(RestoreChosenChapter);

    private void RestoreChosenChapter()
    {
        try
        {
            var restored = ReadChapterProgress(chosenChapter, ChapterProgressPath(chosenChapter));
            ResetChapter(true, chosenChapter, restored);
        }
        catch (Exception error) when (ProgressError(error))
        {
            RefreshChapterProgressButtons();
            ShowProgressError("无法读取本关存档。你仍可选择开始新进度。", error);
        }
    }

    private static bool ProgressError(Exception error) => error is IOException or InvalidDataException
        or UnauthorizedAccessException or ArgumentException or OverflowException or TargetInvocationException;

    private void ShowProgressError(string message, Exception error)
    {
        GD.PushWarning(error.Message);
        if (chapterProgressError == null)
        {
            chapterProgressError = new AcceptDialog { Title = "存档", Theme = ui.Theme, Exclusive = true };
            AddChild(chapterProgressError);
        }
        chapterProgressError.DialogText = message;
        chapterProgressError.PopupCentered();
    }

    private void WriteChapterProgress(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write))
            {
                using (var compressed = new GZipStream(file, CompressionLevel.Fastest, leaveOpen: true))
                {
                    using var writer = new BinaryWriter(compressed, Encoding.UTF8, leaveOpen: true);
                    writer.Write(ChapterProgressFormat);
                    writer.Write(Encoding.ASCII.GetBytes(activeChapter.SaveId));
                    writer.Write(battleSeconds);
                    writer.Write(multiplier);
                    WritePoint(writer, view.CameraPosition);
                    writer.Write(view.Zoom);
                    writer.Write(activeTab);
                    WriteIds(writer, selected);
                    writer.Write(groups.Count);
                    foreach (var (number, group) in groups)
                    {
                        writer.Write(number);
                        WriteIds(writer, group);
                    }
                    writer.Write(view.Beacons.Count);
                    foreach (var beacon in view.Beacons) WritePoint(writer, beacon);
                    BattleStateArchive.Save(compressed, world, sequence);
                }
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static ChapterProgress ReadChapterProgress(CampaignChapter chapter, string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > 32 * 1024 * 1024) throw new InvalidDataException("Checkpoint is too large.");
        using var compressed = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(compressed, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != ChapterProgressFormat ||
            Encoding.ASCII.GetString(reader.ReadBytes(chapter.SaveId.Length)) != chapter.SaveId)
            throw new InvalidDataException("Unknown checkpoint version or chapter.");
        double time = reader.ReadDouble();
        int speed = reader.ReadInt32();
        var camera = ReadPoint(reader);
        float zoom = reader.ReadSingle();
        int tab = reader.ReadInt32();
        if (!double.IsFinite(time) || time < 0 || speed is < 1 or > 4 || !float.IsFinite(zoom) ||
            zoom is < .5f or > 2.4f || tab is < 0 or > 3)
            throw new InvalidDataException("Invalid saved camera or battle controls.");
        int[] selection = ReadIds(reader);
        var savedGroups = new Dictionary<int, int[]>();
        int groupCount = ReadCount(reader, 10);
        for (int i = 0; i < groupCount; i++)
        {
            int number = reader.ReadInt32();
            if (number is < 0 or > 9) throw new InvalidDataException("Invalid saved control group.");
            savedGroups.Add(number, ReadIds(reader));
        }
        var beacons = new Point[ReadCount(reader, MaxViewRecords)];
        for (int i = 0; i < beacons.Length; i++) beacons[i] = ReadPoint(reader);

        var restored = chapter.Open();
        var timeline = chapter.CreateSequence(restored);
        BattleStateArchive.Restore(compressed, restored, timeline);
        return new(restored, timeline, time, speed, camera, zoom, tab, selection, savedGroups, beacons);
    }

    private void RestorePlayerView(ChapterProgress progress)
    {
        battleSeconds = progress.BattleSeconds;
        multiplier = progress.Speed;
        activeTab = progress.Tab;
        var byId = world.Live(0).ToDictionary(unit => unit.Id);
        Piece[] FindUnits(int[] ids) => ids.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
        selected.AddRange(FindUnits(progress.Selection));
        foreach (var (number, ids) in progress.Groups) groups.Add(number, FindUnits(ids));
        view.Beacons.AddRange(progress.Beacons);
        view.Selection = selected.ToArray();
        if (view.Cinematic) CenterSequence();
        else view.Center(progress.Camera, progress.Zoom);
    }

    private static void WritePoint(BinaryWriter writer, Point point)
    {
        writer.Write(point.X);
        writer.Write(point.Y);
    }

    private static Point ReadPoint(BinaryReader reader)
    {
        var point = new Point(reader.ReadSingle(), reader.ReadSingle());
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new InvalidDataException("Invalid saved point.");
        return point;
    }

    private static void WriteIds(BinaryWriter writer, IReadOnlyCollection<Piece> pieces)
    {
        writer.Write(pieces.Count);
        foreach (var piece in pieces) writer.Write(piece.Id);
    }

    private static int[] ReadIds(BinaryReader reader)
    {
        var ids = new int[ReadCount(reader, MaxViewRecords)];
        for (int i = 0; i < ids.Length; i++) ids[i] = reader.ReadInt32();
        return ids;
    }

    private static int ReadCount(BinaryReader reader, int limit)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > limit) throw new InvalidDataException("Invalid checkpoint list.");
        return count;
    }
}
