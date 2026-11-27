using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    [Serializable]
    public sealed class SurvivorEnemySkillDefinition
    {
        public string id = "WarriorSlash";
        public string displayName = "近战斩击";
        public string description = "对前方玩家造成伤害。";
        public bool enabled = true;
        public string behavior = "Strike";
        public float cooldown = 3;
        public float initialCooldown = 1.5f;
        public float initialCooldownJitter = 0.5f;
        public float castRange = 2;
        public float windup = 0.35f;
        public float damage = 8;
        public float radius = 2;
        public float arcAngle = 110;
        public float duration = 2;
        public float chargeSpeed = 10;
        public int projectileCount = 1;
        public float projectileSpread = 15;
        public float projectileSpeed = 9;
        public float projectileLifetime = 4;
        public float projectileRadius = 0.2f;
        public float damageReduction = 0.5f;
        public float moveSpeedMultiplier = 1.5f;
        public float healPercent = 0.15f;
        public float healThreshold = 0.7f;
        public string effectColor = "#FF795D";
        [NonSerialized] public Color color;
    }

    [Serializable]
    public sealed class SurvivorEnemySkillCatalog
    {
        public int version = 1;
        public int projectileLimit = 320;
        public SurvivorEnemySkillDefinition[] skills;
        [NonSerialized] private Dictionary<string, SurvivorEnemySkillDefinition> _byId;

        public SurvivorEnemySkillDefinition Get(string id)
        {
            if (_byId == null) Rebuild();
            if (id == null || !_byId.TryGetValue(id, out var definition)) throw new ArgumentException("未知怪物技能 ID: " + id);
            return definition;
        }

        public bool Contains(string id)
        {
            if (_byId == null) Rebuild();
            return id != null && _byId.ContainsKey(id);
        }

        public void Rebuild()
        {
            if (version != 1 || skills == null || skills.Length == 0 || skills.Length > 256)
                throw new ArgumentException("怪物技能版本必须为 1, 并包含 1–256 项技能。");
            SurvivorConfiguration.ValidateNumber(projectileLimit, 1, 1024, "怪物投射物上限");
            var byId = new Dictionary<string, SurvivorEnemySkillDefinition>(StringComparer.Ordinal);
            foreach (var skill in skills)
            {
                if (skill == null) throw new ArgumentException("怪物技能项不能为空。");
                SurvivorConfiguration.ValidateEnemyId(skill.id);
                if (byId.ContainsKey(skill.id) || string.IsNullOrWhiteSpace(skill.displayName)) throw new ArgumentException("怪物技能 ID 必须唯一且名称不能为空: " + skill.id);
                ValidateDefinition(skill);
                byId.Add(skill.id, skill);
            }
            _byId = byId;
        }

        private static void ValidateDefinition(SurvivorEnemySkillDefinition skill)
        {
            if (Array.IndexOf(SurvivorEnemySkills.behaviors, skill.behavior) < 0) throw new ArgumentException("未知怪物技能行为: " + skill.behavior);
            SurvivorConfiguration.ValidateNumber(skill.cooldown, 0.1f, 3600, "技能冷却");
            SurvivorConfiguration.ValidateNumber(skill.initialCooldown, 0, 3600, "初始冷却");
            SurvivorConfiguration.ValidateNumber(skill.initialCooldownJitter, 0, 3600, "初始冷却随机浮动");
            SurvivorConfiguration.ValidateNumber(skill.castRange, 0, 60, "施放距离");
            SurvivorConfiguration.ValidateNumber(skill.windup, 0, 10, "施法前摇");
            SurvivorConfiguration.ValidateNumber(skill.damage, 0, 10000, "技能伤害");
            SurvivorConfiguration.ValidateNumber(skill.radius, 0.05f, 30, "技能范围");
            SurvivorConfiguration.ValidateNumber(skill.arcAngle, 1, 360, "攻击扇形角度");
            SurvivorConfiguration.ValidateNumber(skill.duration, 0.05f, 60, "持续时间");
            SurvivorConfiguration.ValidateNumber(skill.chargeSpeed, 0.1f, 40, "冲锋速度");
            SurvivorConfiguration.ValidateNumber(skill.projectileCount, 1, 16, "投射物数量");
            SurvivorConfiguration.ValidateNumber(skill.projectileSpread, 0, 180, "投射物夹角");
            SurvivorConfiguration.ValidateNumber(skill.projectileSpeed, 0.1f, 40, "投射物速度");
            SurvivorConfiguration.ValidateNumber(skill.projectileLifetime, 0.1f, 20, "投射物寿命");
            SurvivorConfiguration.ValidateNumber(skill.projectileRadius, 0.05f, 3, "投射物半径");
            SurvivorConfiguration.ValidateNumber(skill.damageReduction, 0, 0.95f, "减伤比例");
            SurvivorConfiguration.ValidateNumber(skill.moveSpeedMultiplier, 1, 5, "加速倍率");
            SurvivorConfiguration.ValidateNumber(skill.healPercent, 0, 1, "回复生命比例");
            SurvivorConfiguration.ValidateNumber(skill.healThreshold, 0, 1, "治疗触发生命比例");
            if (!ColorUtility.TryParseHtmlString(skill.effectColor, out skill.color)) throw new ArgumentException("怪物技能颜色无效: " + skill.id);
        }
    }

    public static class SurvivorEnemySkills
    {
        public const string ASSET_PATH = SurvivorConfiguration.ASSET_DIRECTORY + "/EnemySkills.json";
        public static readonly string[] behaviors = { "Strike", "Shockwave", "Charge", "Projectile", "Volley", "Bombard", "Guard", "Haste", "Heal" };

        public static SurvivorEnemySkillCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<SurvivorEnemySkillCatalog>(json);
            if (catalog == null) throw new ArgumentException("怪物技能 JSON 不能为空。");
            catalog.Rebuild();
            return catalog;
        }
    }
}
