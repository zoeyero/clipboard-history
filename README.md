﻿# clipboard历史剪切板

Windows 本地剪贴板历史工具，支持文字/图片、搜索、收藏、置顶、批量删除、备忘、主题与背景，以及可自定义快捷键的选择性记录模式。

## 使用

从本仓库 Releases 下载 `clipboard-0.4.1-win-x64.zip`，全部解压后运行 `clipboard历史剪切板.exe`。Windows 10/11 x64，需要 .NET Framework 4.x（建议 4.8）。

数据保存在本机；软件包不包含任何人的历史记录。普通复制也可以只进入系统剪贴板：在设置→记录与快捷键选择“仅快捷键记录”，默认 Ctrl+Shift+C，可改为 Ctrl+X 等。

## 构建与测试

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Phase2\package.ps1
```

以下内容供需要修改源码的开发者参考，普通使用者无需操作。项目使用 Windows 自带的 .NET Framework 编译器；快捷键测试需在正常登录的 Windows 桌面运行。测试使用独立样例数据，不修改个人剪贴板记录。

运行程序位于 `Phase2/dist`，分发 ZIP 位于 `releases`。这些自动生成的文件无需上传到源码仓库；供用户下载的软件 ZIP 应上传到 GitHub Releases。

## 文档

- [使用说明](Phase2/docs/使用说明.txt)
- [English user guide](Phase2/docs/User-Guide-English.txt)
- [版本与功能说明](Phase2/README.md)

