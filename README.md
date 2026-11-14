# 断线前沿 · Greyhaven RTS

《断线前沿》是一款等距视角的单人即时战略游戏。你是沿岸联合保障司令部的先遣指挥官。停火协议破裂后，你带队夺回旧码头，再从河湾西岸突破大桥，恢复通往内陆的补给线。

Godot 试玩版开放第一关「断线之夜」和第二关「河湾镇」。你可以建造基地、采矿、排产，调度步兵与装甲单位。第二关的北线与白石会在玩家接管前开始交战，桥西值班室可由工兵占领，再派步兵进驻。

## 下载与运行

在 [v0.23.0 发布页](https://github.com/Wenxin666-code/greyhaven-rts/releases/tag/v0.23.0) 的 Assets 区域选对应系统的试玩包。Assets 中的 `Source code` 是开发用源码压缩包。试玩包附带运行时，玩家无需安装 Godot 或 .NET SDK。

| 系统 | 下载文件 | 启动方式 |
| --- | --- | --- |
| Windows x64（Intel / AMD） | `Greyhaven-v0.23.0-windows-x64.zip` | 完整解压，双击 `Greyhaven.exe`。保留旁边的 `.pck` 和数据文件夹。 |
| Linux x64（Intel / AMD） | `Greyhaven-v0.23.0-linux-x64.tar.gz` | 解压后打开终端进入 `Greyhaven` 文件夹，运行 `./start-greyhaven.sh`。两关流程和窗口操作已在 Ubuntu（WSL）验证。 |
| macOS（Apple 芯片 / Intel） | `Greyhaven-v0.23.0-macos-universal.zip` | 在 Finder 解压后打开其中的 `.app`。此包已完成导出和文件检查，待 Mac 实机验证，使用临时签名且未做 Apple 公证。 |

macOS 若只提示无法验证开发者，请先核对下载来源和发布页的 SHA-256，再按 Apple 指南在「系统设置 → 隐私与安全性」为该应用单独选择「仍要打开」。若显示文件损坏或恶意软件提示，请停止并反馈完整提示。[Apple 指南](https://support.apple.com/en-us/102445)

### 分享给同学

仓库为私有。已获授权的同学登录 GitHub 后才可通过发布页下载。只分享游戏时，可把对应系统的试玩包直接发给同学，文件解压后可以离线运行。若确实需要让同学访问源码，可在 Settings → Collaborators 邀请对方。个人私有仓库的协作者拥有写入权限。[GitHub 权限说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository)

## 游戏版本

- **Godot 主线**提供上述两关。第一关结算可直接继续「河湾镇」。第三、四关待迁移。
- **Windows 旧版**可从 [v0.22.0 发布页](https://github.com/Wenxin666-code/greyhaven-rts/releases/tag/v0.22.0) 下载，可玩四关。WinForms 源码和旧测试保留在 [legacy/windows](legacy/windows/)。
- 完整改动见[版本记录](CHANGELOG.md)，剧情见[战役主线与关卡规划](docs/design/战役主线与关卡规划.md)。

## 源码与开发

1. 获取整个仓库。游戏在构建时会读取同仓库的 `art/source`。
2. 安装 Godot 4.7.2 **.NET** 版和 .NET 10 SDK。
3. 用 Godot 导入 [game/project.godot](game/project.godot)，按 F5 运行。导出、测试与目录说明见 [Godot 工程说明](game/README.md)。

| 目录 | 内容 |
| --- | --- |
| [game/](game/) | Godot 主工程、战役界面、渲染与模拟代码 |
| [art/](art/) | 正式美术资源、模型、动画与评审资料 |
| [maps/](maps/) | 地图策划与编辑数据 |
| [docs/](docs/) | 战役设计、调研和历史评审资料 |
| [scripts/](scripts/) | 美术导出、打包及旧工具兼容入口 |
