using System;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    [Serializable]
    public sealed class SurvivorLevelDefinition
    {
        public int level = 1;
        /// <summary> 到达此等级所需的累计总经验, 1 级固定为 0. </summary>
        public int totalExperience;
        public int skillRewards;
        public int modificationRewards;
        public float maxHealthGain = 8;
        /// <summary> 到达此等级增加的每秒生命恢复量. </summary>
        public float healthRegenerationGain;
        public float baseDamageGain = 0.8f;
    }

    [Serializable]
    public sealed class SurvivorLevelCatalog
    {
        public int version = 3;
        public float baseMaxHealth = 100;
        public float healthRegenerationPerSecond = 1;
        public float baseDamage = 10;
        public SurvivorLevelDefinition[] levels;

        public int MaxLevel => levels.Length;
        public SurvivorLevelDefinition Get(int level) => levels[Mathf.Clamp(level, 1, MaxLevel) - 1];

        public float MaxHealthAtLevel(int level)
        {
            var value = baseMaxHealth;
            var count = Mathf.Clamp(level, 1, MaxLevel);
            for (var i = 0; i < count; i++) value += levels[i].maxHealthGain;
            return value;
        }

        public float BaseDamageAtLevel(int level)
        {
            var value = baseDamage;
            var count = Mathf.Clamp(level, 1, MaxLevel);
            for (var i = 0; i < count; i++) value += levels[i].baseDamageGain;
            return value;
        }

        /// <summary> 基础每秒生命恢复与已到达等级的恢复增长之和. </summary>
        public float HealthRegenerationAtLevel(int level)
        {
            var value = healthRegenerationPerSecond;
            var count = Mathf.Clamp(level, 1, MaxLevel);
            for (var i = 0; i < count; i++) value += levels[i].healthRegenerationGain;
            return value;
        }

        public int LevelAtTotalExperience(int experience)
        {
            var level = 1;
            while (level < MaxLevel && experience >= Get(level + 1).totalExperience) level++;
            return level;
        }

        public void Rebuild()
        {
            if (version != 3 || levels == null || levels.Length == 0 || levels.Length > 1000)
                throw new ArgumentException("Level 配置版本必须为 3, 并包含 1–1000 个连续等级。");
            SurvivorConfiguration.ValidateNumber(baseMaxHealth, 0.01f, 10000000, "基础最大生命值");
            SurvivorConfiguration.ValidateNumber(healthRegenerationPerSecond, 0, 10000, "每秒生命恢复");
            SurvivorConfiguration.ValidateNumber(baseDamage, 0, 10000000, "基础伤害");
            for (var i = 0; i < levels.Length; i++)
            {
                var definition = levels[i];
                if (definition == null || definition.level != i + 1)
                    throw new ArgumentException("Level 必须从 1 开始按顺序连续配置。");
                if (i == 0 && definition.totalExperience != 0) throw new ArgumentException("1 级的累计经验必须为 0。");
                if (i > 0 && definition.totalExperience <= levels[i - 1].totalExperience)
                    throw new ArgumentException("累计总经验必须随等级严格递增。");
                SurvivorConfiguration.ValidateNumber(definition.skillRewards, 0, 8, "技能获取次数");
                SurvivorConfiguration.ValidateNumber(definition.modificationRewards, 0, 45, "改造获取次数");
                SurvivorConfiguration.ValidateNumber(definition.maxHealthGain, 0, 10000, "升级生命上限增长");
                SurvivorConfiguration.ValidateNumber(definition.healthRegenerationGain, 0, 10000, "每秒生命恢复增长");
                SurvivorConfiguration.ValidateNumber(definition.baseDamageGain, 0, 100000, "升级基础伤害增长");
            }
        }
    }

    public static class SurvivorLevels
    {
        public const string ASSET_PATH = SurvivorConfiguration.ASSET_DIRECTORY + "/Level.json";

        public static SurvivorLevelCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<SurvivorLevelCatalog>(json);
            if (catalog == null) throw new ArgumentException("Level JSON 不能为空。");
            catalog.Rebuild();
            return catalog;
        }
    }
}
