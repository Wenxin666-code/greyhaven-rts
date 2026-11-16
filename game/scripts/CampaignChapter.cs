using Greyhaven;
using System;
using Point = System.Drawing.PointF;

internal sealed record CampaignChapter(string SaveId, MissionBriefing Brief, string SceneTitle, Func<Campaign> Open,
    Func<Campaign, ChapterSequence> CreateSequence)
{
    internal static readonly CampaignChapter Harbor = new("harbor", MissionBriefing.Prologue, "重连灰港  /  第一章",
        Campaign.OpenGreyhaven, world => new HarborSequence(world));
    internal static readonly CampaignChapter Riverbend = new("riverbend", MissionBriefing.Riverbend, "河湾镇  /  第二章",
        () => new Campaign(riverbend: true), world => new RiverbendSequence(world));

    internal static readonly CampaignChapter Basin = new("basin", MissionBriefing.Basin, "盆地岔口  /  第三章",
        () => new Campaign(basin: true), world => new BasinSequence(world));

    internal static readonly CampaignChapter[] Playable = { Harbor, Riverbend, Basin };
    internal CampaignChapter Next => this == Harbor ? Riverbend : this == Riverbend ? Basin : null;
}

internal abstract class ChapterSequence
{
    internal const string Speaker = "无线电 · 许岑 / 作战参谋";
    internal abstract bool Opening { get; }
    internal abstract bool Departing { get; }
    internal abstract bool Complete { get; }
    internal virtual bool ResultReady => Complete;
    internal abstract bool CanTakeCommand { get; }
    internal abstract Piece CommandVehicle { get; }
    internal abstract float Fog { get; }
    internal abstract string Caption { get; }
    internal abstract string[] Debrief { get; }
    internal abstract Point OpeningPanTarget { get; }
    internal virtual string FieldRadio => null;
    internal virtual string Failure => "先遣队未能完成任务。";
    internal abstract bool TakeCommand();
    internal abstract bool BeginDeparture(Point from);
    internal abstract void Tick(float step);
    internal abstract Point Camera();
}
