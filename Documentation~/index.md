# Game Dev Tools 使用说明

安装与最小接入见 [README](../README.md)。包名为 `com.sanmon.gamedevtools`，使用 Unity `2022.3`，无第三方运行时依赖。

## 控制器与生命周期

场景中添加 `GameConsole.GameDevToolsController`。组件在 Awake 初始化命令与日志系统，在首次打开时创建 UI；默认使用反引号键切换。场景切换时是否保留控制器由使用方管理，避免同时保留多个控制器实例。

可通过 `Open()`、`Close()` 控制窗口，通过 `Restart()` 重启工具，通过 `Shutdown()` 释放模块。`Print(string, LogType)` 向开发日志输出，`ClearOutput()` 清空日志。使用 `Instance` 前判断它是否存在。

当前输入使用旧 Input Manager；`Active Input Handling` 需为 `Input Manager (Old)` 或 `Both`。

## 命令与宏

- 在静态方法上标记 `RedSaw.CommandLineInterface.CommandAttribute`，以方法名或指定的 `name` 调用，例如 `Hello()`。
- 使用 `CommandPropertyAttribute` 暴露静态属性或字段，使用 `CommandValueParserAttribute` 注册静态参数解析器。
- 使用方自定义程序集需引用 `GameDevTools.Runtime`。扫描发生在命令系统初始化时，后加载的程序集需重启工具后重新发现。
- `GameConsole.Extension` 下提供命令别名、参数预设和宏扩展。编辑器与 Development Build 中包含现有标签压力测试命令。
- `WaitFor(命令调用)` 调用一次并等待 `Task`、`Task<T>` 或 `IEnumerator` 完成。同步方法返回即完成，普通调用不等待异步结果；需要等待异步操作时返回 Task，不使用 async void。
- `WaitFor()` 等待下一帧；`WaitFor(秒数)` 按非缩放时间等待，timeScale 为零时仍有效。停止宏只取消等待与后续调度，业务 Task 继续执行；工具销毁时清理其拥有的协程。
- 基于反射的成员在 IL2CPP 或启用代码裁剪时需由使用方通过 `[UnityEngine.Scripting.Preserve]` 或 `Assets/link.xml` 保留；需在目标平台实际验证。

## 设置、主题和语言

配置、宏和历史记录写入 `Application.persistentDataPath/development_data.json`。Inspector 显示实际文件路径，可打开文件、重新读取或恢复默认值。

`Runtime/GameUI/Resources/Schema.asset` 登记主题、字体与语言资源相对路径。保留 Resources 内的相对路径，不添加 `Runtime/` 或 `Packages/` 前缀。

Git/Registry 包使用提交的 Schema，不自动写包缓存。开发自定义资源时，改用本地包或嵌入式包；更新器仅追加新增条目，保留手动名称与顺序。自定义字体也可通过 `GameDevToolsController.RegisterFont(string, Font)` 注册。

现有主题布局规则见包内 `Runtime/GameUI/Resources/Style/STYLE_GUIDE.md`；图标规则见同级 Resources 下的 `Icon/README.md`。字体 OFL 许可随资源保留。

## 从 Assets 版本迁移

先保存场景与项目，再移除使用方旧的 GameDevTools 源码后安装 UPM 包，避免同名类型、重复资源和 GUID 冲突。包迁移保留了原始源码和资源 GUID；应在版本控制中检查场景、预制体和自定义程序集引用。原命名空间保持不变，但程序集由 Assembly-CSharp 改为 GameDevTools.Runtime；自行保存的程序集限定类型名需要同步更新。
