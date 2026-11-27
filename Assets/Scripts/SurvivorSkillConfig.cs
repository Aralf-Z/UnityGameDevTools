using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public enum SurvivorSkillParameter
    {
        Cooldown = 0, Radius = 1, DamageMultiplier = 2, ProjectileCount = 3, VolleyCount = 4, VolleyInterval = 5,
        ProjectileSpeed = 6, ProjectileLifetime = 7, ProjectileSpread = 8, OrbiterCount = 9,
        OrbitSpeed = 10, BladeRotationSpeed = 11, OrbitHeight = 12, HitInterval = 13, Duration = 14,
        TickInterval = 15, Knockback = 16, KnockbackDecay = 17, HeavyKnockbackMultiplier = 18,
        DashSpeedMultiplier = 19, InvulnerabilityDuration = 20, Healing = 21, TargetRange = 22
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
        public float cooldown = 1, radius = 1, damageMultiplier;
        public int projectileCount = 1, volleyCount = 1, orbiterCount = 3;
        public float volleyInterval = 0.12f, projectileSpeed = 18, projectileLifetime = 1.6f, projectileSpread = 11;
        public float orbitSpeed = 2.7f, bladeRotationSpeed = 140, orbitHeight = 0.85f, hitInterval = 0.4f;
        public float duration = 2, tickInterval = 0.5f, knockback, knockbackDecay = 6, heavyKnockbackMultiplier = 0.55f;
        public float dashSpeedMultiplier = 3.8f, invulnerabilityDuration = 0.24f, healing;
        public float targetRange = 18;
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
            SurvivorSkillParameter.DamageMultiplier => damageMultiplier, SurvivorSkillParameter.ProjectileCount => projectileCount,
            SurvivorSkillParameter.VolleyCount => volleyCount, SurvivorSkillParameter.VolleyInterval => volleyInterval,
            SurvivorSkillParameter.ProjectileSpeed => projectileSpeed, SurvivorSkillParameter.ProjectileLifetime => projectileLifetime,
            SurvivorSkillParameter.ProjectileSpread => projectileSpread, SurvivorSkillParameter.OrbiterCount => orbiterCount,
            SurvivorSkillParameter.OrbitSpeed => orbitSpeed, SurvivorSkillParameter.BladeRotationSpeed => bladeRotationSpeed,
            SurvivorSkillParameter.OrbitHeight => orbitHeight, SurvivorSkillParameter.HitInterval => hitInterval,
            SurvivorSkillParameter.Duration => duration, SurvivorSkillParameter.TickInterval => tickInterval,
            SurvivorSkillParameter.Knockback => knockback, SurvivorSkillParameter.KnockbackDecay => knockbackDecay,
            SurvivorSkillParameter.HeavyKnockbackMultiplier => heavyKnockbackMultiplier,
            SurvivorSkillParameter.DashSpeedMultiplier => dashSpeedMultiplier,
            SurvivorSkillParameter.InvulnerabilityDuration => invulnerabilityDuration,
            SurvivorSkillParameter.TargetRange => targetRange,
            SurvivorSkillParameter.Healing => healing, _ => 0
        };

        public void Set(SurvivorSkillParameter parameter, float value)
        {
            switch (parameter)
            {
                case SurvivorSkillParameter.Cooldown: cooldown = value; break;
                case SurvivorSkillParameter.Radius: radius = value; break;
                case SurvivorSkillParameter.DamageMultiplier: damageMultiplier = value; break;
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
                case SurvivorSkillParameter.DashSpeedMultiplier: dashSpeedMultiplier = value; break;
                case SurvivorSkillParameter.InvulnerabilityDuration: invulnerabilityDuration = value; break;
                case SurvivorSkillParameter.Healing: healing = value; break;
                case SurvivorSkillParameter.TargetRange: targetRange = value; break;
            }
        }
    }

    [Serializable]
    public sealed class SurvivorSkillValueOverride
    {
        public string parameter = "DamageMultiplier";
        public string operation = "Set";
        public float value;
    }

    public static class SurvivorSkills
    {
        public const int COMBAT_SKILL_COUNT = 8;
        public const int SKILL_COUNT = 9;
        public const int MODIFICATION_COUNT = 5;
        public const int ALL_MODIFICATIONS = (1 << MODIFICATION_COUNT) - 1;
        public const string AssetPath = SurvivorConfiguration.ASSET_DIRECTORY + "/Skills.json";
        public static readonly string[] DebuffTypes = { "Freeze", "Slow", "Bleed" };
        private static readonly SurvivorSkillParameter[][] ParameterSets =
        {
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.ProjectileCount, SurvivorSkillParameter.VolleyCount, SurvivorSkillParameter.VolleyInterval, SurvivorSkillParameter.ProjectileSpeed, SurvivorSkillParameter.ProjectileLifetime, SurvivorSkillParameter.ProjectileSpread },
            new[] { SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.OrbiterCount, SurvivorSkillParameter.OrbitSpeed, SurvivorSkillParameter.BladeRotationSpeed, SurvivorSkillParameter.OrbitHeight, SurvivorSkillParameter.HitInterval, SurvivorSkillParameter.Cooldown },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.Knockback, SurvivorSkillParameter.KnockbackDecay, SurvivorSkillParameter.HeavyKnockbackMultiplier },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.Duration, SurvivorSkillParameter.TickInterval },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.ProjectileCount },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.Duration, SurvivorSkillParameter.TickInterval },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.TargetRange, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.ProjectileCount, SurvivorSkillParameter.ProjectileSpeed, SurvivorSkillParameter.ProjectileLifetime, SurvivorSkillParameter.ProjectileSpread },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.TargetRange, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier, SurvivorSkillParameter.Duration, SurvivorSkillParameter.TickInterval },
            new[] { SurvivorSkillParameter.Cooldown, SurvivorSkillParameter.Duration, SurvivorSkillParameter.DashSpeedMultiplier, SurvivorSkillParameter.InvulnerabilityDuration, SurvivorSkillParameter.Healing, SurvivorSkillParameter.Radius, SurvivorSkillParameter.DamageMultiplier }
        };
        public static SurvivorSkillParameter[] Parameters(SkillKind kind) => ParameterSets[(int)kind];
        public static bool IsCount(SurvivorSkillParameter parameter) => parameter is SurvivorSkillParameter.ProjectileCount or SurvivorSkillParameter.VolleyCount or SurvivorSkillParameter.OrbiterCount;
        public static string FieldName(SurvivorSkillParameter parameter) { var name = parameter.ToString(); return char.ToLowerInvariant(name[0]) + name.Substring(1); }
        public static string Label(SurvivorSkillParameter parameter) => parameter switch
        {
            SurvivorSkillParameter.Cooldown => "冷却（秒）", SurvivorSkillParameter.Radius => "半径 / 射程",
            SurvivorSkillParameter.DamageMultiplier => "基础伤害倍率（倍）", SurvivorSkillParameter.ProjectileCount => "每轮子弹数量",
            SurvivorSkillParameter.VolleyCount => "连续发射轮数", SurvivorSkillParameter.VolleyInterval => "连发间隔（秒）",
            SurvivorSkillParameter.ProjectileSpeed => "子弹速度", SurvivorSkillParameter.ProjectileLifetime => "子弹存活时间（秒）",
            SurvivorSkillParameter.ProjectileSpread => "子弹间夹角（度）", SurvivorSkillParameter.OrbiterCount => "匕首数量",
            SurvivorSkillParameter.OrbitSpeed => "环绕速度（弧度/秒）", SurvivorSkillParameter.BladeRotationSpeed => "刀刃自转速度（度/秒）",
            SurvivorSkillParameter.OrbitHeight => "匕首高度", SurvivorSkillParameter.HitInterval => "同一敌人的命中间隔（秒）",
            SurvivorSkillParameter.Duration => "区域持续时间（秒）", SurvivorSkillParameter.TickInterval => "区域伤害间隔（秒）",
            SurvivorSkillParameter.Knockback => "击退初速度", SurvivorSkillParameter.KnockbackDecay => "击退衰减速度",
            SurvivorSkillParameter.HeavyKnockbackMultiplier => "重型敌人击退倍率",
            SurvivorSkillParameter.DashSpeedMultiplier => "冲刺速度倍率",
            SurvivorSkillParameter.InvulnerabilityDuration => "冲刺无敌时间（秒）",
            SurvivorSkillParameter.Healing => "冲刺回复生命", SurvivorSkillParameter.TargetRange => "自动索敌距离", _ => parameter.ToString()
        };

        public static SurvivorSkillCatalog Load()
        {
            return Parse(SurvivorConfiguration.ReadJson("Skills.json"));
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
            var minimum = parameter is SurvivorSkillParameter.DamageMultiplier or SurvivorSkillParameter.ProjectileSpread or SurvivorSkillParameter.OrbitSpeed or SurvivorSkillParameter.BladeRotationSpeed or SurvivorSkillParameter.OrbitHeight or SurvivorSkillParameter.Knockback or SurvivorSkillParameter.HeavyKnockbackMultiplier or SurvivorSkillParameter.InvulnerabilityDuration or SurvivorSkillParameter.Healing ? 0 : 0.01f;
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
