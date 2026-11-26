# Changelog

## [Unreleased]

### Changed

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
- Schema 更新器自动定位包目录，避免修改 Git/Registry 包缓存。

本版本尚未提交或推送到远程，也未创建 Git 版本标签。
