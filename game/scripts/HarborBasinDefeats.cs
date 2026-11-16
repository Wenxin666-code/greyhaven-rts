using System.Linq;
using Godot;

public partial class HarborGame
{
    private Control basinDefeatFeed;
    private RichTextLabel basinDefeatLines;

    private void MakeBasinDefeatFeed()
    {
        basinDefeatFeed = Strip(ui, "#1a262af5", "#65746a");
        basinDefeatFeed.MouseFilter = Control.MouseFilterEnum.Ignore;
        basinDefeatFeed.ZIndex = 3;
        basinDefeatLines = Paragraph(basinDefeatFeed, 12, 5, 475, 83, 17);
    }

    private void UpdateBasinDefeatFeed()
    {
        var events = world.BasinOperation?.Orders.Defeats;
        basinDefeatFeed.Visible = started && !sequence.Opening && !sequence.Departing && !Finished &&
            !ReadingArchive && !paused && events != null && events.Count > 0;
        if (!basinDefeatFeed.Visible) return;
        var lines = events.Where(entry => world.VisualTime - entry.At <= 18).TakeLast(3).Select(entry =>
        {
            string defeated = world.StoryFactionName(entry.Defeated);
            return world.HasSurrendered(entry.Defeated) ? entry.Winner >= 0
                ? $"{world.StoryFactionName(entry.Winner)} 迫使 {defeated} 投降" : $"{defeated} 已投降"
                : entry.Winner < 0 ? $"{defeated} 退出战斗"
                : $"{world.StoryFactionName(entry.Winner)} 击败了 {defeated}";
        }).ToArray();
        basinDefeatFeed.Visible = lines.Length > 0;
        if (lines.Length == 0) return;
        float y = !ReadingRoadOrder && basinObjective.Visible ? basinObjective.Position.Y + 40 : 12;
        Position(basinDefeatFeed, 16, y, 497, 14 + 23 * lines.Length);
        ColorParagraph(basinDefeatLines, string.Join("\n", lines));
    }
}
