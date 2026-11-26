# Game Dev Tools 开发工程

使用 Unity `2022.3.62f3` 打开本目录。此工程从本地引用仓库根目录的 `com.sanmon.gamedevtools`，不要把包源码复制到 Assets。

首次打开需解析 Unity 开发依赖，无需安装第三方异步库。新建场景和 GameObject，添加 `GameConsole.GameDevToolsController`，进入 Play Mode 后按反引号键打开工具。工程没有预设演示场景。

在 `Window > General > Test Runner` 的 EditMode 中运行 `GameDevTools.Tests.PackageCompatibilityTests`。更多内容见仓库根目录的 README 和 Documentation~/development.md。

在 PlayMode 中运行 `GameDevTools.Tests.MacroExecutionTests`，验证 Task、协程、等待与取消行为。

本目录是完整开发工程，不通过 Package Manager 的 Samples 按钮导入。
