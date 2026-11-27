using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    [Serializable]
    public sealed class SurvivorSpawnGroup
    {
        public string enemy = "Grunt";
        public int count = 1;
    }

    [Serializable]
    public sealed class SurvivorWaveDefinition
    {
        public string displayName = "新波次";
        public float startTime;
        public float spawnInterval = 2;
        public int spawnCount = 3;
        public string[] enemyPool = { "Grunt", "Runner" };
        public SurvivorSpawnGroup[] entrySpawns = Array.Empty<SurvivorSpawnGroup>();
    }

    [Serializable]
    public sealed class SurvivorWaveCatalog
    {
        public int version = 1;
        public float runDuration = 180;
        public int enemyLimit = 180;
        public float firstSpawnDelay = 5;
        public float spawnDistanceMin = 12;
        public float spawnDistanceMax = 19;
        public SurvivorSpawnGroup[] initialSpawns = { new() { enemy = "Grunt", count = 12 } };
        public SurvivorWaveDefinition[] waves;

        /// <summary> 波次按数组顺序编号, 最后一波持续到对局结束. </summary>
        public int IndexAt(float elapsed)
        {
            var index = 0;
            while (index + 1 < waves.Length && elapsed >= waves[index + 1].startTime) index++;
            return index;
        }

        public void Rebuild()
        {
            if (version != 1 || waves == null || waves.Length == 0)
                throw new ArgumentException("波次配置版本必须为 1, 且至少包含一波。");
            SurvivorConfiguration.ValidateNumber(runDuration, 0.1f, 86400, "对局时长");
            SurvivorConfiguration.ValidateNumber(enemyLimit, 1, 2000, "敌人上限");
            SurvivorConfiguration.ValidateNumber(firstSpawnDelay, 0, 86400, "首次自动生成延迟");
            SurvivorConfiguration.ValidateNumber(spawnDistanceMin, 0, SurvivorArena.ArenaRadius * 2, "最小生成距离");
            SurvivorConfiguration.ValidateNumber(spawnDistanceMax, spawnDistanceMin, SurvivorArena.ArenaRadius * 2, "最大生成距离");
            ValidateSpawns(initialSpawns);
            var previousTime = -1f;
            foreach (var wave in waves)
            {
                if (wave == null || string.IsNullOrWhiteSpace(wave.displayName)) throw new ArgumentException("波次缺少名称。");
                SurvivorConfiguration.ValidateNumber(wave.startTime, 0, runDuration, "波次开始时间");
                if (wave.startTime <= previousTime) throw new ArgumentException("波次开始时间必须严格递增。");
                SurvivorConfiguration.ValidateNumber(wave.spawnInterval, 0.05f, 3600, "生成间隔");
                SurvivorConfiguration.ValidateNumber(wave.spawnCount, 0, 2000, "每批生成数量");
                if (wave.enemyPool == null || wave.enemyPool.Length == 0) throw new ArgumentException(wave.displayName + " 的敌人池不能为空。");
                var kinds = new HashSet<string>();
                foreach (var enemy in wave.enemyPool)
                {
                    SurvivorConfiguration.ValidateEnemyId(enemy);
                    if (!kinds.Add(enemy)) throw new ArgumentException(wave.displayName + " 的敌人池包含重复项: " + enemy);
                }
                ValidateSpawns(wave.entrySpawns);
                previousTime = wave.startTime;
            }
            if (waves[0].startTime != 0) throw new ArgumentException("第一波必须从 0 秒开始。");
        }

        private static void ValidateSpawns(SurvivorSpawnGroup[] groups)
        {
            foreach (var group in groups ?? Array.Empty<SurvivorSpawnGroup>())
            {
                if (group == null) throw new ArgumentException("生成组不能为空。");
                SurvivorConfiguration.ValidateEnemyId(group.enemy);
                SurvivorConfiguration.ValidateNumber(group.count, 0, 2000, "生成组数量");
            }
        }

        public void ValidateEnemies(SurvivorEnemyCatalog catalog)
        {
            foreach (var group in initialSpawns ?? Array.Empty<SurvivorSpawnGroup>()) catalog.Get(group.enemy);
            foreach (var wave in waves)
            {
                foreach (var id in wave.enemyPool) catalog.Get(id);
                foreach (var group in wave.entrySpawns ?? Array.Empty<SurvivorSpawnGroup>()) catalog.Get(group.enemy);
            }
        }
    }

    public static class SurvivorWaves
    {
        public const string ASSET_PATH = SurvivorConfiguration.ASSET_DIRECTORY + "/Waves.json";

        public static SurvivorWaveCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<SurvivorWaveCatalog>(json);
            if (catalog == null) throw new ArgumentException("波次 JSON 不能为空。");
            catalog.Rebuild();
            return catalog;
        }
    }
}
