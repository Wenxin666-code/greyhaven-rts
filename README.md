# 断线前沿 · Greyhaven RTS

《断线前沿》是一款等距视角的单人即时战略游戏。你是沿岸联合保障司令部的先遣指挥官。停火协议破裂后，你带队夺回旧码头，再从河湾西岸突破大桥，恢复通往内陆的补给线。

战役前三关现已开放。你将从灰港的旧码头出发，经过争夺中的河湾大桥，沿盆地铁路夺回封锁的道口，把补给送往石门。主菜单的「战役档案」收录了各方势力的来历、负责人和立场。

## 下载与运行

从 [v1.0.0 发布页](https://github.com/Wenxin666-code/greyhaven-rts/releases/tag/v1.0.0) 的 Assets 区域下载对应系统的游戏包。游戏包附带运行时，解压后可离线游玩，无需安装 Godot 或 .NET SDK。

| 系统 | 下载文件 | 启动方式 |
| --- | --- | --- |
| Windows x64（Intel / AMD） | `Greyhaven-v1.0.0-windows-x64.zip` | 完整解压，双击 `Greyhaven.exe`。启动程序、`.pck` 和数据文件夹请放在一起。 |
| Linux x64（Intel / AMD） | `Greyhaven-v1.0.0-linux-x64.tar.gz` | 解压后在 `Greyhaven` 文件夹打开终端，运行 `./start-greyhaven.sh`。 |
| macOS（Apple 芯片 / Intel） | `Greyhaven-v1.0.0-macos-universal.zip` | 在 Finder 解压后打开 `断线前沿.app`。包使用临时签名，尚未做 Apple 公证和 Mac 实机验证。 |

macOS 若只提示无法验证开发者，请先核对下载来源和发布页的 SHA-256，再按 Apple 指南在「系统设置 → 隐私与安全性」为该应用单独选择「仍要打开」。若显示文件损坏或恶意软件提示，请停止并反馈完整提示。[Apple 指南](https://support.apple.com/en-us/102445)

### 分享给同学

仓库为私有。已获授权的同学登录 GitHub 后才可通过发布页下载。只分享游戏时，可把对应系统的试玩包直接发给同学，文件解压后可以离线运行。若确实需要让同学访问源码，可在 Settings → Collaborators 邀请对方。个人私有仓库的协作者拥有写入权限。[GitHub 权限说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository)

## 游戏版本

- **Godot 主线**包含「断线之夜」「河湾镇」「盆地岔口」。前两关通关后可从结算直接进入下一关。
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
