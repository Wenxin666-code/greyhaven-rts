using Godot;

public partial class HarborGame
{
    private const int ArchiveColumns = 2;
    private Control archivePanel;
    private Button archiveEntry, archiveResult, archiveBack;
    private Button[] archiveCards;
    private Texture2D[] archiveMarks;
    private TextureRect archiveMark;
    private ColorRect archiveAccent, archiveFlag;
    private Label archiveName, archiveRegion;
    private RichTextLabel archiveArticle;
    private int archiveIndex;

    private bool ReadingArchive => frontPage == FrontPage.Archive;

    private void MakeArchive(Control front)
    {
        archivePanel = Page(front);
        Accent(archivePanel, 0, 55, 1472, 719, new Color("#101b20db"));
        Bold(archivePanel, "战役档案", 60, 78, 490, 60, 43);
        LabelAt(archivePanel, "重连灰港  /  势力档案", 64, 143, 460, 28, 17).Modulate = Gold;

        archiveCards = new Button[FactionArchive.Records.Length];
        archiveMarks = new Texture2D[FactionArchive.Records.Length];
        for (int i = 0; i < FactionArchive.Records.Length; i++)
        {
            int index = i;
            var faction = FactionArchive.Records[i];
            var color = BattleArt.Faction(i);
            var card = AddButton(archivePanel, "", 64 + i % ArchiveColumns * 242, 198 + i / ArchiveColumns * 145,
                224, 125, () => SelectArchive(index));
            card.TooltipText = faction.Name;
            card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            archiveCards[i] = card;
            archiveMarks[i] = GD.Load<Texture2D>($"res://assets/factions/{faction.Mark}.svg");
            Accent(card, 14, 13, 196, 60, color.Darkened(.37f));
            AddArchiveMark(card, 72, 16, 80, 54).Texture = archiveMarks[i];
            var title = Bold(card, faction.ShortName, 8, 78, 208, 33, 18);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Name = "FactionName";
        }

        var file = SteelPanel(archivePanel, 574, 98, 834, 622);
        archiveAccent = Accent(file, 0, 1, 6, 146);
        archiveFlag = Accent(file, 36, 28, 159, 106);
        archiveMark = AddArchiveMark(archiveFlag, 17, 10, 125, 86);
        archiveRegion = LabelAt(file, "", 218, 80, 578, 37, 19);
        archiveName = Bold(file, "", 216, 33, 581, 54, 37);
        Accent(file, 36, 156, 761, 1, Steel);
        archiveArticle = Paragraph(file, 36, 179, 764, 415, 21);
        archiveArticle.ScrollActive = true;
        archiveArticle.MouseFilter = Control.MouseFilterEnum.Stop;
        archiveArticle.MouseForcePassScrollEvents = false;
        archiveArticle.AddThemeFontOverride("bold_font", uiBold);
        archiveArticle.AddThemeConstantOverride("line_separation", 7);
        archiveArticle.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginRight = 13 });
        var scrollbar = archiveArticle.GetVScrollBar();
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat
            { BgColor = Ink, ContentMarginLeft = 5, ContentMarginRight = 5 });
        scrollbar.AddThemeStyleboxOverride("grabber", new StyleBoxFlat { BgColor = Steel });
        scrollbar.AddThemeStyleboxOverride("grabber_highlight", new StyleBoxFlat { BgColor = Dim });
        scrollbar.AddThemeStyleboxOverride("grabber_pressed", new StyleBoxFlat { BgColor = Gold });
        archiveBack = AddButton(archivePanel, "‹  返回主界面", 64, 676, 224, 44, CloseArchive);
        SelectArchive(0);
    }

    private static TextureRect AddArchiveMark(Control parent, float x, float y, float w, float h)
    {
        var mark = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(mark);
        Position(mark, x, y, w, h);
        return mark;
    }

    private void SelectArchive(int index)
    {
        archiveIndex = index;
        var faction = FactionArchive.Records[archiveIndex];
        var color = BattleArt.Faction(archiveIndex);
        archiveAccent.Color = color;
        archiveFlag.Color = color.Darkened(.37f);
        archiveMark.Texture = archiveMarks[archiveIndex];
        archiveName.Text = faction.Name;
        archiveRegion.Text = faction.Region;
        archiveRegion.Modulate = color.Lerp(Pale, .44f);
        archiveArticle.Text = $"""
            [color=#{Gold.ToHtml(false)}][b]主要人物[/b][/color]
            {faction.People}

            [color=#{Gold.ToHtml(false)}][b]战争年代[/b][/color]
            {faction.Origins}

            [color=#{Gold.ToHtml(false)}][b]停火时期[/b][/color]
            {faction.Ceasefire}

            [color=#{Gold.ToHtml(false)}][b]断线后的立场[/b][/color]
            {faction.Position}
            """;
        archiveArticle.GetVScrollBar().Value = 0;
        for (int i = 0; i < archiveCards.Length; i++)
        {
            bool active = i == archiveIndex;
            SetLit(archiveCards[i], active);
            archiveCards[i].GetNode<Label>("FactionName").Modulate = active ? Ink : Pale;
        }
    }

    private void OpenArchive()
    {
        archiveBack.Text = started ? "‹  返回结算" : "‹  返回主界面";
        OpenFront(FrontPage.Archive);
    }

    private void CloseArchive() => OpenFront(FrontPage.Menu);
}
