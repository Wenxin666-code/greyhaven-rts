# 断线前沿 · Godot 主工程

战役开放第一关「断线之夜」和第二关「河湾镇」。两关共用选关、地图预览、暂停、任务简报和结算界面，第一关结算可直接进入第二关。各平台试玩包与启动说明见[项目首页](../README.md#下载与运行)。

## 运行开发版

1. 安装 Godot 4.7.2 .NET 和 .NET 10 SDK。
2. 用 Godot 导入本目录的 `project.godot`，然后按 F6 运行当前场景或 F5 运行项目。
3. 命令行可在本目录执行 `dotnet build Greyhaven.Godot.sln`，再运行 `godot --path .`。
4. 安装同版本的 .NET 导出模板后，可在 Godot 的「项目 → 导出」中选择「Windows x64」「Linux x64」或「macOS Universal」。命令行导出前先创建相应的 `exports/windows`、`exports/linux` 或 `exports/macos`。为 Linux 打包时，将 [`start-greyhaven.sh`](../scripts/start-greyhaven.sh) 放在 `Greyhaven.x86_64` 旁，保留两者的可执行权限。

`core/simulation/` 维护地图、寻路、采矿、建设、AI 和任务判定。`scripts/` 维护 Godot 场景逻辑、界面、渲染和素材坐标。`assets/` 存放 Godot 导入的小型资源，`../art/source/` 存放模型、正式精灵图和贴图包。导出后的运行文件自带资源。

使用 `godot --headless --path . -- --verify` 验证前两章。第一章覆盖教学、经济和任务胜利。第二章检查真实开场交火、玩家接管、房屋占领和驻防，并在受控胜利条件下验证撤离和结算。用 `godot --path . -- --verify-ui` 验证菜单、选关、地图、暂停、键位和战场输入。

## 试玩操作

- 左键点选、框选、移动与攻击。Shift 增减选择，Ctrl+Shift 左键攻击移动。
- 右键取消，按住右键拖图。滚轮缩放，方向键或地图边缘移动镜头。
- D 展开或部署，S 停止，G 警戒，H 回基地，T 同类选择，Ctrl+数字建组。
- 左下角快捷栏可收起，支持编组、同类选择、部署、原地警戒、规划路径和战场标记。按住 Z 设路径点，松开执行。标记或路径点选中后可按 Delete 删除。M、N 切换单位。
- Q、W、E、R 切换生产分类。左键下单或放置，右键暂停或减单，Shift 批量增减。点击「总览」可切换产线并取消指定订单。
- K 启用维修，L 启用出售。暂停菜单切换 1–4 倍速。F11 或 Alt+Enter 切换全屏。
- 选项中的「键盘与鼠标」支持重绑页面列出的单键指令。数字编组及鼠标组合键依照规则介绍。Tab 打开阵营信息。
- 第三、四关及原版的地图、模型、演出调试工具仍在旧版归档。

随项目附带的 Greyhaven UI 是从 Fontsource 的 Noto Sans SC 400 字重提取的简体中文子集，按 SIL OFL 1.1 分发。许可证在 `assets/fonts/OFL.txt`。
