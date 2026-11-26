# Game Dev Tools

基于 UI Toolkit 的 Unity 游戏开发控制台。仓库本身是标准 Unity 工程，可直接开发、运行及测试；可分发的 UPM 包嵌入在 `Packages/com.sanmon.gamedevtools`。

## 开发

1. 用 Unity Hub 添加并打开本仓库根目录，使用 **Unity 2022.3.62f3**。
2. 打开 `Assets/Scenes/Development.unity`（也可使用菜单 `GameDevTools > Open Development Scene`）。
3. 进入 Play Mode，按反引号键打开控制台，调试 Console、Cheat、Macro、Options。
4. 在 `Packages/com.sanmon.gamedevtools` 中修改源码及资源，Unity 会直接编译本地嵌入式包。

`Assets` 放开发场景和宿主脚本；通用功能与回归测试放在包内，避免复制源码导致重复编译。原有资源 GUID、程序集名称和命名空间保持不变。

## 测试

`Window > General > Test Runner` 中运行 EditMode 和 PlayMode 测试，manifest 已通过 `testables` 启用包测试。命令行：

```powershell
unity test . --mode EditMode --output TestResults/editmode.xml
unity test . --mode PlayMode --output TestResults/playmode.xml
```

缓存、IDE 文件和测试报告均由 `.gitignore` 排除。旧开发工程的本机缓存保留在忽略目录 `LegacyProjectCache~/DevelopmentProject`。

## 在其他工程安装

本地安装选择 `Packages/com.sanmon.gamedevtools/package.json`。本次目录改造推送到远程后，Git 安装地址需包含包路径：

```text
https://github.com/Aralf-Z/UnityGameDevTools.git?path=/Packages/com.sanmon.gamedevtools#master
```

此前不含 `?path=` 的地址仅适用于旧目录结构。使用方应在迁移发布后更新地址，由 Unity 重新解析锁文件。

使用方法见 [包 README](Packages/com.sanmon.gamedevtools/README.md)，详细说明见 [开发与验证](Packages/com.sanmon.gamedevtools/Documentation~/development.md) 和 [编码规范](Packages/com.sanmon.gamedevtools/Documentation~/coding-standard.md)。许可：[MIT](LICENSE)。
