using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public enum SurvivorSkillParameter
    {
        Cooldown, Radius, Damage, ProjectileCount, VolleyCount, VolleyInterval,
        ProjectileSpeed, ProjectileLifetime, ProjectileSpread, OrbiterCount,
        OrbitSpeed, BladeRotationSpeed, OrbitHeight, HitInterval, Duration,
        TickInterval, Knockback, KnockbackDecay, HeavyKnockbackMultiplier
    }

    [Serializable]
    public sealed class SurvivorDebuffConfig
    {
        public string type = "Freeze";
        public bool enabled = true;
        public float duration = 3;
        public float tickInterval = 1;
        public float currentHealthPercent = 0.05f;
        public float moveSpeedMultiplier = 0.25f;
    }

    [Serializable]
    public sealed class SurvivorSkillStats
    {
        public float cooldown = 1, radius = 1, damage;
        public int projectileCount = 1, volleyCount = 1, orbiterCount = 3;
        public float volleyInterval = 0.12f, projectileSpeed = 18, projectileLifetime = 1.6f, projectileSpread = 11;
        public float orbitSpeed = 2.7f, bladeRotationSpeed = 140, orbitHeight = 0.85f, hitInterval = 0.4f;
        public float duration = 2, tickInterval = 0.5f, knockback, knockbackDecay = 6, heavyKnockbackMultiplier = 0.55f;
        public string effectColor = "#FFFFFF";
        public SurvivorDebuffConfig[] debuffs = Array.Empty<SurvivorDebuffConfig>();
        [NonSerialized] public Color color;

        public SurvivorSkillStats Copy()
        {
            var copy = (SurvivorSkillStats)MemberwiseClone();
            copy.debuffs = (SurvivorDebuffConfig[])(debuffs ?? Array.Empty<SurvivorDebuffConfig>()).Clone();
            return copy;
        }

        public float Get(SurvivorSkillParameter parameter) => parameter switch
        {
            SurvivorSkillParameter.Cooldown => cooldown, SurvivorSkillParameter.Radius => radius,
            SurvivorSkillParameter.Damage => damage, SurvivorSkillParameter.ProjectileCount => projectileCount,
            SurvivorSkillParameter.VolleyCount => volleyCount, SurvivorSkillParameter.VolleyInterval => volleyInterval,
            SurvivorSkillParameter.ProjectileSpeed => projectileSpeed, SurvivorSkillParameter.ProjectileLifetime => projectileLifetime,
            SurvivorSkillParameter.ProjectileSpread => projectileSpread, SurvivorSkillParameter.OrbiterCount => orbiterCount,
            SurvivorSkillParameter.OrbitSpeed => orbitSpeed, SurvivorSkillParameter.BladeRotationSpeed => bladeRotationSpeed,
            SurvivorSkillParameter.OrbitHeight => orbitHeight, SurvivorSkillParameter.HitInterval => hitInterval,
            SurvivorSkillParameter.Duration => duration, SurvivorSkillParameter.TickInterval => tickInterval,
            SurvivorSkillParameter.Knockback => knockback, SurvivorSkillParameter.KnockbackDecay => knockbackDecay,
            SurvivorSkillParameter.HeavyKnockbackMultiplier => heavyKnockbackMultiplier, _ => 0
        };

        public void Set(SurvivorSkillParameter parameter, float value)
        {
            switch (parameter)
            {
                case SurvivorSkillParameter.Cooldown: cooldown = value; break;
                case SurvivorSkillParameter.Radius: radius = value; break;
                case SurvivorSkillParameter.Damage: damage = value; break;
                case SurvivorSkillParameter.ProjectileCount: projectileCount = Mathf.RoundToInt(value); break;
                case SurvivorSkillParameter.VolleyCount: volleyCount = Mathf.RoundToInt(value); break;
                case SurvivorSkillParameter.VolleyInterval: volleyInterval = value; break;
                case SurvivorSkillParameter.ProjectileSpeed: projectileSpeed = value; break;
                case SurvivorSkillParameter.ProjectileLifetime: projectileLifetime = value; break;
                case SurvivorSkillParameter.ProjectileSpread: projectileSpread = value; break;
                case SurvivorSkillParameter.OrbiterCount: orbiterCount = Mathf.RoundToInt(value); break;
                case SurvivorSkillParameter.OrbitSpeed: orbitSpeed = value; break;
                case SurvivorSkillParameter.BladeRotationSpeed: bladeRotationSpeed = value; break;
                case SurvivorSkillParameter.OrbitHeight: orbitHeight = value; break;
                case SurvivorSkillParameter.HitInterval: hitInterval = value; break;
                case SurvivorSkillParameter.Duration: duration = value; break;
                case SurvivorSkillParameter.TickInterval: tickInterval = value; break;
                case SurvivorSkillParameter.Knockback: knockback = value; break;
                case SurvivorSkillParameter.KnockbackDecay: knockbackDecay = value; break;
                case SurvivorSkillParameter.HeavyKnockbackMultiplier: heavyKnockbackMultiplier = value; break;
            }
        }
    }

    [Serializable]
    public sealed class SurvivorSkillValueOverride
    {
        public string parameter = "Damage";
        public string operation = "Set";
        public float value;
    }

    [Serializable]
    public sealed class SurvivorSkillUpgrade
    {
        public bool enabled;
        public string displayName = "预留";
        [TextArea] public string additionalDescription = "";
        public string effectColor = "";
        public SurvivorSkillValueOverride[] overrides = Array.Empty<SurvivorSkillValueOverride>();
        public SurvivorDebuffConfig[] debuffOverrides = Array.Empty<SurvivorDebuffConfig>();
    }

    [Serializable]
    public sealed class SurvivorSkillDefinition
    {
        public string id;
        public bool enabled = true;
        public string displayName;
        [TextArea] public string description;
        public SurvivorSkillStats baseStats = new();
        public SurvivorSkillUpgrade t1 = new(), t2 = new(), t3 = new();
        public SurvivorSkillUpgrade Upgrade(int tier) => tier == 1 ? t1 : tier == 2 ? t2 : tier == 3 ? t3 : null;
        public int MaxTier => !t1.enabled ? 0 : !t2.enabled ? 1 : !t3.enabled ? 2 : 3;
    }

    [Serializable]
    public sealed class SurvivorSkillCatalog
    {
        public int version = 1;
        public SurvivorSkillDefinition[] skills;
        [NonSerialized] private SurvivorSkillDefinition[] _ordered;
        [NonSerialized] private SurvivorSkillStats[][] _resolved;
        [NonSerialized] private string[][] _descriptions;

        public SurvivorSkillDefinition Get(SkillKind skill) { if (_ordered == null) Rebuild(); return _ordered[(int)skill]; }
        public SurvivorSkillStats Resolve(SkillKind skill, int tier)
        {
            if (_resolved == null) Rebuild();
            return _resolved[(int)skill][Mathf.Clamp(tier, 0, Get(skill).MaxTier)];
        }

        public string Description(SkillKind skill, int tier)
        {
            if (_descriptions == null) Rebuild();
            return _descriptions[(int)skill][Mathf.Clamp(tier, 0, Get(skill).MaxTier)];
        }

        public void Rebuild()
        {
            if (version != 1 || skills == null || skills.Length != 4) throw new ArgumentException("技能配置版本必须为 1，并包含 Bolt、Orbit、Nova、Frost 四个技能。");
            var ordered = new SurvivorSkillDefinition[4];
            var resolved = new SurvivorSkillStats[4][];
            var descriptions = new string[4][];
            foreach (var definition in skills)
            {
                if (definition == null || !Enum.TryParse(definition.id, false, out SkillKind kind) || !Enum.IsDefined(typeof(SkillKind), kind))
                    throw new ArgumentException("技能 id 必须为 Bolt、Orbit、Nova 或 Frost。");
                var index = (int)kind;
                if (ordered[index] != null) throw new ArgumentException("重复的技能 id：" + definition.id);
                if (string.IsNullOrWhiteSpace(definition.displayName) || definition.baseStats == null) throw new ArgumentException(definition.id + " 缺少名称或基础数值。");
                definition.t1 ??= new SurvivorSkillUpgrade(); definition.t2 ??= new SurvivorSkillUpgrade(); definition.t3 ??= new SurvivorSkillUpgrade();
                if ((definition.t2.enabled && !definition.t1.enabled) || (definition.t3.enabled && !definition.t2.enabled))
                    throw new ArgumentException(definition.displayName + " 的强化必须从 T1 起连续启用。");
                ordered[index] = definition;
                resolved[index] = new SurvivorSkillStats[4];
                descriptions[index] = new string[4];
                descriptions[index][0] = definition.description ?? "";
                var current = definition.baseStats.Copy();
                SurvivorSkills.ValidateDebuffs(current.debuffs);
                SurvivorSkills.ValidateStats(kind, current);
                resolved[index][0] = current;
                for (var tier = 1; tier <= 3; tier++)
                {
                    var upgrade = definition.Upgrade(tier);
                    if (upgrade.enabled && string.IsNullOrWhiteSpace(upgrade.displayName)) throw new ArgumentException(definition.displayName + " T" + tier + " 缺少名称。");
                    var next = current.Copy();
                    var seen = new HashSet<SurvivorSkillParameter>();
                    foreach (var change in upgrade.overrides ?? Array.Empty<SurvivorSkillValueOverride>())
                    {
                        if (change == null || !Enum.TryParse(change.parameter, false, out SurvivorSkillParameter parameter) || !Enum.IsDefined(typeof(SurvivorSkillParameter), parameter))
                            throw new ArgumentException(definition.id + " T" + tier + " 包含未知的数值项目。");
                        if (!seen.Add(parameter)) throw new ArgumentException(definition.id + " T" + tier + " 重复覆盖 " + parameter);
                        if (Array.IndexOf(SurvivorSkills.Parameters(kind), parameter) < 0) throw new ArgumentException(definition.id + " 不使用参数 " + parameter);
                        if (!SurvivorArena.IsFinite(change.value)) throw new ArgumentException("强化数值必须是有限数字。");
                        var operation = string.IsNullOrEmpty(change.operation) ? "Set" : change.operation;
                        var value = operation switch
                        {
                            "Set" => change.value, "Add" => next.Get(parameter) + change.value,
                            "Multiply" => next.Get(parameter) * change.value,
                            _ => throw new ArgumentException("覆盖方式必须为 Set、Add 或 Multiply。")
                        };
                        SurvivorSkills.ValidateValue(parameter, value);
                        next.Set(parameter, value);
                    }
                    if (!string.IsNullOrWhiteSpace(upgrade.effectColor)) next.effectColor = upgrade.effectColor;
                    SurvivorSkills.ValidateDebuffs(upgrade.debuffOverrides);
                    var debuffs = new List<SurvivorDebuffConfig>(next.debuffs);
                    foreach (var change in upgrade.debuffOverrides ?? Array.Empty<SurvivorDebuffConfig>())
                    {
                        debuffs.RemoveAll(item => item.type == change.type);
                        if (change.enabled) debuffs.Add(change);
                    }
                    next.debuffs = debuffs.ToArray();
                    SurvivorSkills.ValidateStats(kind, next);
                    resolved[index][tier] = next;
                    descriptions[index][tier] = descriptions[index][tier - 1] + (string.IsNullOrWhiteSpace(upgrade.additionalDescription) ? "" : "\n" + upgrade.additionalDescription);
                    current = next;
                }
            }
            _ordered = ordered;
            _resolved = resolved;
            _descriptions = descriptions;
        }
    }

    public static class SurvivorSkills
    {
        public const string ResourcePath = "SurvivorDemo/Skills";
        public const string AssetPath = "Assets/Scripts/Resources/SurvivorDemo/Skills.json";
        public static readonly string[] DebuffTypes = { "Freeze", "Slow", "Bleed" };
        private static readonly SurvivorSkillParameter[][] ParameterSets =
        {
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.Damage, SurvivorSkillParameter.ProjectileCount, SurvivorSkillParameter.VolleyCount, SurvivorSkillParameter.VolleyInterval, SurvivorSkillParameter.ProjectileSpeed, SurvivorSkillParameter.ProjectileLifetime, SurvivorSkillParameter.ProjectileSpread },
            new[] { SurvivorSkillParameter.Radius, SurvivorSkillParameter.Damage, SurvivorSkillParameter.OrbiterCount, SurvivorSkillParameter.OrbitSpeed, SurvivorSkillParameter.BladeRotationSpeed, SurvivorSkillParameter.OrbitHeight, SurvivorSkillParameter.HitInterval, SurvivorSkillParameter.Cooldown },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.Damage, SurvivorSkillParameter.Knockback, SurvivorSkillParameter.KnockbackDecay, SurvivorSkillParameter.HeavyKnockbackMultiplier },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.Damage, SurvivorSkillParameter.Duration, SurvivorSkillParameter.TickInterval }
        };
        public static SurvivorSkillParameter[] Parameters(SkillKind kind) => ParameterSets[(int)kind];
        public static bool IsCount(SurvivorSkillParameter parameter) => parameter is SurvivorSkillParameter.ProjectileCount or SurvivorSkillParameter.VolleyCount or SurvivorSkillParameter.OrbiterCount;
        public static string FieldName(SurvivorSkillParameter parameter) { var name = parameter.ToString(); return char.ToLowerInvariant(name[0]) + name.Substring(1); }
        public static string Label(SurvivorSkillParameter parameter) => parameter switch
        {
            SurvivorSkillParameter.Cooldown => "冷却（秒）", SurvivorSkillParameter.Radius => "半径 / 射程",
            SurvivorSkillParameter.Damage => "每次命中伤害", SurvivorSkillParameter.ProjectileCount => "每轮子弹数量",
            SurvivorSkillParameter.VolleyCount => "连续发射轮数", SurvivorSkillParameter.VolleyInterval => "连发间隔（秒）",
            SurvivorSkillParameter.ProjectileSpeed => "子弹速度", SurvivorSkillParameter.ProjectileLifetime => "子弹存活时间（秒）",
            SurvivorSkillParameter.ProjectileSpread => "子弹间夹角（度）", SurvivorSkillParameter.OrbiterCount => "匕首数量",
            SurvivorSkillParameter.OrbitSpeed => "环绕速度（弧度/秒）", SurvivorSkillParameter.BladeRotationSpeed => "刀刃自转速度（度/秒）",
            SurvivorSkillParameter.OrbitHeight => "匕首高度", SurvivorSkillParameter.HitInterval => "同一敌人的命中间隔（秒）",
            SurvivorSkillParameter.Duration => "区域持续时间（秒）", SurvivorSkillParameter.TickInterval => "区域伤害间隔（秒）",
            SurvivorSkillParameter.Knockback => "击退初速度", SurvivorSkillParameter.KnockbackDecay => "击退衰减速度",
            SurvivorSkillParameter.HeavyKnockbackMultiplier => "重型敌人击退倍率", _ => parameter.ToString()
        };

        public static SurvivorSkillCatalog Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("缺少技能配置：" + AssetPath);
            return Parse(asset.text);
        }
        public static SurvivorSkillCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<SurvivorSkillCatalog>(json);
            if (catalog == null) throw new ArgumentException("技能 JSON 不能为空。");
            catalog.Rebuild();
            return catalog;
        }
        public static void ValidateStats(SkillKind kind, SurvivorSkillStats stats)
        {
            foreach (var parameter in Parameters(kind)) ValidateValue(parameter, stats.Get(parameter));
            if (!ColorUtility.TryParseHtmlString(stats.effectColor, out stats.color)) throw new ArgumentException(kind + " 的特效颜色必须是有效的 HTML 颜色，例如 #FFFFFF。");
        }
        public static void ValidateValue(SurvivorSkillParameter parameter, float value)
        {
            var minimum = parameter is SurvivorSkillParameter.Damage or SurvivorSkillParameter.ProjectileSpread or SurvivorSkillParameter.OrbitSpeed or SurvivorSkillParameter.BladeRotationSpeed or SurvivorSkillParameter.OrbitHeight or SurvivorSkillParameter.Knockback or SurvivorSkillParameter.HeavyKnockbackMultiplier ? 0 : 0.01f;
            var maximum = parameter == SurvivorSkillParameter.HeavyKnockbackMultiplier ? 1 : IsCount(parameter) ? 64 : 100000;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum || (IsCount(parameter) && (value < 1 || value != Mathf.Round(value))))
                throw new ArgumentException(Label(parameter) + " 的值无效：" + value);
        }
        public static void ValidateDebuffs(SurvivorDebuffConfig[] debuffs)
        {
            var seen = new HashSet<string>();
            foreach (var debuff in debuffs ?? Array.Empty<SurvivorDebuffConfig>())
            {
                if (debuff == null || Array.IndexOf(DebuffTypes, debuff.type) < 0 || !seen.Add(debuff.type)) throw new ArgumentException("Debuff 类型必须唯一，且为 Freeze、Slow 或 Bleed。");
                if (!debuff.enabled) continue;
                if (!SurvivorArena.IsFinite(debuff.duration) || debuff.duration <= 0 || debuff.duration > 120) throw new ArgumentException("Debuff 持续时间必须在 0–120 秒内。");
                if (debuff.type == "Bleed" && (!SurvivorArena.IsFinite(debuff.tickInterval) || debuff.tickInterval < 0.01f || !SurvivorArena.IsFinite(debuff.currentHealthPercent) || debuff.currentHealthPercent <= 0 || debuff.currentHealthPercent > 1)) throw new ArgumentException("流血间隔至少 0.01 秒，当前生命比例必须大于 0 且不超过 100%。");
                if (debuff.type == "Slow" && (!SurvivorArena.IsFinite(debuff.moveSpeedMultiplier) || debuff.moveSpeedMultiplier < 0 || debuff.moveSpeedMultiplier > 1)) throw new ArgumentException("减速后的移动倍率必须在 0–1 之间。");
            }
        }
    }
}
