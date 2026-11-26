# 开发与验证

## 打开开发工程

使用 Unity `2022.3.62f3` 打开仓库下的 `Samples~/DevelopmentProject`。工程保留原有 ProjectSettings 和开发依赖，通过 `file:../../..` 引用仓库根目录的 `com.sanmon.gamedevtools`。该相对地址以工程的 Packages 目录为基准。

解析开发依赖需要访问 Unity 包注册表。运行时仅依赖 Unity 内置模块，宏调度使用标准 Task 和 Unity 协程。不要复制 Runtime 到工程 Assets，以免重复编译。工程当前没有预设场景；新建场景和 GameObject 并添加 GameDevToolsController 即可调试。

根目录是 UPM 包，不再作为 Unity 工程打开。原有 Library、Logs、UserSettings 和 IDE 文件保存在本机忽略目录 `LegacyProjectCache~`，不迁入开发工程，不提交。末尾的 ~ 防止 Unity 将这些旧文件当作包资产导入。

## 程序集与资源

- `GameDevTools.Runtime` 不引用第三方异步程序集，保留 GameConsole 与 RedSaw.CommandLineInterface 命名空间。
- `GameDevTools.Editor` 仅在 Editor 编译，并引用运行时程序集。
- `GameDevTools.Editor.Tests` 为 Editor 测试程序集，通过开发工程 manifest 的 `testables` 启用。
- `GameDevTools.Runtime.Tests` 包含 PlayMode 宏执行测试，覆盖 Task、协程、取消、异常、下一帧和非缩放时间等待。
- 所有可导入资产及目录提交配套 `.meta`，移动已有资产时保留 GUID。`Documentation~` 与 `Samples~` 是 Unity 忽略的目录，本身无需 `.meta`。

Schema 更新器以运行时程序集对应的 PackageInfo 定位资源。仅 Local 和 Embedded 来源允许刷新；Git、Registry 等来源不改写资产。新增主题、字体或语言后，在本地开发工程中等待导入并将更新后的 Schema 一起纳入变更。

## 验证

在 Unity Test Runner 的 EditMode 中运行 `GameDevTools.Tests.PackageCompatibilityTests`，覆盖包身份、跨程序集命令/属性/参数解析器发现、Schema 资源和共享模板加载。可使用 Unity CLI：

```powershell
unity test ./Samples~/DevelopmentProject --mode EditMode --filter GameDevTools.Tests.PackageCompatibilityTests --output <仓库外报告目录>/editmode.xml
unity test ./Samples~/DevelopmentProject --mode PlayMode --filter GameDevTools.Tests.MacroExecutionTests --output <仓库外报告目录>/playmode.xml
```

手动进入 Play Mode，检查反引号开关、Console/Cheat/Macro/Options、主题与语言切换、字体和配置重新加载。构建目标平台，确认 Player 程序集没有 UnityEditor 依赖；IL2CPP 还需验证反射成员保留。

静态检查或单元测试不能代替实际交互验证。具体执行结果以此次交付的验证报告为准。

## 发布

当前迁移仅完成本地整理，不自动提交、推送或打标签。校验通过并由维护者推送迁移到 master 后，安装链接为：

```text
https://github.com/Aralf-Z/UnityGameDevTools.git#master
```

发布新版本时同步修改 package.json 与 CHANGELOG，并在实际创建标签后提供固定版本链接。Git 包不会随着远程分支变化自动更新，需在 Package Manager 中更新依赖。
