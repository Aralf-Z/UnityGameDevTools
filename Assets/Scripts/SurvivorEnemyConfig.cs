using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    [Serializable]
    public sealed class SurvivorEnemyDefinition
    {
        public string id = "Grunt";
        public string displayName = "普通敌人";
        public string archetype = "Warrior";
        public int strength = 1;
        public float health = 38;
        /// <summary> 以基础生命为基数, 每波线性增加的比例. </summary>
        public float healthGrowthPerWave = 0.18f;
        public float moveSpeed = 2.7f;
        public float contactDamage = 8;
        public float contactInterval = 1;
        public int experience = 1;
        public float radius = 0.45f;
        public string bodyColor = "#F28673";
        public string shape = "Capsule";
        public Vector3 scaleMin = new(0.64f, 0.73f, 0.64f);
        public Vector3 scaleMax = new(0.76f, 0.87f, 0.76f);
        public bool heavy;
        public float preferredDistance;
        public float skillDamageMultiplier = 1;
        public string[] skills = { "WarriorSlash" };
        [NonSerialized] public Color color;
        [NonSerialized] public PrimitiveType primitive;

        public float HealthAtWave(int wave) => health * (1 + (Mathf.Max(1, wave) - 1) * healthGrowthPerWave);
    }

    [Serializable]
    public sealed class SurvivorEnemyCatalog
    {
        public int version = 3;
        public SurvivorEnemyDefinition[] enemies;
        [NonSerialized] private Dictionary<string, SurvivorEnemyDefinition> _byId;

        public SurvivorEnemyDefinition Get(string id)
        {
            if (_byId == null) Rebuild();
            if (id == null || !_byId.TryGetValue(id, out var definition)) throw new ArgumentException("未知的怪物 ID: " + id);
            return definition;
        }

        public SurvivorEnemyDefinition Get(EnemyKind kind) => Get(kind.ToString());

        public bool Contains(string id)
        {
            if (_byId == null) Rebuild();
            return id != null && _byId.ContainsKey(id);
        }

        public void Rebuild()
        {
            if (version != 3 || enemies == null || enemies.Length == 0 || enemies.Length > 256)
                throw new ArgumentException("怪物配置版本必须为 3, 并包含 1–256 种怪物。");
            var byId = new Dictionary<string, SurvivorEnemyDefinition>(StringComparer.Ordinal);
            foreach (var enemy in enemies)
            {
                if (enemy == null) throw new ArgumentException("敌人配置项不能为空。");
                SurvivorConfiguration.ValidateEnemyId(enemy.id);
                if (byId.ContainsKey(enemy.id)) throw new ArgumentException("重复的怪物 ID: " + enemy.id);
                if (string.IsNullOrWhiteSpace(enemy.displayName)) throw new ArgumentException(enemy.id + " 缺少名称。");
                if (Array.IndexOf(SurvivorEnemies.archetypes, enemy.archetype) < 0) throw new ArgumentException("未知怪物系别: " + enemy.archetype);
                SurvivorConfiguration.ValidateNumber(enemy.strength, 1, 3, "怪物强度");
                SurvivorConfiguration.ValidateNumber(enemy.health, 0.01f, 1000000, "基础生命");
                SurvivorConfiguration.ValidateNumber(enemy.healthGrowthPerWave, 0, 100, "每波生命增长");
                SurvivorConfiguration.ValidateNumber(enemy.moveSpeed, 0, 100, "移动速度");
                SurvivorConfiguration.ValidateNumber(enemy.contactDamage, 0, 10000, "接触伤害");
                SurvivorConfiguration.ValidateNumber(enemy.contactInterval, 0.01f, 3600, "接触间隔");
                SurvivorConfiguration.ValidateNumber(enemy.experience, 0, 100000, "掉落经验");
                SurvivorConfiguration.ValidateNumber(enemy.radius, 0.05f, 5, "碰撞半径");
                if (!ColorUtility.TryParseHtmlString(enemy.bodyColor, out enemy.color))
                    throw new ArgumentException(enemy.id + " 的外观颜色必须是有效的 HTML 颜色。");
                if (Array.IndexOf(SurvivorEnemies.shapes, enemy.shape) < 0 || !Enum.TryParse(enemy.shape, out enemy.primitive))
                    throw new ArgumentException(enemy.id + " 的形状必须为 Cube、Sphere、Capsule 或 Cylinder。");
                ValidateScaleRange(enemy);
                SurvivorConfiguration.ValidateNumber(enemy.preferredDistance, 0, 25, "怪物保持距离");
                SurvivorConfiguration.ValidateNumber(enemy.skillDamageMultiplier, 0, 100, "怪物技能伤害倍率");
                if (enemy.skills == null || enemy.skills.Length < 1 || enemy.skills.Length > 2) throw new ArgumentException(enemy.id + " 必须配置 1–2 个技能。");
                var ids = new HashSet<string>();
                foreach (var id in enemy.skills)
                {
                    SurvivorConfiguration.ValidateEnemyId(id);
                    if (!ids.Add(id)) throw new ArgumentException(enemy.id + " 重复配置技能: " + id);
                }
                byId.Add(enemy.id, enemy);
            }
            _byId = byId;
        }

        public void ValidateSkills(SurvivorEnemySkillCatalog catalog)
        {
            foreach (var enemy in enemies)
                foreach (var id in enemy.skills) catalog.Get(id);
        }

        private static void ValidateScaleRange(SurvivorEnemyDefinition enemy)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                SurvivorConfiguration.ValidateNumber(enemy.scaleMin[axis], 0.01f, 20, "怪物最小 Scale");
                SurvivorConfiguration.ValidateNumber(enemy.scaleMax[axis], enemy.scaleMin[axis], 20, "怪物最大 Scale");
            }
        }
    }

    public static class SurvivorEnemies
    {
        public const string ASSET_PATH = SurvivorConfiguration.ASSET_DIRECTORY + "/Enemies.json";
        public static readonly string[] shapes = { "Cube", "Sphere", "Capsule", "Cylinder" };
        public static readonly string[] archetypes = { "Tank", "Warrior", "Agile", "Ranged" };

        public static SurvivorEnemyCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<SurvivorEnemyCatalog>(json);
            if (catalog == null) throw new ArgumentException("敌人 JSON 不能为空。");
            catalog.Rebuild();
            return catalog;
        }
    }
}
