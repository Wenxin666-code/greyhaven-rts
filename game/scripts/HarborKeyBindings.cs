using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HarborGame
{
    private const string BindingsPath = "user://controls.cfg";
    private static readonly (Key Key, string Name)[] ConfigurableKeys =
    {
        (Key.D, "部署／收起／卸载"), (Key.S, "停止"), (Key.G, "原地警戒"), (Key.H, "定位基地"),
        (Key.T, "同类选择"), (Key.P, "全选战斗单位"), (Key.Z, "路径点规划"), (Key.Delete, "删除标记／路径点"),
        (Key.Q, "建筑分类"), (Key.W, "防御分类"), (Key.E, "步兵分类"), (Key.R, "车辆分类"),
        (Key.K, "维修模式"), (Key.L, "出售模式"), (Key.M, "上一个单位"), (Key.N, "下一个单位"),
        (Key.X, "散开"), (Key.Space, "定位最近事件"), (Key.B, "放置战场标记"), (Key.Tab, "阵营信息")
    };
    private readonly Dictionary<Key, Key> bindings = new(), draftBindings = new();
    private Control keyboardPanel;
    private Label keyboardPageLabel, keyHelp;
    private readonly Button[] keyboardRows = new Button[10];
    private readonly Label[] keyboardNames = new Label[10];
    private bool keyboardOpen;
    private int keyboardPage;
    private Key capturingKey, physicalRouteKey;

    private void MakeKeyboard()
    {
        using var stored = new ConfigFile();
        stored.Load(BindingsPath);
        var claimed = new HashSet<Key>();
        foreach (var spec in ConfigurableKeys)
        {
            var key = (Key)(long)stored.GetValue("bindings", spec.Key.ToString(), (long)spec.Key);
            bindings[spec.Key] = IsAvailableKey(key) && claimed.Add(key) ? key : Key.None;
        }
        keyboardPanel = Strip(pauseCurtain, "#202c32", "#ecb54e");
        Position(keyboardPanel, 235, 65, 1000, 690);
        Bold(keyboardPanel, "键盘与鼠标", 31, 15, 815, 52, 29);
        LabelAt(keyboardPanel, "点击按键行后按新键，Backspace 清除，Esc 取消输入。", 32, 59, 920, 28, 15).Modulate = Dim;
        for (int i = 0; i < keyboardRows.Length; i++)
        {
            int row = i;
            int y = 101 + i * 45;
            keyboardNames[i] = LabelAt(keyboardPanel, "", 33, y + 3, 450, 30, 15);
            keyboardRows[i] = AddButton(keyboardPanel, "", 525, y, 430, 35, () =>
            {
                capturingKey = ConfigurableKeys[keyboardPage * keyboardRows.Length + row].Key;
                RefreshKeyBindings();
            });
        }
        keyHelp = LabelAt(keyboardPanel, "", 33, 560, 900, 25, 14);
        keyHelp.Modulate = Dim;
        AddButton(keyboardPanel, "‹", 33, 589, 65, 34, () => { keyboardPage = 0; capturingKey = Key.None; RefreshKeyBindings(); });
        keyboardPageLabel = Bold(keyboardPanel, "", 115, 591, 70, 27, 15);
        keyboardPageLabel.Modulate = Gold;
        AddButton(keyboardPanel, "›", 186, 589, 65, 34, () => { keyboardPage = 1; capturingKey = Key.None; RefreshKeyBindings(); });
        AddButton(keyboardPanel, "恢复默认", 295, 589, 180, 34, () =>
        {
            foreach (var spec in ConfigurableKeys) draftBindings[spec.Key] = spec.Key;
            capturingKey = Key.None;
            RefreshKeyBindings();
        });
        SetLit(AddButton(keyboardPanel, "应用并返回", 503, 589, 180, 34, SaveKeyBindings), true);
        AddButton(keyboardPanel, "返回选项", 709, 589, 180, 34, CloseKeyboard);
    }

    private void OpenKeyboard()
    {
        foreach (var pair in bindings) draftBindings[pair.Key] = pair.Value;
        keyboardPage = 0;
        keyboardOpen = true;
        keyHelp.Text = "数字编组和鼠标组合键沿用战场控制。";
        RefreshKeyBindings();
        RefreshPausePanels();
    }

    private void CloseKeyboard()
    {
        keyboardOpen = false;
        capturingKey = Key.None;
        RefreshPausePanels();
    }

    private void RefreshKeyBindings()
    {
        keyboardPageLabel.Text = $"{keyboardPage + 1} / 2";
        for (int i = 0; i < keyboardRows.Length; i++)
        {
            var spec = ConfigurableKeys[keyboardPage * keyboardRows.Length + i];
            var key = draftBindings[spec.Key];
            keyboardNames[i].Text = spec.Name;
            keyboardRows[i].Text = capturingKey == spec.Key ? "请按键…" : key == Key.None ? "未分配" : OS.GetKeycodeString(key);
            SetLit(keyboardRows[i], capturingKey == spec.Key);
        }
    }

    private static bool IsAvailableKey(Key key) => Enum.IsDefined(key) && key is not
        (Key.None or Key.Escape or Key.Enter or Key.F11 or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta or
         Key.Left or Key.Right or Key.Up or Key.Down) && ((int)key < (int)Key.Key0 || (int)key > (int)Key.Key9);

    private void CaptureBinding(InputEventKey evt)
    {
        if (evt.Keycode == Key.Escape)
        {
            if (capturingKey == Key.None) CloseKeyboard();
            else { capturingKey = Key.None; RefreshKeyBindings(); }
            return;
        }
        if (capturingKey == Key.None) return;
        var key = evt.PhysicalKeycode;
        if (key != Key.Backspace && (!IsAvailableKey(key) || evt.ShiftPressed || evt.CtrlPressed || evt.AltPressed || evt.MetaPressed)) return;
        keyHelp.Text = "数字编组和鼠标组合键沿用战场控制。";
        if (key != Key.Backspace)
        {
            var conflict = ConfigurableKeys.FirstOrDefault(spec => spec.Key != capturingKey && draftBindings[spec.Key] == key);
            if (conflict.Key != Key.None)
            {
                draftBindings[conflict.Key] = Key.None;
                keyHelp.Text = $"已解除「{conflict.Name}」的旧绑定。";
            }
        }
        draftBindings[capturingKey] = key == Key.Backspace ? Key.None : key;
        capturingKey = Key.None;
        RefreshKeyBindings();
    }

    private void SaveKeyBindings()
    {
        using var stored = new ConfigFile();
        foreach (var pair in draftBindings) stored.SetValue("bindings", pair.Key.ToString(), (long)pair.Value);
        if (stored.Save(BindingsPath) != Error.Ok)
        {
            keyHelp.Text = "无法保存按键设置。";
            return;
        }
        foreach (var pair in draftBindings) bindings[pair.Key] = pair.Value;
        CloseKeyboard();
    }

    private Key BoundAction(Key physical)
    {
        foreach (var binding in bindings)
            if (binding.Value == physical) return binding.Key;
        return Key.None;
    }
}
