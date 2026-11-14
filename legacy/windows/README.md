# Windows 旧版归档

原先的 Windows 单文件版在本目录独立保存，双击「断线前沿.exe」即可运行。该程序保存本轮重组前的版本，包含已实现的四个关卡。

- `src/` 和 `tests/` 保留重组时的原 Windows 源码和测试。
- Windows 上运行 `powershell -ExecutionPolicy Bypass -File .\tools\build.ps1` 可重建旧版。脚本使用仓库 `art/source/` 中的原有正式资源，评审输出沿用仓库 `docs/`。
- [旧版说明](README.original.md) 和[版本记录](../../CHANGELOG.md)留作查阅。

日常开发入口见[工程首页](../../README.md)。
