using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameConsole.Extension;
using RedSaw.CommandLineInterface;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    /// <summary> 当前对局的开发命令, 参数预设跟随运行时配置. </summary>
    public static class SurvivorCommands
    {
        private static SurvivorArena Game => SurvivorArena.Instance != null ? SurvivorArena.Instance : throw new InvalidOperationException("请先运行 Development 场景。");
        private static SurvivorSkillCatalog Skills => SurvivorArena.Instance?.SkillCatalog ?? SurvivorSkills.Load();

        [CommandProperty("SurvivorHealth")] public static float Health { get => Game.Stats.Health; set => Game.SetPlayerAttribute("Health", value); }
        [CommandProperty("SurvivorMaxHealth")] public static float MaxHealth { get => Game.Stats.MaxHealth; set => Game.SetPlayerAttribute("MaxHealth", value); }
        [CommandProperty("SurvivorHealthRegen")] public static float HealthRegeneration { get => Game.Stats.HealthRegenerationPerSecond; set => Game.SetPlayerAttribute("HealthRegenerationPerSecond", value); }
        [CommandProperty("SurvivorMoveSpeed")] public static float MoveSpeed { get => Game.Stats.MoveSpeed; set => Game.SetPlayerAttribute("MoveSpeed", value); }
        [CommandProperty("SurvivorDamage")] public static float Damage { get => Game.Stats.BaseDamage; set => Game.SetPlayerAttribute("BaseDamage", value); }
        [CommandProperty("SurvivorGodMode")] public static bool GodMode { get => Game.Stats.GodMode; set => Game.Stats.GodMode = value; }
        [CommandProperty("SurvivorLevel")] public static int Level { get => Game.Stats.Level; set => Game.SetPlayerLevel(value); }

        [Command(name: "SurvivorHelp", desc: "查看当前对局开发工具的使用方法", tag: "Survivor/对局")]
        [MethodAlias("开发命令说明")]
        public static string Help() =>
            "F1 打开开发工具, Cheat 按分类选择操作, 技能与改造预设来自当前配置。\n" +
            "玩家: SurvivorSetAttribute(\"BaseDamage\", 100)、@SurvivorHealth = 80、SurvivorGod(true)、SurvivorSetLevel(20)。\n" +
            "技能: SurvivorGiveSkill(\"Bolt\")、SurvivorGiveAllSkills()。直接获得技能不受等级奖励次数限制, 可同时持有全部启用技能。\n" +
            "改造: SurvivorGiveModification(\"Bolt\", \"1\") 支持改造 ID、名称或 1–5 序号; SurvivorGiveAllModifications() 获得全部启用技能及改造。\n" +
            "调试: SurvivorResetCooldowns()、SurvivorClearRewards()、SurvivorSpawnEnemy(\"Tank\", 3)、SurvivorClearEnemies()。\n" +
            "SurvivorSkills() 查看技能、改造 ID 和持有状态。直接发放不消耗待选奖励; 修改等级按配置重算成长属性并清除待选奖励。";

        [Command(name: "SurvivorStatus", desc: "查看玩家属性、战场和待选奖励", tag: "Survivor/对局")]
        [MethodAlias("查看对局状态")]
        public static string Status() =>
            $"等级 {Game.Stats.Level} | 生命 {Game.Stats.Health:0.##}/{Game.Stats.MaxHealth:0.##} | 恢复 {Game.Stats.HealthRegenerationPerSecond:0.##}/秒 | 移速 {Game.Stats.MoveSpeed:0.##} | 基础伤害 {Game.Stats.BaseDamage:0.##}\n" +
            $"无敌 {Game.Stats.GodMode} | 暂停 {Game.IsPaused} | 结束 {Game.HasEnded} | 倍速 {Game.GameSpeed:0.##} | 自动攻击 {Game.AutoAttack} | 自动刷怪 {Game.AutoSpawn}\n" +
            $"经验 {Game.Stats.Experience} | 波次 {Game.Stats.Wave} | 击杀 {Game.Stats.Kills} | 敌人 {Game.EnemyCount} | 时间 {Game.Stats.Elapsed:0.0}秒 | 待选技能 {Game.PendingSkills} / 改造 {Game.PendingModifications}";

        [Command(name: "SurvivorSetAttribute", desc: "设置生命、生命上限、每秒恢复、移速或基础伤害, 立即生效", tag: "Survivor/玩家")]
        [MethodAlias("修改玩家属性", "属性 ID 或名称", "数值")]
        [ParamDefine(typeof(SurvivorCommands), nameof(AttributePresets))]
        public static float SetAttribute(string attribute = "BaseDamage", float value = 100) => Game.SetPlayerAttribute(attribute, value);

        [Command(name: "SurvivorGod", desc: "开启或关闭玩家无敌", tag: "Survivor/玩家")]
        [MethodAlias("玩家无敌", "开启")]
        [Macro("[Survivor] 开启无敌", "true", "忽略玩家受到的伤害")]
        public static bool God(bool enabled = true) => Game.Stats.GodMode = enabled;

        [Command(name: "SurvivorHeal", desc: "恢复全部生命; 已结束的对局请使用复活", tag: "Survivor/玩家")]
        [MethodAlias("恢复全部生命")]
        public static float Heal() { Game.SetHealth(Game.Stats.MaxHealth); return Game.Stats.Health; }

        [Command(name: "SurvivorRevive", desc: "恢复全部生命并继续对局, 获得 3 秒保护", tag: "Survivor/玩家")]
        [MethodAlias("复活玩家")]
        public static void Revive() => Game.Revive();

        [Command(name: "SurvivorSetLevel", desc: "设置等级和对应经验, 重算成长属性并清除待选奖励, 不弹出升级选择", tag: "Survivor/玩家")]
        [MethodAlias("设置玩家等级", "等级")]
        public static int SetLevel(int level = 20) { Game.SetPlayerLevel(level); return Game.Stats.Level; }

        [Command(name: "SurvivorAddExperience", desc: "增加经验, 正常触发配置中的升级和奖励选择", tag: "Survivor/玩家")]
        [MethodAlias("增加玩家经验", "经验")]
        public static int AddExperience(int amount = 100) { Game.AddExperience(amount); return Game.Stats.Level; }

        [Command(name: "SurvivorTeleport", desc: "传送玩家到场地坐标, 自动限制在边界内", tag: "Survivor/玩家")]
        [MethodAlias("传送玩家", "X", "Z")]
        public static ArenaPoint Teleport(float x = 0, float z = 0) { Game.Teleport(new ArenaPoint(x, z)); return new ArenaPoint(Game.PlayerPosition.x, Game.PlayerPosition.z); }

        [Command(name: "SurvivorGiveSkill", desc: "直接获得指定技能, 无获取次数或槽位限制, 保留已有改造", tag: "Survivor/技能")]
        [MethodAlias("获得技能", "技能 ID 或名称")]
        [ParamDefine(typeof(SurvivorCommands), nameof(SkillPresets))]
        public static string GiveSkill(string skill = "Bolt")
        {
            var kind = ParseSkill(skill);
            return Game.GiveSkill(kind) ? "获得技能: " + Skills.Get(kind).displayName : "已拥有技能: " + Skills.Get(kind).displayName;
        }

        [Command(name: "SurvivorGiveAllSkills", desc: "直接获得全部启用的战斗技能, 不消耗等级奖励, 保留已有改造", tag: "Survivor/技能")]
        [MethodAlias("获得全部技能")]
        [Macro("[Survivor] 全部技能", "", "直接获得全部启用的战斗技能")]
        public static int GiveAllSkills() => Game.GiveAllSkills();

        [Command(name: "SurvivorRemoveSkill", desc: "移除指定战斗技能及其改造, 冲刺始终保留", tag: "Survivor/技能")]
        [MethodAlias("移除技能", "技能 ID 或名称")]
        [ParamDefine(typeof(SurvivorCommands), nameof(CombatSkillPresets))]
        public static bool RemoveSkill(string skill = "Bolt") => Game.RemoveSkill(ParseSkill(skill));

        [Command(name: "SurvivorSkills", desc: "列出技能和改造的 ID、名称、启用状态及持有状态", tag: "Survivor/技能")]
        [MethodAlias("查看技能与改造")]
        public static string ListSkills()
        {
            var lines = new List<string>();
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var kind = (SkillKind)i;
                var definition = Game.SkillCatalog.Get(kind);
                lines.Add($"{definition.id} / {definition.displayName} [{(definition.enabled ? Game.HasSkill(kind) ? "已拥有" : "未拥有" : "已禁用")}] 改造 {Game.ModificationCount(kind)}/5");
                for (var j = 0; j < definition.modifications.Length; j++)
                {
                    var item = definition.modifications[j];
                    var state = !item.enabled ? "已禁用" : (Game.ModificationMask(kind) & (1 << j)) != 0 ? "已获得" : "未获得";
                    lines.Add($"  {j + 1}. {item.id} / {item.displayName} [{state}]");
                }
            }
            return string.Join("\n", lines);
        }

        [Command(name: "SurvivorGiveModification", desc: "指定获得一项改造, 自动获得对应技能; 支持 ID、名称或 1–5 序号", tag: "Survivor/改造")]
        [MethodAlias("获得改造", "技能 ID 或名称", "改造 ID、名称或序号")]
        [ParamDefine(typeof(SurvivorCommands), nameof(ModificationPresets))]
        public static string GiveModification(string skill = "Bolt", string modification = "1")
        {
            var kind = ParseSkill(skill);
            var index = ParseModification(kind, modification);
            var name = Skills.Get(kind).displayName + " / " + Skills.Get(kind).modifications[index].displayName;
            return Game.GiveModification(kind, index) ? "获得改造: " + name : "已拥有改造: " + name;
        }

        [Command(name: "SurvivorGiveAllModifications", desc: "获得指定技能的全部启用改造; all 获得全部启用技能及改造", tag: "Survivor/改造")]
        [MethodAlias("获得全部改造", "技能 ID、名称或 all")]
        [ParamDefine(typeof(SurvivorCommands), nameof(AllSkillPresets))]
        [Macro("[Survivor] 全部技能及改造", "\"all\"", "获得全部启用技能与改造")]
        public static int GiveAllModifications(string skill = "all") => Game.GiveAllModifications(IsAll(skill) ? (SkillKind?)null : ParseSkill(skill));

        [Command(name: "SurvivorRemoveModification", desc: "移除指定改造, 保留技能及其他改造", tag: "Survivor/改造")]
        [MethodAlias("移除改造", "技能 ID 或名称", "改造 ID、名称或序号")]
        [ParamDefine(typeof(SurvivorCommands), nameof(ModificationPresets))]
        public static bool RemoveModification(string skill = "Bolt", string modification = "1")
        {
            var kind = ParseSkill(skill);
            return Game.RemoveModification(kind, ParseModification(kind, modification));
        }

        [Command(name: "SurvivorResetCooldowns", desc: "清除指定技能或全部技能的剩余冷却, 包含冲刺", tag: "Survivor/技能")]
        [MethodAlias("重置技能冷却", "技能 ID、名称或 all")]
        [ParamDefine(typeof(SurvivorCommands), nameof(AllSkillPresets))]
        [Macro("[Survivor] 重置冷却", "\"all\"", "立即清除所有玩家技能冷却")]
        public static void ResetCooldowns(string skill = "all") => Game.ResetSkillCooldowns(IsAll(skill) ? (SkillKind?)null : ParseSkill(skill));

        [Command(name: "SurvivorCast", desc: "立即释放已拥有技能; 可绕过冷却, 暂停或选择奖励时无法释放", tag: "Survivor/技能")]
        [MethodAlias("释放技能", "技能 ID 或名称", "绕过冷却")]
        [ParamDefine(typeof(SurvivorCommands), nameof(SkillPresets))]
        public static bool Cast(string skill = "Bolt", bool force = true)
        {
            var kind = ParseSkill(skill);
            if (Game.IsPaused || Game.IsChoosingReward || Game.HasEnded) return false;
            if (force && kind == SkillKind.Dash) Game.ResetSkillCooldowns(kind);
            return Game.TryCast(kind, force);
        }

        [Command(name: "SurvivorChooseReward", desc: "选择当前奖励界面的第 1–3 项", tag: "Survivor/奖励")]
        [MethodAlias("选择当前奖励", "选项 1–3")]
        public static bool ChooseReward(int option = 1) => Game.ChooseReward(option - 1);

        [Command(name: "SurvivorClearRewards", desc: "清除全部待选技能和改造, 关闭奖励选择界面以继续测试", tag: "Survivor/奖励")]
        [MethodAlias("清除待选奖励")]
        [Macro("[Survivor] 清除待选奖励", "", "关闭技能和改造选择, 保留已获得的效果")]
        public static int ClearRewards() => Game.ClearPendingRewards();

        [Command(name: "SurvivorSpawnEnemy", desc: "按当前怪物配置生成敌人, 遵循战场敌人数量上限", tag: "Survivor/战场")]
        [MethodAlias("生成敌人", "怪物 ID", "数量")]
        [ParamDefine(typeof(SurvivorCommands), nameof(EnemyPresets))]
        public static int SpawnEnemy(string enemy = "Grunt", int count = 12) => Game.Spawn(enemy, Mathf.Clamp(count, 0, Game.EnemyLimit));

        [Command(name: "SurvivorClearEnemies", desc: "清空场上敌人, 默认不掉落经验以免干扰测试", tag: "Survivor/战场")]
        [MethodAlias("清除敌人", "掉落经验")]
        [Macro("[Survivor] 清场", "false", "清除场上敌人, 不掉落经验")]
        public static int ClearEnemies(bool experience = false) => Game.ClearEnemies(experience);

        [Command(name: "SurvivorAutoSpawn", desc: "开启或关闭自动刷怪", tag: "Survivor/战场")]
        [MethodAlias("自动刷怪", "开启")]
        public static bool AutoSpawn(bool enabled = false) => Game.AutoSpawn = enabled;

        [Command(name: "SurvivorAutoAttack", desc: "开启或关闭玩家自动攻击", tag: "Survivor/战场")]
        [MethodAlias("自动攻击", "开启")]
        public static bool AutoAttack(bool enabled = true) => Game.AutoAttack = enabled;

        [Command(name: "SurvivorPause", desc: "暂停或恢复当前对局", tag: "Survivor/对局")]
        [MethodAlias("暂停对局", "暂停")]
        public static bool Pause(bool paused = true) { Game.SetPaused(paused); return Game.IsPaused; }

        [Command(name: "SurvivorGameSpeed", desc: "调整游戏倍速, 范围 0.25–3", tag: "Survivor/对局")]
        [MethodAlias("设置游戏倍速", "倍速")]
        public static float GameSpeed(float speed = 1) { Game.GameSpeed = speed; return Game.GameSpeed; }

        [Command(name: "SurvivorRestart", desc: "按随机种子重开并重新加载配置, 清除当前调试修改", tag: "Survivor/对局")]
        [MethodAlias("重新开始对局", "随机种子")]
        public static void Restart(int seed = 42) => Game.RestartRun(seed);

        public static List<ParamDefine> AttributePresets() => new()
        {
            new ParamDefine("\"Health\", 100", "当前生命", "范围 0–当前生命上限, 设为 0 会结束对局", "生命"),
            new ParamDefine("\"MaxHealth\", 1000", "生命上限", "同步限制当前生命, 不自动回复", "生命"),
            new ParamDefine("\"HealthRegenerationPerSecond\", 10", "每秒生命恢复", "0 表示不恢复", "生命"),
            new ParamDefine("\"MoveSpeed\", 10", "移动速度", "0 表示无法正常移动", "战斗"),
            new ParamDefine("\"BaseDamage\", 100", "基础伤害", "所有技能按此伤害与技能倍率计算", "战斗")
        };

        public static List<ParamDefine> SkillPresets() => BuildSkillPresets(false, false);
        public static List<ParamDefine> CombatSkillPresets() => BuildSkillPresets(true, false);
        public static List<ParamDefine> AllSkillPresets() => BuildSkillPresets(false, true);

        private static List<ParamDefine> BuildSkillPresets(bool combatOnly, bool includeAll)
        {
            var catalog = Skills;
            var result = new List<ParamDefine>();
            if (includeAll) result.Add(new ParamDefine("\"all\"", "全部技能", "对全部启用技能和默认冲刺执行操作", "全部"));
            var count = combatOnly ? SurvivorSkills.COMBAT_SKILL_COUNT : SurvivorSkills.SKILL_COUNT;
            for (var i = 0; i < count; i++)
            {
                var definition = catalog.Get((SkillKind)i);
                if (!definition.enabled) continue;
                result.Add(new ParamDefine(Quote(definition.id), definition.displayName, definition.description, i == (int)SkillKind.Dash ? "冲刺" : "战斗技能", definition.id, LoadPresetIcon));
            }
            return result;
        }

        public static List<ParamDefine> ModificationPresets()
        {
            var result = new List<ParamDefine>();
            var catalog = Skills;
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var definition = catalog.Get((SkillKind)i);
                if (!definition.enabled) continue;
                foreach (var modification in definition.modifications)
                {
                    if (!modification.enabled) continue;
                    result.Add(new ParamDefine(Quote(definition.id) + ", " + Quote(modification.id), modification.displayName, modification.additionalDescription, definition.displayName, definition.id, LoadPresetIcon));
                }
            }
            return result;
        }

        public static List<ParamDefine> EnemyPresets()
        {
            var catalog = SurvivorArena.Instance?.EnemyCatalog ?? SurvivorEnemies.Parse(SurvivorConfiguration.ReadJson("Enemies.json"));
            return catalog.enemies.Select(enemy => new ParamDefine(Quote(enemy.id) + ", " + (enemy.heavy ? 3 : 12), enemy.displayName, enemy.id, "怪物", enemy.id, LoadPresetIcon)).ToList();
        }

        private static SkillKind ParseSkill(string name)
        {
            var catalog = Skills;
            name = (name ?? string.Empty).Trim();
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var definition = catalog.Get((SkillKind)i);
                if (string.Equals(definition.id, name, StringComparison.OrdinalIgnoreCase) || definition.displayName == name) return (SkillKind)i;
            }
            throw new ArgumentException("未知技能: " + name + "。使用 SurvivorSkills() 查看技能 ID。");
        }

        private static int ParseModification(SkillKind skill, string name)
        {
            var modifications = Skills.Get(skill).modifications;
            name = (name ?? string.Empty).Trim();
            for (var i = 0; i < modifications.Length; i++)
                if (string.Equals(modifications[i].id, name, StringComparison.OrdinalIgnoreCase) || modifications[i].displayName == name) return i;
            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= modifications.Length) return number - 1;
            throw new ArgumentException("未知改造: " + name + "。使用改造 ID、名称或 1–5 序号。");
        }

        private static bool IsAll(string value) => string.Equals(value?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        private static Sprite LoadPresetIcon(string key) => SurvivorArena.Instance != null ? SurvivorArena.Instance.PresetIcon(key) : null;

        // 只移除旧版本自动安装且正文未修改的三个示例宏, 保留用户创建或编辑过的宏.
        internal static void RemoveLegacyExampleMacros()
        {
            var system = Game.Tools?.Macros;
            if (system == null) return;
            var examples = new (string name, string body)[]
            {
                ("[示例] 三波技能", "Repeat(3)\nDemoSpawn(\"Grunt\", 6)\nDemoCast(\"Nova\")\nWaitFor(0.3)\nEndRepeat()\nDemoExperience(20)\nDemoStatus()"),
                ("[示例] 异步与协程", "WaitFor(DemoSpawnStream(\"Runner\", 3, 0.1))\nWaitFor(DemoHealLater(0.1, 10))\nDemoStatus()"),
                ("[示例] 移动与坐标", "DemoGod(true)\nDemoMove(1, 0, 0.4)\nWaitFor(0.5)\nDemoTeleport(\"0,0\")\nDemoStatus()")
            };
            foreach (var macro in system.Macros.ToArray())
            {
                if (macro.IsBuiltIn || !examples.Any(example => example.name == macro.name && example.body == macro.macro.Replace("\r\n", "\n"))) continue;
                if (!system.Delete(macro, out var error)) Debug.LogWarning("[Survivor] 无法移除旧示例宏: " + error);
            }
        }
    }
}
