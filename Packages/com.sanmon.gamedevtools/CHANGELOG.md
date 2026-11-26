# Changelog

## [Unreleased]

### Repository

- 将仓库根目录恢复为标准 Unity 开发工程，包源码嵌入 Packages/com.sanmon.gamedevtools。
- 增加可直接运行的 Development 场景和打开场景菜单，保留原有包资产 GUID 与测试。
- Git 包安装地址新增 ?path=/Packages/com.sanmon.gamedevtools；旧开发工程缓存保留在本机忽略目录。

### Changed

- Schema 改名为 Style；界面样式统一命名为 UIStyle，不保留旧名称兼容。
- 默认资源集中为不导入的 DefaultResources~ 模板，首次打开项目时复制到 Assets/Resources/Sanmon/GameDevTools，模板源不进入 Player。
- 移除自动资源追加，已有 Style 仅对空 UI 样式和语言列表警告；首次使用 MapleMono，支持外部字体和完全无字体时使用 UI Toolkit 默认字体。
- 移除 UniTask 兼容及依赖，宏执行改用标准 Task 与 Unity 协程。
- 同步中英文宏语法说明、帮助文案、诊断字段和安装说明。
- 保留 Task、Task<T>、IEnumerator 等待、非缩放时间延迟与取消行为，新增宏执行回归测试。

## [0.1.0] - 2026-11-25

### Added

- UPM 包清单，包名为 `com.sanmon.gamedevtools`。
- Runtime、Editor 和 Editor 测试程序集定义。
- 安装、快速接入、开发、测试与第三方许可说明。
- 跨程序集命令、属性、参数解析器和资源加载回归测试。

### Changed

- 将源码与资源整理到 Runtime 和 Editor，保留原有资源 GUID。
- 将 Unity 开发工程迁至 `Samples~/DevelopmentProject`，本地引用仓库根包。
- 命令扫描支持使用方运行时程序集。
- Style 更新器自动定位包目录，避免修改 Git/Registry 包缓存。

本版本尚未提交或推送到远程，也未创建 Git 版本标签。
