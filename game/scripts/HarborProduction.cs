using Godot;
using Greyhaven;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HarborGame
{
    private const int QueuePageSize = 4;
    private readonly HashSet<string> seenCards = new();
    private readonly List<ProductionTile> tiles = new();
    private readonly List<Button> tabs = new();
    private readonly List<QueueRow> queueRows = new();
    private Control cardGrid, queueBoard, queueSummary;
    private Label queueTitle, queueCounter, pendingCount;
    private Button prevProducer, nextProducer, prevQueue, nextQueue, locateReady;
    private ProductionTile currentOrder;
    private int activeTab, queuePage, queueId;
    private bool queueOpen;
    private Piece[] queueSources = Array.Empty<Piece>();

    private sealed record ProductionTile(Panel Plate, TextureRect Portrait, Label Name, Label Status,
        Label Count, ColorRect Bar, ColorRect Shade, ColorRect ReadyPlate, Label Ready)
    {
        internal TypeInfo Type;
    }

    private sealed record QueueRow(Panel Plate, TextureRect Portrait, Label Name, Label Status, Button Cancel)
    {
        internal Piece Source;
        internal int Index;
        internal TypeInfo[] Snapshot;
    }

    private void MakeProduction()
    {
        string[] labels = { "建筑", "防御", "步兵", "车辆" };
        for (int i = 0; i < labels.Length; i++)
        {
            int tab = i;
            tabs.Add(AddButton(panel, labels[i], 8 + i * 39, 234, 36, 31, () => SetTab(tab), 12));
        }

        cardGrid = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(cardGrid);
        Position(cardGrid, 8, 274, 152, 300);
        for (int i = 0; i < 8; i++)
            tiles.Add(MakeTile(cardGrid, i % 2 * 80, i / 2 * 75));

        queueSummary = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(queueSummary);
        Position(queueSummary, 8, 581, 152, 156);
        Accent(queueSummary, 0, 0, 151, 1, Steel);
        pendingCount = LabelAt(queueSummary, "", 1, 9, 100, 20, 11);
        AddButton(queueSummary, "总览", 104, 5, 48, 27, OpenQueue, 11);
        currentOrder = MakeTile(queueSummary, 0, 38, true);
        locateReady = AddButton(queueSummary, "定位最新单位", 0, 101, 152, 29, LocateReady, 12);
        selectionText = LabelAt(queueSummary, "", 2, 135, 149, 21, 10);
        selectionText.ClipText = true;
        selectionText.Modulate = Teal;

        queueBoard = Strip(panel, "#17262b", "#57665f");
        Position(queueBoard, 8, 274, 152, 450);
        queueTitle = LabelAt(queueBoard, "", 2, 10, 94, 23, 12);
        queueTitle.ClipText = true;
        prevProducer = AddButton(queueBoard, "‹", 98, 3, 24, 28, () => CycleProducer(-1), 13);
        nextProducer = AddButton(queueBoard, "›", 128, 3, 24, 28, () => CycleProducer(1), 13);
        for (int row = 0; row < QueuePageSize; row++)
        {
            var tray = Strip(queueBoard, "#1d2c30", "#526267");
            Position(tray, 0, 42 + row * 60, 152, 55);
            var picture = Thumbnail(tray, 3, 6, 38, 36);
            var name = LabelAt(tray, "", 44, 6, 78, 19, 10);
            var status = LabelAt(tray, "", 44, 27, 78, 20, 10);
            name.ClipText = status.ClipText = true;
            status.Modulate = Gold;
            QueueRow entry = null;
            var cancel = AddButton(tray, "×", 126, 12, 24, 28, () => CancelQueueOrder(entry), 12);
            entry = new QueueRow(tray, picture, name, status, cancel);
            queueRows.Add(entry);
        }
        prevQueue = AddButton(queueBoard, "‹", 0, 284, 25, 24, () => ChangeQueuePage(-1), 12);
        queueCounter = LabelAt(queueBoard, "", 32, 285, 94, 23, 11);
        nextQueue = AddButton(queueBoard, "›", 127, 284, 25, 24, () => ChangeQueuePage(1), 12);
        AddButton(queueBoard, "返回生产栏", 0, 414, 152, 30, () => { queueOpen = false; UpdateInterface(); }, 12);
        queueBoard.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } mouse) return;
            if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                ChangeQueuePage(mouse.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
                queueBoard.AcceptEvent();
            }
        };
    }

    private ProductionTile MakeTile(Control parent, float x, float y, bool wide = false)
    {
        var plate = Strip(parent, "#18262a", "#42565c");
        float width = wide ? 152 : 72, height = wide ? 57 : 71;
        Position(plate, x, y, width, height);
        var picture = Thumbnail(plate, 3, 3, wide ? 39 : 66, wide ? 39 : 38);
        var shade = Accent(plate, 3, 3, wide ? 39 : 66, 38, new Color(Ink, .64f));
        var title = Bold(plate, "", wide ? 45 : 4, wide ? 3 : 39, wide ? 102 : 64, 18, 11);
        title.ClipText = true;
        var state = LabelAt(plate, "", wide ? 45 : 4, wide ? 26 : 55, wide ? 104 : 64, 16, 10);
        state.ClipText = true;
        var count = Bold(plate, "", wide ? 16 : 50, 2, 30, 17, 12);
        count.Modulate = Gold;
        var progress = Accent(plate, 3, height - 4, 0, 2);
        var readyPlate = Accent(plate, wide ? 45 : 3, wide ? 24 : 17, wide ? 80 : 66, 21, new Color(Ink, 225 / 255f));
        var ready = Bold(plate, "就 绪", wide ? 49 : 16, wide ? 23 : 15, 51, 21, 15);
        ready.Modulate = Teal;
        var tile = new ProductionTile(plate, picture, title, state, count, progress, shade, readyPlate, ready);
        plate.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: false } mouse || !CanControl) return;
            if (mouse.ButtonIndex is not MouseButton.Left and not MouseButton.Right || tile.Type == null) return;
            ProductionClick(tile.Type, mouse.ButtonIndex == MouseButton.Right, mouse.ShiftPressed ? 5 : 1);
            plate.AcceptEvent();
        };
        return tile;
    }

    private static TextureRect Thumbnail(Control parent, float x, float y, float w, float h)
    {
        var picture = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(picture);
        Position(picture, x, y, w, h);
        return picture;
    }

    private void SetTab(int tab)
    {
        if (!CanControl) return;
        activeTab = tab;
        queueOpen = false;
        structureTool = StructureTool.None;
        placing = tab < 2 ? ReadyConstruction(tab) : null;
        UpdateInterface();
    }

    private void CancelMapMode()
    {
        if (structureTool != StructureTool.None) structureTool = StructureTool.None;
        else if (placing != null) placing = null;
        else if (beaconMode) beaconMode = false;
        else
        {
            selected.Clear();
            selectedBeacon = view.SelectedBeacon = -1;
        }
        view.Placement = null;
        UpdateInterface();
    }

    private void ToggleStructureTool(StructureTool tool)
    {
        if (!CanControl) return;
        structureTool = structureTool == tool ? StructureTool.None : tool;
        placing = null;
        world.Radio = structureTool == StructureTool.Sell ? "出售模式：点己方建筑，右键退出。" :
            structureTool == StructureTool.Repair ? "维修模式：点受损建筑开关维修，右键退出。" : "已退出建筑工具。";
        UpdateInterface();
    }

    private Piece CardProducer(TypeInfo type)
    {
        if (type.Producer == "factory") return world.VehicleQueue(0);
        return world.Live(0).Where(p => p.T.Key == type.Producer && p.Queue.Contains(type))
            .OrderByDescending(p => selected.Contains(p)).ThenByDescending(p => p.Queue.Peek() == type)
            .ThenByDescending(p => p.ProductionPaused && p.Queue.Peek() == type)
            .ThenByDescending(p => p.Queue.Peek() == type ? p.Work : 0).ThenBy(p => p.Id).FirstOrDefault()
            ?? world.PrimaryFactory(0, type.Producer);
    }

    private void RefreshProduction()
    {
        cardGrid.Visible = !queueOpen;
        queueSummary.Visible = !queueOpen;
        queueBoard.Visible = queueOpen;
        for (int i = 0; i < tabs.Count; i++) SetLit(tabs[i], !queueOpen && activeTab == i);

        if (queueOpen) { RefreshQueue(); return; }
        foreach (var type in Campaign.Types)
        {
            string reason = world.Require(type, 0);
            if (reason == null || reason.Contains("队列已满") || world.Construction(type) != null)
                seenCards.Add(type.Key);
        }
        if (world.Live(0).Any(p => p.T.Key == "barracks")) seenCards.Add("sniper");
        var cards = Campaign.Types.Where(t => t.Key != "hq" && seenCards.Contains(t.Key) && (activeTab switch
        {
            0 => t.Building && !t.Defense,
            1 => t.Defense,
            2 => t.Foot,
            _ => !t.Building && !t.Foot
        })).ToArray();
        for (int i = 0; i < tiles.Count; i++)
        {
            var type = i < cards.Length ? cards[i] : null;
            if (type == null) ShowTile(tiles[i], null, "", 0, 0, false, false, false);
            else ShowProductionCard(tiles[i], type);
        }

        var producers = world.Live(0).Where(p => p.Queue.Count > 0).ToArray();
        int pending = producers.Sum(p => p.Queue.Count) + Enumerable.Range(0, 2).Count(c => world.Construction(0, c) != null);
        pendingCount.Text = $"排产 {pending} / {producers.Count(p => !p.ProductionPaused)}线";
        var building = activeTab < 2 ? world.Construction(0, activeTab) : null;
        if (building != null) ShowProductionCard(currentOrder, building.Type);
        else
        {
            var source = producers.OrderByDescending(p => selected.Contains(p)).ThenByDescending(p => p == world.LastOrder).FirstOrDefault();
            if (source != null) ShowProductionCard(currentOrder, source.Queue.Peek());
            else ShowTile(currentOrder, null, "生产线空闲", 0, 0, false, false, false);
        }

        locateReady.Disabled = world.LastReady?.Deployed != true;
    }

    private void ShowProductionCard(ProductionTile tile, TypeInfo type)
    {
        var job = type.Building ? world.Construction(type) : null;
        var source = type.Building ? null : CardProducer(type);
        bool active = source?.Queue.Count > 0 && source.Queue.Peek() == type;
        int count = type.Building ? 0 : world.Live(0).Where(p => p.T.Key == type.Producer).Sum(p => p.Queue.Count(t => t == type));
        string reason = type.Building ? world.ConstructionReason(type) : world.Require(type, 0);
        bool enabled = reason == null || active || count > 0;
        float speed = MatchTempo.DefaultRate * multiplier;
        string text = job != null ? world.ConstructionState(job, 0, speed)
            : active ? world.QueueState(source, speed) : enabled ? "$ " + type.Price : reason;
        float progress = job != null ? job.Work / type.Seconds : active ? source.Work / type.Seconds : 0;
        ShowTile(tile, type, text, progress, count, enabled, job?.Ready == true, placing == type || world.LessonKey == type.Key);
        tile.Plate.TooltipText = type.Name + "   $" + type.Price + "\n" +
            (reason ?? (type.Building ? "左键生产或放置，右键暂停／取消" : "左键排产，右键减单／暂停")) +
            $"\n生命 {type.Hp}    视野 {type.SightCells} 格";
    }

    private void ShowTile(ProductionTile tile, TypeInfo type, string text, float progress, int count, bool enabled, bool ready, bool lit)
    {
        if (tile.Type != type) tile.Portrait.Texture = type == null ? null : view.Art.Portrait(type);
        tile.Type = type;
        if (type == null) tile.Plate.TooltipText = "";
        tile.Name.Text = type?.Name ?? "";
        tile.Name.Modulate = enabled ? Pale : Dim;
        tile.Status.Text = !ready ? text : "";
        tile.Status.Modulate = enabled ? Gold : Dim;
        tile.Ready.Visible = tile.ReadyPlate.Visible = ready;
        tile.Count.Text = count > 0 ? count.ToString() : "";
        tile.Shade.Visible = type != null && !enabled;
        tile.Bar.Size = new Vector2((tile.Plate.Size.X - 6) * Math.Clamp(progress, 0, 1), 2);
        var border = lit || type != null && tile.Plate.GetRect().HasPoint(tile.Plate.GetParent<Control>().GetLocalMousePosition())
            ? "#ecb54e" : type == null ? "#35464a" : "#536367";
        var plate = skin.Plate(type == null ? "#1b292d" : enabled ? "#2c383c" : "#1c282d", border);
        if (tile.Plate.GetThemeStylebox("panel") != plate)
            tile.Plate.AddThemeStyleboxOverride("panel", plate);
    }

    private void OpenQueue()
    {
        if (!CanControl) return;
        queueOpen = true;
        placing = null;
        queuePage = 0;
        var source = selected.FirstOrDefault(p => p.Queue.Count > 0 || p.T.Key == "factory") ?? world.LastOrder;
        if (source?.T.Key == "factory") source = world.VehicleQueue(0);
        queueId = source?.Id ?? 0;
        UpdateInterface();
    }

    private void RefreshQueue()
    {
        var vehicleQueue = world.VehicleQueue(0);
        queueSources = world.Live(0).Where(p => p == vehicleQueue || p.T.Key is "hq" or "barracks")
            .OrderByDescending(p => p.Queue.Count > 0).ThenBy(p => p.Id).ToArray();
        var source = queueSources.FirstOrDefault(p => p.Id == queueId) ?? queueSources.FirstOrDefault();
        queueId = source?.Id ?? 0;
        queueTitle.Text = source == null ? "无生产线" : source == vehicleQueue ? "车辆队列" : source.T.Name + " #" + source.Id;
        prevProducer.Disabled = nextProducer.Disabled = queueSources.Length <= 1;
        var jobs = source?.Queue.ToArray() ?? Array.Empty<TypeInfo>();
        var construction = source?.T.Key == "hq"
            ? Enumerable.Range(0, 2).Select(c => world.Construction(0, c)).Where(c => c != null).ToArray() : Array.Empty<ConstructionJob>();
        int total = jobs.Length + construction.Length;
        int pages = Math.Max(1, (total + QueuePageSize - 1) / QueuePageSize);
        queuePage = Math.Clamp(queuePage, 0, pages - 1);
        float speed = MatchTempo.DefaultRate * multiplier;
        for (int row = 0; row < queueRows.Count; row++)
        {
            var entry = queueRows[row];
            int i = queuePage * QueuePageSize + row;
            entry.Source = source;
            entry.Index = i;
            entry.Snapshot = jobs;
            entry.Cancel.Visible = i < jobs.Length;
            TypeInfo type = i < jobs.Length ? jobs[i] : i < total ? construction[i - jobs.Length].Type : null;
            entry.Portrait.Texture = type == null ? null : view.Art.Portrait(type);
            entry.Name.Text = type?.Name ?? (total == 0 && row == 0 ? "暂无订单" : "—");
            entry.Status.Text = i >= total ? "" : i >= jobs.Length ? world.ConstructionState(construction[i - jobs.Length], 0, speed) :
                i == 0 ? world.QueueState(source, speed) : source.ProductionPaused ? "等待继续" : world.Funds[0] == 0 ? "等待资金" :
                "约 " + Math.Ceiling((jobs.Take(i + 1).Sum(t => t.Seconds) - source.Work) / (world.ProductionRate(source) * speed)) + " 秒";
        }
        queueCounter.Text = $"{queuePage + 1}/{pages}  {total}/{(source?.T.Key == "hq" ? 2 : Campaign.ProductionQueueLimit)}";
        prevQueue.Disabled = queuePage <= 0;
        nextQueue.Disabled = queuePage + 1 >= pages;
    }

    private void ChangeQueuePage(int amount) { queuePage += amount; RefreshQueue(); }
    private void CycleProducer(int amount)
    {
        if (queueSources.Length == 0) return;
        int index = Array.FindIndex(queueSources, p => p.Id == queueId);
        queueId = queueSources[(Math.Max(index, 0) + amount + queueSources.Length) % queueSources.Length].Id;
        queuePage = 0;
        RefreshQueue();
    }

    private void CancelQueueOrder(QueueRow entry)
    {
        if (CanControl && entry.Source != null && entry.Source.Queue.SequenceEqual(entry.Snapshot))
            world.CancelOrder(entry.Source, entry.Index);
        RefreshQueue();
    }

    private void LocateReady()
    {
        if (!CanControl || world.LastReady?.Deployed != true) return;
        selected.Clear();
        selected.Add(world.LastReady);
        view.Center(world.LastReady.P);
        view.QueueRedraw();
    }
}
