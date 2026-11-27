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

## SurvivorDemo 配置

菜单 `Demo > 幸存者配置器` 的标签依次为玩家等级、玩家技能、波次、怪物、怪物技能, 新窗口默认打开玩家等级。支持表单、原始 JSON、Undo/Redo、校验与组合预览; 切换分类保留未保存的修改, “保存全部修改”会先校验所有修改过的配置再写入。

配置位于 `Assets/StreamingAssets/SurvivorDemo/`:

- `Skills.json`: 版本 3 包含能量弹、环绕匕首、冲击波、冰霜领域、连锁闪电、烈焰火环、追踪导弹、腐蚀毒池共 8 个战斗技能, 以及独立的默认冲刺。每项技能和冲刺各有 5 个独立改造, 支持开关、名称、描述、数值设定/增加/倍率、颜色及 Debuff。技能的 `damageMultiplier` 表示对玩家基础伤害的倍率, 改造中的对应参数为 `DamageMultiplier`。每项改造只能获得一次, 多项效果按配置顺序叠加, 热更新按改造 ID 匹配已获得效果。
- `Level.json`: 版本 3 配置基础最大生命 `baseMaxHealth`、基础每秒生命恢复 `healthRegenerationPerSecond` 和基础伤害 `baseDamage`, 并逐级保存累计总经验、技能与改造奖励、生命增长 `maxHealthGain`、恢复增长 `healthRegenerationGain` 和基础伤害增长 `baseDamageGain`。1 级累计经验为 0, 后续必须严格递增, 最后一行决定等级上限。默认基础生命 100、恢复 1/秒、基础伤害 10, 各级增量沿用现有配置。
- `Waves.json`: 对局时长、敌人上限、生成距离、开局生成组, 以及按时间排列的波次。每波可设置生成间隔、每批数量、随机敌人池与入场生成组。第一波从 0 秒开始, 最后一波持续到对局结束。
- `Enemies.json`: 版本 3 支持自定义怪物 ID、坦克/战士/敏捷/远程系别、强度 1–3、生命成长、速度、接触攻击、经验掉落、颜色与形状, 每只绑定 1–2 个怪物技能 ID。`scaleMin` / `scaleMax` 定义三维 Scale 区间, 每次生成（含对象池复用）随机取样, XYZ 使用相同插值比例, 默认尺寸浮动约 ±8%。热更新保留个体随机比例, 血条高度和碰撞半径随体型调整。`preferredDistance` 控制远程怪物保持的距离, `skillDamageMultiplier` 调整该怪物的技能威力。生命按 `基础生命 × (1 + (生成波次 - 1) × 每波增长比例)` 计算。
- `EnemySkills.json`: 独立怪物技能配置, 包含开关、名称、描述、技能行为、CD、初始 CD、随机延迟、施放距离、前摇、伤害、范围和行为专用参数。内置扇形近战、范围攻击、冲锋、投射物、散射、定点轰击、减伤护盾、移动加速和自身治疗。每个怪物分别维护各技能的冷却, 符合距离与状态条件时自动施放; 冻结/击退会中断前摇和冲锋, 暂停/选择奖励/对局结束时停止计时。怪物技能伤害 = 技能基础伤害 × 该怪物技能伤害倍率, 与玩家技能配置独立。

默认怪物阵容每系三只, 普通怪物带 1 个技能, 精英和强敌各带 2 个技能, 波次逐步加入较强怪物:

| 系别 | 普通（强度 1） | 精英（强度 2） | 强敌（强度 3） |
|---|---|---|---|
| 坦克 | 石甲守卫 `Tank` | 铁盾重卫 `IronGuard` | 山岭巨像 `Colossus` |
| 战士 | 赤刃战士 `Grunt` | 狂战士 `Berserker` | 战旗统领 `Warlord` |
| 敏捷 | 疾影斥候 `Runner` | 影袭刺客 `Assassin` | 幻影舞者 `ShadowDancer` |
| 远程 | 荒原射手 `Archer` | 焰火术士 `FireMage` | 重弩炮手 `Artillery` |

Level 页的“等级成长公式”统一使用 `lv`, 支持四则运算、`%`、`^`、括号以及 `pow/min/max/round/floor/ceil/sqrt/abs/log/exp`。经验、最大生命、每秒恢复和基础伤害公式都计算该等级的总值。例如 `100 + 8 * (lv - 1)` 在 5 级表示最大生命 132, 自动换算成各级增加 8; `1 + 0.1 * (lv - 1)` 表示 5 级每秒恢复 1.4; `10 + 0.8 * (lv - 1)` 表示 5 级基础伤害 13.2。应用属性公式时计算相邻等级差值保存到增量字段, 包含 1 级时同时更新基础值并将 1 级增量置零。范围外的总值保持不变, 相邻增量自动重算; 总属性可保持不变但不能递减。可勾选字段和等级范围, 应用后等级表立即显示总值并保留滚动位置。公式仅在编辑器计算, 不保存到 JSON, 不显示额外预览表格。经验 `37.5 * (lv - 1)^2` 在 5 级表示累计 600 经验, 仍直接保存累计门槛, 升级不会扣除经验。

所有技能的直接命中与区域伤害按 `玩家当前基础伤害 × 技能伤害倍率` 计算, 包括已发射子弹、追踪导弹和持续领域; 等级成长或控制台 `@SurvivorDamage` 修改基础伤害后即生效。流血等百分比 Debuff 保持按目标生命比例计算。现有技能伤害已按基础伤害 10 转换为对应倍率, 默认伤害保持原数值。

Level 页直接使用可编辑等级表格, 支持修改最高等级、输入等级定位、逐行修改累计经验、最大生命值、每秒生命恢复和基础伤害的总值。单级总值修改会自动调整相邻增量, 保持其他等级的总值不变; 保存的 JSON 仍使用基础值与逐级增量。“解锁技能”和“获得改造”各使用一个等级列表字段, 如 `1, 2, 5, 10`, 离开输入框后自动解析为各等级的 `skillRewards` / `modificationRewards`, 保存时写入 JSON。支持中英文逗号、空格和换行; 重复等级表示同级多次奖励, 留空清除该类奖励。表格中的技能和改造列显示解析后的次数。无效等级不会覆盖已解析数据, 修正后才可保存。

玩家的基础“每秒生命恢复”在 Level 页配置, 对应 `Level.json` 的 `healthRegenerationPerSecond`, 默认每秒恢复 1 点。等级表中的“恢复增长/秒”对应各级的 `healthRegenerationGain`, 可单独调整每一级增加的恢复量, 默认均为 0。当前恢复量为基础值加上已达到各级增长之和; 全部设为 0 时不恢复。升级不再立即回复生命, 原升级回血字段已移除。恢复随游戏时间计算且不会超过生命上限, 暂停、选择奖励或对局结束时停止。HUD 与玩家调试信息显示该属性, 控制台可通过 `@SurvivorHealthRegen = 2.5` 调整为每秒恢复 2.5 点。

怪物 ID 修改会同步波次引用; 删除怪物会移除开局及入场生成组中的引用, 如果某波怪物池变空则使用剩余第一种怪物。至少保留一种怪物。波次池和控制台怪物预设随配置动态生成, `SurvivorSpawnEnemy("自定义ID", 12)` 可直接生成新增怪物。怪物技能页支持增删改, 修改技能 ID 会同步怪物绑定; 删除技能后若某怪物没有技能则使用剩余第一项。怪物、技能、波次统一检查引用后一起应用, 形状与体型变化同步到场上怪物, 删除的怪物实例及对象池会被清理。技能热更新按 ID 同步剩余 CD, 不会给所有怪物重新刷新完整冷却。

开局不持有战斗技能, 默认在 1、2、5、10 级各获得一次技能随机三选一, 只抽取启用且尚未拥有的技能。冲刺默认拥有, 不占技能获取次数。改造默认在 2–5 级每级发放一次, 此后在 7、9、11、13、15 级发放, 15 级后在 18、21、24…级发放。所有奖励等级与次数均在 Level 配置中调整。

改造三选一仅包含已拥有技能及默认冲刺, 每次先从各技能的未获得改造中抽取一项, 再随机展示至多三个不同技能的选项。候选不足三个时展示实际数量, 全部耗尽时跳过该次奖励。同一级先发技能选择再发改造选择, 一次跨越多级时按等级排队选择。控制台可用 `SurvivorChooseReward(1)` 选择第一项, `SurvivorGiveAllModifications("Bolt")` 获取能量弹并应用全部启用的改造。

运行时从 `Application.streamingAssetsPath` 加载; Android/WebGL 启动时通过 `UnityWebRequest` 异步读取。桌面重开对局会重新读取磁盘配置。编辑器内保存或外部编辑 JSON 后会同步当前对局: 波次更新不重放入场生成, 现有敌人重算生命上限并保留生命比例, 新生成敌人使用最新配置。Level 热更新保留已发放奖励, 按已获得等级重算生命上限、恢复和基础伤害, 当前生命只受新上限约束且不会立即回复。配置无效时拒绝应用并保留对局原配置。

## Survivor 开发工具

F1 打开工具, Cheat 中按 `Survivor/玩家`、`技能`、`改造`、`奖励`、`战场`、`对局` 分类操作。原有 `Demo*` 命令及属性已移除, 使用 `SurvivorHelp()` 查看入口。原先自动安装且未修改的三个示例宏会自动清理, 新快捷宏使用 `[Survivor]` 前缀。

| 操作 | 命令示例 |
|---|---|
| 修改玩家属性 | `SurvivorSetAttribute("BaseDamage", 100)`、`SurvivorSetAttribute("生命上限", 1000)` |
| 直接读写属性 | `@SurvivorHealth = 80`、`@SurvivorMaxHealth = 1000`、`@SurvivorHealthRegen = 10`、`@SurvivorMoveSpeed = 12`、`@SurvivorDamage = 100` |
| 无敌、恢复、复活 | `SurvivorGod(true)`、`SurvivorHeal()`、`SurvivorRevive()` |
| 设置等级、增加经验 | `SurvivorSetLevel(20)`、`SurvivorAddExperience(100)` |
| 获得技能、全部技能 | `SurvivorGiveSkill("Bolt")`、`SurvivorGiveAllSkills()` |
| 获得指定改造 | `SurvivorGiveModification("Bolt", "1")`, 也支持配置中的改造 ID 或名称 |
| 获得全部改造 | `SurvivorGiveAllModifications("Bolt")`, 无参数时获得全部启用技能及改造 |
| 移除技能或改造 | `SurvivorRemoveSkill("Bolt")`、`SurvivorRemoveModification("Bolt", "1")` |
| 列出技能、改造及持有状态 | `SurvivorSkills()` |
| 冷却、施放、自动攻击 | `SurvivorResetCooldowns()`、`SurvivorCast("Nova")`、`SurvivorAutoAttack(false)` |
| 清除待选奖励 | `SurvivorClearRewards()` |
| 刷怪、清场 | `SurvivorSpawnEnemy("Tank", 3)`、`SurvivorClearEnemies()`、`SurvivorAutoSpawn(false)` |
| 传送、暂停、倍速、重开 | `SurvivorTeleport(5, 8)`、`SurvivorPause(true)`、`SurvivorGameSpeed(2)`、`SurvivorRestart(42)` |
| 查看当前状态 | `SurvivorStatus()` |

直接获取技能不受等级奖励次数或技能槽位限制, 可同时拥有全部 8 个启用的战斗技能及默认冲刺; 相同技能不重复叠加, 已有改造保留。改造可以独立指定, 不要求按顺序解锁; 未拥有对应技能时自动获取, 每项改造仍只生效一次。禁用的技能或改造不会被发放, 技能与改造预设使用当前配置的 ID、名称及说明。

直接发放不要求或花费奖励次数, 会重建当前三选一以避免失效选项; 候选耗尽时按原规则跳过待选奖励。设置等级会同步累计经验、按配置重算生命上限/恢复/基础伤害, 保留已有技能与改造并清除待选奖励; 增加经验则正常发放升级奖励。属性修改立即影响当前对局, 生命上限降低时同步限制当前生命。所有调试修改仅作用于当前对局, 重开后按配置恢复。清场默认不掉落经验, 重置冷却包括默认冲刺。

## 在其他工程安装

本地安装选择 `Packages/com.sanmon.gamedevtools/package.json`。本次目录改造推送到远程后，Git 安装地址需包含包路径：

```text
https://github.com/Aralf-Z/UnityGameDevTools.git?path=/Packages/com.sanmon.gamedevtools#master
```

此前不含 `?path=` 的地址仅适用于旧目录结构。使用方应在迁移发布后更新地址，由 Unity 重新解析锁文件。

使用方法见 [包 README](Packages/com.sanmon.gamedevtools/README.md)，详细说明见 [开发与验证](Packages/com.sanmon.gamedevtools/Documentation~/development.md) 和 [编码规范](Packages/com.sanmon.gamedevtools/Documentation~/coding-standard.md)。许可：[MIT](LICENSE)。
