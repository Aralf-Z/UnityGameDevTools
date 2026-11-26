# 开发与验证

## 打开开发工程

使用 Unity `2022.3.62f3` 打开仓库根目录。根目录包含标准 `Assets/`、`Packages/`、`ProjectSettings/`；GameDevTools 位于 `Packages/com.sanmon.gamedevtools/`，由 Unity 自动识别为 Embedded 包，manifest 不再声明指向自身的 file 依赖。

打开 `Assets/Scenes/Development.unity` 后进入 Play Mode，按反引号键打开控制台。菜单 `GameDevTools > Open Development Scene` 可打开场景，场景缺失时会创建包含摄像机、灯光和 GameDevToolsController 的最小入口，并登记到构建设置。

`Assets/` 用于开发场景及宿主脚本，包内 `Runtime/`、`Editor/` 用于可分发功能，`Tests/Editor/` 和 `Tests/Runtime/` 用于回归测试。不要将包源码复制到 Assets。开发依赖需要访问 Unity 包注册表；运行时仅依赖 Unity 内置模块。

旧 `Samples~/DevelopmentProject` 的本机缓存已归档到忽略目录 `LegacyProjectCache~/DevelopmentProject`，不作为开发入口。根目录的 Library、Logs、UserSettings 和 IDE 生成文件均不提交。

## 程序集与资源

- `GameDevTools.Runtime` 保留 GameConsole 与 RedSaw.CommandLineInterface 命名空间。
- `GameDevTools.Editor` 仅在 Editor 编译，并引用运行时程序集。
- `GameDevTools.Editor.Tests` 和 `GameDevTools.Runtime.Tests` 由 manifest 的 `testables` 启用。
- 移动资产时保留 `.meta` 和 GUID；新增可导入资产及目录提交配套 `.meta`。包内 `Documentation~` 无需 `.meta`。

Schema 更新器通过运行时程序集的 PackageInfo 定位资源；Local 和 Embedded 来源允许刷新，Git/Registry 来源不改写资产。新增主题、字体或语言后，等待导入并将更新后的 Schema 纳入变更。

## 验证

在仓库根目录运行：

```powershell
unity test . --mode EditMode --output TestResults/editmode.xml
unity test . --mode PlayMode --output TestResults/playmode.xml
```

EditMode 的 `GameDevTools.Tests.PackageCompatibilityTests` 验证包身份、跨程序集发现、Schema 和模板；PlayMode 的 `GameDevTools.Tests.MacroExecutionTests` 覆盖 Task、协程、取消、异常、下一帧及非缩放时间等待。

手动检查控制台开关、Console/Cheat/Macro/Options、主题、语言、字体与配置。构建目标平台以确认 Player 程序集依赖；IL2CPP 需要另行验证反射保留。自动测试不能代替实际交互验证。

`Sanmon-Shosetsu/client` 是独立远程 Git 集成宿主。其依赖地址应在本次迁移推送后改为下方地址，并由 Unity Package Manager 更新锁文件；未发布的本地修改不能视为远程验证结果。

## 分发

本地安装使用 `Packages/com.sanmon.gamedevtools/package.json`。迁移推送后的 Git 地址：

```text
https://github.com/Aralf-Z/UnityGameDevTools.git?path=/Packages/com.sanmon.gamedevtools#master
```

发布版本时同步更新包内 package.json 与 CHANGELOG。Git 包需要在 Package Manager 中更新才能获得新提交。本次目录改造不自动提交、推送或打标签。
