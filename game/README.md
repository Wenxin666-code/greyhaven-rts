# 断线前沿 · Godot 主工程

当前主工程开放「断线之夜」「河湾镇」「盆地岔口」。前两关胜利结算可直接进入下一关。第三关为 270×180 格的盆地六方混战。补给从河湾运到换装站后改由铁路送往石门，击败沿线五支敌军后即可发车。各平台发布包与启动说明见[项目首页](../README.md#下载与运行)。

## 运行开发版

1. 安装 Godot 4.7.2 .NET 和 .NET 10 SDK。
2. 用 Godot 导入本目录的 `project.godot`，然后按 F6 运行当前场景或 F5 运行项目。
3. 命令行可在本目录执行 `dotnet build Greyhaven.Godot.sln`，再运行 `godot --path .`。
4. 安装同版本的 .NET 导出模板后，可在 Godot 的「项目 → 导出」中选择「Windows x64」「Linux x64」或「macOS Universal」。命令行导出前先创建相应的 `exports/windows`、`exports/linux` 或 `exports/macos`。为 Linux 打包时，将 [`start-greyhaven.sh`](../scripts/start-greyhaven.sh) 放在 `Greyhaven.x86_64` 旁，保留两者的可执行权限。

`core/simulation/` 维护地图、寻路、采矿、建设、AI 和任务判定。`scripts/` 维护 Godot 场景逻辑、界面、渲染和素材坐标。`assets/` 存放 Godot 导入的小型资源，`../art/source/` 存放模型、正式精灵图和贴图包。导出后的运行文件自带资源。

原始图集更新后，在项目根目录运行 `python scripts/pack-runtime-sprites.py` 重建运行素材包。只更新车辆朝向、步兵站姿、围墙或部分爆炸特效时，可用 `--cells-only` 只重建按单帧加载的图集包。此工具需要支持 WebP 的 Pillow。平时构建和导出可直接使用仓库附带的素材包。

使用 `godot --headless --path . -- --verify` 验证前三章。第一章覆盖教学、经济和任务胜利。第二章覆盖开场交火、房屋占领、驻防和结算衔接。第三章验证交火、五方混战的胜利条件和战后发车演出。用 `godot --path . -- --verify-ui` 验证菜单、选关、地图、暂停、键位和战场输入。

用 `godot --path . -- --verify-memory` 重复进入第三关，检查战场纹理缓存和退出关卡时的资源释放。

### Windows 试玩包验证

更新试玩入口前，先在工作区的测试副本中核对导出包里的 `Greyhaven.exe`、`Greyhaven.pck` 和 `data_Greyhaven.Godot_windows_x86_64`，用完整的导出目录验证游戏能进入主菜单。运行时目录要随包保留。游戏根目录的 `打开断线前沿.cmd` 会在启动前检查关键文件。

自动验证从工作区的副本启动。复测 D 盘的正式试玩入口时，使用正常桌面权限启动。权限受限的测试进程可能无法访问 D 盘上的运行时目录，即使运行文件都在，也会触发 `.NET assemblies not found`。

## 试玩操作

- 左键点选、框选、移动与攻击。Shift 增减选择，Ctrl+Shift 左键攻击移动。
- 右键取消，按住右键拖图。滚轮缩放，方向键或地图边缘移动镜头。
- D 展开或部署，S 停止，X 就近散开，G 警戒，H 回基地，T 同类选择，Ctrl+数字建组。Alt+左键强制移动。
- 左下角快捷栏可收起，支持编组、同类选择、部署、原地警戒、规划路径和战场标记。按住 Z 设路径点，松开执行。标记或路径点选中后可按 Delete 删除。M、N 切换单位。
- Q、W、E、R 切换生产分类。左键下单或放置，右键暂停或减单，Shift 批量增减。点击「总览」可切换产线并取消指定订单。
- K 启用维修，L 启用出售。暂停菜单切换 1–4 倍速。F11 或 Alt+Enter 切换全屏。
- Esc 暂停、继续或返回上一层。Windows 上按 Alt+Tab 切到其他窗口。主菜单的「退出游戏」、窗口关闭按钮和 Alt+F4 均会打开退出确认。
- 选项中的「键盘与鼠标」支持重绑页面列出的单键指令。数字编组及鼠标组合键依照规则介绍。Tab 打开阵营信息。
- 暂停后选择「保存并返回选关」或确认退出游戏，可以保存当前战局。每关各有一份本地存档，再选中该关时可继续上次进度或开始新进度。
- 旧版「孤岛要塞」「雾脊中继站」及早期地图、模型和演出调试工具见旧版归档。

随项目附带的 Greyhaven UI 是从 Fontsource 的 Noto Sans SC 400 字重提取的简体中文子集，按 SIL OFL 1.1 分发。许可证在 `assets/fonts/OFL.txt`。
