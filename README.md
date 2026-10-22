﻿# clipboard历史剪切板

Windows 本地剪贴板历史工具，支持文字/图片、搜索、收藏、置顶、批量删除、备忘、主题与背景，以及可自定义快捷键的选择性记录模式。

## 使用

从本仓库 Releases 下载 `clipboard-0.4.0-win-x64.zip`，全部解压后运行 `clipboard历史剪切板.exe`。Windows 10/11 x64，需要 .NET Framework 4.x（建议 4.8）。

数据保存在本机；软件包不包含任何人的历史记录。普通复制也可以只进入系统剪贴板：在设置→记录与快捷键选择“仅快捷键记录”，默认 Ctrl+Shift+C，可改为 Ctrl+X 等。

## 构建与测试

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\package.ps1
```

使用 Windows .NET Framework 编译器，无需安装第三方依赖。快捷键注册回归需要交互式 Windows 桌面；受限沙盒可能拒绝注册。测试使用隔离样例和模拟剪贴板，不发送模拟按键到其他应用。

运行程序位于 `Phase2/dist`，分发 ZIP 位于 `releases`。不要提交这些构建产物或测试输出。

## 文档

- [使用说明](Phase2/docs/使用说明.txt)
- [English user guide](Phase2/docs/User-Guide-English.txt)
- [版本与功能说明](Phase2/README.md)

本源码快照尚未指定开源许可证，仓库所有者可在发布前自行选择。
## 0.4.0 · 中英文界面 / Languages

设置 → 常规 → 语言 / Language，可选择简体中文或 English。点击应用或保存后立即切换，重启后保留选择。已有记录、备注及背景不变。

Settings → General → Language: select English or Simplified Chinese, then Apply or Save settings. The choice persists across restarts. Your clipboard content, notes and background are unchanged. Windows file dialogs and system error details follow the Windows display language.
