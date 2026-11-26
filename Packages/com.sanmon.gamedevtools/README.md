# Game Dev Tools

基于 UI Toolkit 的 Unity 游戏开发控制台，包含命令补全、调试参数面板、宏、日志、主题、字体切换与中英文本地化。

| 项目 | 内容 |
| --- | --- |
| UPM 包名 | `com.sanmon.gamedevtools` |
| 版本 | `0.1.0` |
| Unity 基线 | `2022.3`，验证目标为 `2022.3.62f3` |
| 运行时依赖 | Unity 内置模块，无第三方运行时依赖 |
| 许可 | [MIT](LICENSE)，第三方组件见 [Third Party Notices.md](Third%20Party%20Notices.md) |

## 安装

**发布状态：此次标准 Unity 工程目录改造尚未提交或推送。下列地址需要在目录迁移推送到远程 `master` 后使用。**

在 Unity 的 `Window > Package Manager` 中选择 `+ > Add package from git URL`，输入 Game Dev Tools 地址（迁移推送后可用）：

```text
https://github.com/Aralf-Z/UnityGameDevTools.git?path=/Packages/com.sanmon.gamedevtools#master
```

无需预装第三方异步库。Git 地址对应的提交由项目的 `Packages/packages-lock.json` 锁定；远程分支有新提交后，需要在 Package Manager 中更新该 Git 包。固定版本时，将 `#master` 替换为已存在的标签或完整提交 SHA；本次未创建版本标签。

### 使用本地仓库

选择 `+ > Add package from disk` 并选中本仓库的 `Packages/com.sanmon.gamedevtools/package.json`。开发者直接用 Unity 打开仓库根目录；本包作为 Embedded 包参与编译。

## 快速开始

1. 使用 Unity `2022.3` 打开目标工程。`Active Input Handling` 设为 `Input Manager (Old)` 或 `Both`，当前快捷键使用 `UnityEngine.Input`。
2. 在场景中新建 GameObject，添加 `GameConsole.GameDevToolsController` 组件。
3. 进入 Play Mode，按反引号键 `` ` `` 打开控制台；默认不需要修饰键。Inspector 或 Options 中可调整快捷键。
4. 通过 Console、Cheat、Macro、Options 页使用命令、预设参数、宏和外观设置；界面首次打开时创建。

在使用方的运行时脚本中添加命令：

```csharp
using RedSaw.CommandLineInterface;
using UnityEngine;

public static class GameCommands
{
    [Command(name: "Hello", desc: "输出问候", tag: "示例")]
    public static void Hello()
    {
        Debug.Log("Hello from Game Dev Tools");
    }
}
```

在控制台输入 `Hello()`。使用自定义 `.asmdef` 的项目需显式引用 `GameDevTools.Runtime`；普通 `Assembly-CSharp` 脚本可直接使用。命令系统在初始化时发现包自身及已加载、直接引用运行时程序集的命令、属性与参数解析器。

宏的 `WaitFor(命令调用)` 支持等待 `Task`、`Task<T>` 和 `IEnumerator`；普通命令调用不等待异步结果。`WaitFor()` 等待下一帧，`WaitFor(秒数)` 使用非缩放时间。停止宏取消当前等待与后续调度，不取消业务 Task。

## 配置与资源

设置、宏和输入历史统一保存在 `Application.persistentDataPath/development_data.json`，不写入 UPM 包。Inspector 中显示实际路径并提供打开按钮。

主题、字体、图标和语言资源位于 `Runtime/GameUI/Resources`。Git 安装使用随包提供的 `Schema.asset`；修改资源时使用本地包或嵌入式包，避免直接编辑 `Library/PackageCache`。运行时自定义字体可通过控制器的 `RegisterFont` 注册。

## 仓库目录

```text
package.json                  UPM 包清单
Runtime/                      运行时代码与 Resources
Editor/                       Inspector 与 Schema 更新器
Tests/Editor/                 包兼容与跨程序集回归测试
Documentation~/               使用说明、开发指南、编码规范

CHANGELOG.md                  版本记录
LICENSE                       项目 MIT 许可
Third Party Notices.md        第三方组件及字体许可说明
```

上述目录相对于 `Packages/com.sanmon.gamedevtools/`。仓库根目录的 `Assets/`、`Packages/`、`ProjectSettings/` 组成开发工程，缓存和 IDE 生成文件不纳入版本管理。

更多内容见 [使用说明](Documentation~/index.md)、[开发与验证](Documentation~/development.md) 和 [编码规范](Documentation~/coding-standard.md)。
