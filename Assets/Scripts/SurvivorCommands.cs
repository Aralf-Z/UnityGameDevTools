using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GameConsole.Extension;
using RedSaw.CommandLineInterface;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    /// <summary> GameDevTools 接入范例：命令、属性、预设、别名、宏、参数解析和调试对象。 </summary>
    public static class SurvivorCommands
    {
        private static SurvivorArena Game => SurvivorArena.Instance != null ? SurvivorArena.Instance : throw new InvalidOperationException("Start the Development scene first.");

        public static readonly (string name, string description, string body)[] ExampleMacros =
        {
            ("[示例] 三波技能", "Repeat / WaitFor / 生成与清场", "Repeat(3)\nDemoSpawn(\"Grunt\", 6)\nDemoCast(\"Nova\")\nWaitFor(0.3)\nEndRepeat()\nDemoExperience(20)\nDemoStatus()"),
            ("[示例] 异步与协程", "WaitFor(IEnumerator) 与 WaitFor(Task)", "WaitFor(DemoSpawnStream(\"Runner\", 3, 0.1))\nWaitFor(DemoHealLater(0.1, 10))\nDemoStatus()"),
            ("[示例] 移动与坐标", "移动控制、自定义参数解析与状态返回", "DemoGod(true)\nDemoMove(1, 0, 0.4)\nWaitFor(0.5)\nDemoTeleport(\"0,0\")\nDemoStatus()")
        };

        [CommandProperty("DemoHealth")] public static float Health { get => Game.Stats.Health; set => Game.SetHealth(value); }
        [CommandProperty("DemoMaxHealth")] public static float MaxHealth { get => Game.Stats.MaxHealth; set { Game.Stats.MaxHealth = SurvivorArena.FiniteClamp(value, 10, 5000); Game.SetHealth(Mathf.Min(Game.Stats.Health, Game.Stats.MaxHealth)); } }
        [CommandProperty("DemoSpeed")] public static float Speed { get => Game.Stats.MoveSpeed; set => Game.Stats.MoveSpeed = SurvivorArena.FiniteClamp(value, 0, 25); }
        [CommandProperty("DemoDamage")] public static float Damage { get => Game.Stats.DamageMultiplier; set => Game.Stats.DamageMultiplier = SurvivorArena.FiniteClamp(value, 0, 100); }
        [CommandProperty("DemoGodMode")] public static bool GodMode { get => Game.Stats.GodMode; set => Game.Stats.GodMode = value; }
        [CommandProperty("DemoPaused")] public static bool Paused { get => Game.IsPaused; set => Game.SetPaused(value); }
        [CommandProperty("DemoAutoSpawn")] public static bool AutoSpawn { get => Game.AutoSpawn; set => Game.AutoSpawn = value; }
        [CommandProperty("DemoAutoAttack")] public static bool AutoAttack { get => Game.AutoAttack; set => Game.AutoAttack = value; }
        [CommandProperty("DemoTimeScale")] public static float TimeScale { get => Game.GameSpeed; set => Game.GameSpeed = value; }
        [CommandProperty("DemoSfxVolume")] public static float SfxVolume { get => Game.SfxVolume; set => Game.SfxVolume = value; }
        [CommandProperty("DemoEnemyCount")] public static int EnemyCount => Game.EnemyCount;
        [CommandProperty("DemoElapsed")] public static float Elapsed => Game.Stats.Elapsed;
        [CommandProperty("DemoPosition")] public static ArenaPoint Position { get => new(Game.PlayerPosition.x, Game.PlayerPosition.z); set => Game.Teleport(value); }

        [Command(name: "DemoHelp", desc: "范例入口：玩法、命令、属性、宏和工具生命周期", tag: "示例/说明")]
        [MethodAlias("查看接入说明")]
        public static string Help() =>
            "WASD/方向键移动；战斗技能按冷却自动释放；Space 冲刺；Esc 暂停；F1 开关工具。\n" +
            "命令：DemoSpawn(\"Tank\", 3)、DemoCast(\"Frost\")、DemoTeleport(\"5,8\")、DemoInspect()。\n" +
            "属性：@DemoHealth = 80、@DemoSpeed = 8、@DemoGodMode = true、@DemoPaused = true；@DemoEnemyCount 只读。\n" +
            "Cheat 面板包含分类、中文别名、参数编辑与参数预设。Macro 面板包含内置快捷宏和三个 [示例] 多行宏。\n" +
            "DemoHealLater 返回 Task，DemoSpawnStream 返回 IEnumerator：在宏中通过 WaitFor(...) 等待；Repeat/EndRepeat 可组合。\n" +
            "DemoLog(\"warning\")、DemoLog(\"error\")、DemoLog(\"exception\") 故意产生对应日志，仅在需要时调用。\n" +
            "DemoRegisterFont() 演示外部字体；DemoTools(\"restart\") 重启；shutdown 后按 F1 可恢复。Options 可测试 UIStyle、字体、语言、浮窗和日志设置。";

        [Command(name: "DemoStatus", desc: "返回当前战斗状态，演示返回值与命令补全", tag: "示例/游戏")]
        [MethodAlias("战斗状态")]
        public static string Status() => $"Lv.{Game.Stats.Level} HP {Game.Stats.Health:0}/{Game.Stats.MaxHealth:0} | Wave {Game.Stats.Wave} | Kills {Game.Stats.Kills} | Enemies {Game.EnemyCount} | {Game.Stats.Elapsed:0.0}s | paused={Game.IsPaused}";

        [Command(name: "DemoRestart", desc: "使用固定随机种子重开，可重复测试", tag: "示例/游戏")]
        [MethodAlias("重新开始", "种子")]
        [Macro("[快捷] 重新开始", "42", "重新开始生存测试")]
        public static string Restart(int seed = 42) { Game.RestartRun(seed); return "Run restarted: " + seed; }

        [Command(name: "DemoPause", desc: "暂停或恢复；工具与宏仍工作", tag: "示例/游戏")]
        [MethodAlias("暂停游戏", "暂停")]
        public static bool Pause(bool paused = true) { Game.SetPaused(paused); return Game.IsPaused; }

        [Command(name: "DemoGod", desc: "切换无敌，演示 bool 参数和属性联动", tag: "示例/玩家")]
        [MethodAlias("玩家无敌", "开启")]
        [Macro("[快捷] 开启无敌", "true", "开启玩家无敌")]
        public static bool God(bool enabled = true) => Game.Stats.GodMode = enabled;

        [Command(name: "DemoHeal", desc: "恢复生命，不会自动复活已结束的对局", tag: "示例/玩家")]
        [MethodAlias("治疗", "生命值")]
        public static float Heal(float amount = 25) { Game.SetHealth(Game.Stats.Health + SurvivorArena.FiniteClamp(amount, 0, 5000)); return Game.Stats.Health; }

        [Command(name: "DemoHurt", desc: "伤害玩家，遵循无敌与受击保护", tag: "示例/玩家")]
        [MethodAlias("伤害玩家", "伤害")]
        public static float Hurt(float damage = 20) { Game.HurtPlayer(damage); return Game.Stats.Health; }

        [Command(name: "DemoRevive", desc: "恢复生命并复活，给予 3 秒保护", tag: "示例/玩家")]
        [MethodAlias("复活玩家")]
        public static void Revive() => Game.Revive();

        [Command(name: "DemoExperience", desc: "增加经验，升级后弹出四选一技能升级界面", tag: "示例/玩家")]
        [MethodAlias("增加经验", "经验")]
        public static int Experience(int amount = 20) { Game.AddExperience(amount); return Game.Stats.Level; }

        [Command(name: "DemoMove", desc: "控制一小段移动，供宏和自动回归使用", tag: "示例/玩家")]
        [MethodAlias("移动演示", "横向", "纵向", "秒数")]
        public static void Move(float x = 1, float z = 0, float seconds = 1) => Game.MoveFor(new Vector2(x, z), seconds);

        [Command(name: "DemoTeleport", desc: "自定义 ArenaPoint 参数，格式为引号内 x,z", tag: "示例/玩家")]
        [MethodAlias("传送", "坐标 x,z")]
        [ParamDefine(typeof(SurvivorCommands), nameof(TeleportPresets))]
        public static ArenaPoint Teleport(ArenaPoint point) { Game.Teleport(point); return Position; }

        [CommandValueParser(typeof(ArenaPoint), Alias = "arenaPoint")]
        public static bool ParsePoint(string input, out object value)
        {
            value = null;
            var parts = (input ?? string.Empty).Trim().Trim('(', ')').Split(',');
            if (parts.Length != 2 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) || !SurvivorArena.IsFinite(x) || !SurvivorArena.IsFinite(z)) return false;
            value = new ArenaPoint(x, z);
            return true;
        }

        [Command(name: "DemoSpawn", desc: "生成 Grunt、Runner 或 Tank；总数有上限", tag: "示例/敌人")]
        [MethodAlias("生成敌人", "类型", "数量")]
        [ParamDefine(typeof(SurvivorCommands), nameof(SpawnPresets))]
        [Macro("[快捷] 普通敌人 ×12", "\"Grunt\", 12", "生成一组普通敌人")]
        public static int Spawn(string kind = "Grunt", int count = 12) => Game.Spawn(ParseEnum<EnemyKind>(kind), Mathf.Clamp(count, 0, SurvivorArena.EnemyLimit));

        [Command(name: "DemoSpawnAt", desc: "在指定坐标附近生成，演示复杂参数组合", tag: "示例/敌人")]
        [MethodAlias("定点生成", "坐标", "类型", "数量")]
        public static int SpawnAt(ArenaPoint point, string kind = "Grunt", int count = 5) => Game.Spawn(ParseEnum<EnemyKind>(kind), Mathf.Clamp(count, 0, SurvivorArena.EnemyLimit), point);

        [Command(name: "DemoClear", desc: "清除敌人；可选择掉落经验", tag: "示例/敌人")]
        [MethodAlias("清除敌人", "掉落经验")]
        [Macro("[快捷] 清场", "true", "清除敌人并掉落经验")]
        public static int Clear(bool experience = true) => Game.ClearEnemies(experience);

        [Command(name: "DemoCast", desc: "释放 Bolt、Orbit、Nova、Frost；可绕过冷却", tag: "示例/技能")]
        [MethodAlias("释放技能", "技能", "绕过冷却")]
        [ParamDefine(typeof(SurvivorCommands), nameof(SkillPresets))]
        [Macro("[快捷] 冲击波", "\"Nova\", true", "立即释放冲击波")]
        public static bool Cast(string skill = "Nova", bool force = true) => Game.TryCast(ParseEnum<SkillKind>(skill), force);

        [Command(name: "DemoSkillLevel", desc: "0 禁用，1 基础，2 为 T1，3 为 T2，4 为 T3；最高阶按 JSON 配置", tag: "示例/技能")]
        [MethodAlias("设置技能等级", "技能", "等级")]
        public static int SkillLevel(string skill = "Bolt", int level = 3) { var kind = ParseEnum<SkillKind>(skill); Game.SetSkillLevel(kind, level); return Game.SkillLevel(kind); }

        [Command(name: "DemoDash", desc: "立即尝试冲刺，遵循暂停与冷却", tag: "示例/技能")]
        [MethodAlias("冲刺")]
        public static bool Dash() => Game.Dash();

        [Command(name: "DemoHealLater", desc: "返回 Task；宏中 WaitFor(DemoHealLater(...)) 等待", tag: "示例/异步")]
        [MethodAlias("异步治疗", "实时时间秒", "生命值")]
        public static Task<float> HealLater(float seconds = 0.5f, float amount = 25) => HealLaterCore(Game, SurvivorArena.FiniteClamp(seconds, 0, 10), SurvivorArena.FiniteClamp(amount, 0, 5000));

        private static async Task<float> HealLaterCore(SurvivorArena game, float seconds, float amount)
        {
            await Task.Delay(Mathf.RoundToInt(seconds * 1000), game.Lifetime);
            if (game == null || game.Lifetime.IsCancellationRequested) return 0;
            game.SetHealth(game.Stats.Health + amount);
            Debug.Log("[Survivor] Task heal completed.");
            return game.Stats.Health;
        }

        [Command(name: "DemoSpawnStream", desc: "返回 IEnumerator；在宏中 WaitFor(...) 执行，使用实时时间", tag: "示例/异步")]
        [MethodAlias("协程生成敌人", "类型", "数量", "实时间隔")]
        public static IEnumerator SpawnStream(string kind = "Grunt", int count = 5, float interval = 0.2f) => SpawnStreamCore(Game, ParseEnum<EnemyKind>(kind), Mathf.Clamp(count, 0, 100), SurvivorArena.FiniteClamp(interval, 0.02f, 2));

        private static IEnumerator SpawnStreamCore(SurvivorArena game, EnemyKind enemy, int count, float interval)
        {
            var wait = new WaitForSecondsRealtime(interval);
            for (var i = 0; i < count; i++)
            {
                if (game == null || game.Lifetime.IsCancellationRequested) yield break;
                game.Spawn(enemy, 1);
                yield return wait;
            }
        }

        [Command(name: "DemoInspect", desc: "读取 DebugObject / DebugInfo 的字段及颜色", tag: "示例/诊断")]
        [MethodAlias("调试玩家对象")]
        public static string Inspect() => string.Join("\n", Game.Stats.GetDebugInfos().Select(info => "<color=" + info.Item2 + ">" + info.Item1 + "</color>"));

        [Command(name: "DemoLog", desc: "log/warning/error/exception/dev；错误类日志仅供主动测试", tag: "示例/诊断")]
        [MethodAlias("日志分类测试", "类别", "文本")]
        public static void Log(string kind = "log", string message = "Survivor log sample")
        {
            switch (kind.ToLowerInvariant())
            {
                case "log": Debug.Log(message); break;
                case "warning": Debug.LogWarning(message); break;
                case "error": Debug.LogError(message); break;
                case "exception": Debug.LogException(new InvalidOperationException(message)); break;
                case "dev": Game.Tools?.Print(message); break;
                default: throw new ArgumentException("Use log, warning, error, exception or dev.");
            }
        }

        [Command(name: "DemoConsoleOnly", desc: "CheatIgnore 示例：仅出现在 Console 补全中", tag: "示例/诊断")]
        [CheatIgnore]
        public static string ConsoleOnly() => "This command is hidden from the Cheat panel.";

        [Command(name: "DemoRegisterFont", desc: "从宿主 Resources 注册字体，可在 Options 选择", tag: "示例/工具")]
        [MethodAlias("注册外部字体", "Resources 路径")]
        public static bool RegisterFont(string resourcePath = "Font/AliPuHui")
        {
            var font = Resources.Load<Font>(resourcePath);
            if (font == null) throw new ArgumentException("Font resource not found: " + resourcePath);
            return Game.Tools != null && Game.Tools.RegisterFont(font.name, font);
        }

        [Command(name: "DemoTools", desc: "restart/shutdown/clear/stopLogs；释放后 F1 恢复", tag: "示例/工具")]
        [MethodAlias("工具生命周期", "操作")]
        public static string Tools(string action = "restart")
        {
            var tools = Game.Tools;
            if (tools == null) return "No tools controller.";
            switch (action.ToLowerInvariant())
            {
                case "restart": Game.RestartTools(false); break;
                case "shutdown": tools.Shutdown(); break;
                case "clear": tools.ClearOutput(); break;
                case "stoplogs": tools.ShutdownLogSystem(); break;
                default: throw new ArgumentException("Use restart, shutdown, clear or stopLogs.");
            }
            return "Tools: " + action;
        }

        [Command(name: "DemoInstallMacros", desc: "添加三个示例宏；同名不覆盖、重复调用不新增", tag: "示例/工具")]
        [MethodAlias("安装示例宏")]
        public static string InstallExampleMacros()
        {
            var system = Game.Tools?.Macros;
            if (system == null) return "Macro service unavailable.";
            var added = 0;
            foreach (var example in ExampleMacros)
            {
                if (system.Macros.Any(macro => macro.name == example.name)) continue;
                var macro = new Macro(Macro.Compose(example.name, example.description, example.body));
                if (!system.Save(macro, out var error)) throw new InvalidOperationException("Invalid sample macro: " + error);
                added++;
            }
            return "Example macros added: " + added;
        }

        public static List<ParamDefine> SpawnPresets() => new()
        {
            new ParamDefine("\"Grunt\", 12", "普通敌人 ×12", "基础追踪敌人", "普通", "Grunt", LoadPresetIcon),
            new ParamDefine("\"Runner\", 16", "快速敌人 ×16", "快速、低生命", "快速", "Runner", LoadPresetIcon),
            new ParamDefine("\"Tank\", 3", "重型敌人 ×3", "缓慢、高生命", "重型", "Tank", LoadPresetIcon),
            new ParamDefine("\"Grunt\", 180", "压力测试 ×180", "达到实例上限，测试日志和性能浮窗", "压力", "Grunt", LoadPresetIcon)
        };
        public static List<ParamDefine> SkillPresets()
        {
            var catalog = SurvivorArena.Instance?.SkillCatalog ?? SurvivorSkills.Load();
            var result = new List<ParamDefine>();
            for (var i = 0; i < 4; i++)
            {
                var skill = (SkillKind)i;
                var definition = catalog.Get(skill);
                result.Add(new ParamDefine("\"" + skill + "\", true", definition.displayName, definition.description, i < 2 ? "攻击" : "范围", skill.ToString(), LoadPresetIcon));
            }
            return result;
        }
        public static List<ParamDefine> TeleportPresets() => new()
        {
            new ParamDefine("\"0,0\"", "场地中心", "自定义 ArenaPoint 参数", "坐标"),
            new ParamDefine("\"16,16\"", "东北方向", "传送到东北区域", "坐标"),
            new ParamDefine("\"-16,-16\"", "西南方向", "传送到西南区域", "坐标")
        };

        private static T ParseEnum<T>(string name) where T : struct, Enum => Enum.TryParse<T>(name, true, out var result) && Enum.IsDefined(typeof(T), result)
            ? result : throw new ArgumentException("Unknown " + typeof(T).Name + ": " + name);

        private static Sprite LoadPresetIcon(string key) => SurvivorArena.Instance != null ? SurvivorArena.Instance.PresetIcon(key) : null;
    }
}
