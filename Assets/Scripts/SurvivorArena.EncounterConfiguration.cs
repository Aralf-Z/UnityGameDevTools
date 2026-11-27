using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        /// <summary> 怪物与波次一起校验和应用, 支持同次保存中新建或移除怪物引用. </summary>
        public bool ApplyEncounterConfiguration(string waveJson, string enemyJson, string enemySkillJson)
        {
            if (!_initialized) return false;
            var waves = SurvivorWaves.Parse(waveJson);
            var enemies = SurvivorEnemies.Parse(enemyJson);
            var skills = SurvivorEnemySkills.Parse(enemySkillJson);
            waves.ValidateEnemies(enemies);
            enemies.ValidateSkills(skills);
            var previous = _enemySkills;
            var skillJson = JsonUtility.ToJson(skills);
            var skillsChanged = skillJson != _enemySkillConfigurationJson;
            _enemySkills = skills;
            var changed = ApplyWaveCatalog(waves);
            var enemiesChanged = ApplyEnemyCatalog(enemies);
            if (enemiesChanged || skillsChanged) RefreshEnemyAbilities(previous);
            _enemySkillConfigurationJson = skillJson;
            return changed || enemiesChanged || skillsChanged;
        }

        public bool ApplyWaveConfiguration(string json)
        {
            if (!_initialized) return false;
            var next = SurvivorWaves.Parse(json);
            next.ValidateEnemies(_enemyCatalog);
            return ApplyWaveCatalog(next);
        }

        public bool ApplyEnemyConfiguration(string json)
        {
            if (!_initialized) return false;
            var next = SurvivorEnemies.Parse(json);
            _waves.ValidateEnemies(next);
            next.ValidateSkills(_enemySkills);
            var changed = ApplyEnemyCatalog(next);
            if (changed) RefreshEnemyAbilities(_enemySkills);
            return changed;
        }

        public bool ApplyEnemySkillConfiguration(string json)
        {
            if (!_initialized) return false;
            var next = SurvivorEnemySkills.Parse(json);
            _enemyCatalog.ValidateSkills(next);
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _enemySkillConfigurationJson) return false;
            var previous = _enemySkills;
            _enemySkills = next;
            _enemySkillConfigurationJson = canonicalJson;
            RefreshEnemyAbilities(previous);
            return true;
        }

        private void RefreshEnemyAbilities(SurvivorEnemySkillCatalog previous)
        {
            foreach (var enemy in _enemies) SynchronizeEnemySkills(enemy, _enemyCatalog.Get(enemy.id), previous, false);
            RefreshEnemyProjectiles();
        }

        private bool ApplyWaveCatalog(SurvivorWaveCatalog next)
        {
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _waveConfigurationJson) return false;
            var oldInterval = _waves.waves[_waves.IndexAt(Stats.Elapsed)].spawnInterval;
            _waves = next;
            Stats.Wave = next.IndexAt(Stats.Elapsed) + 1;
            if (!_hasAutoSpawned) _nextSpawn = next.firstSpawnDelay;
            else _nextSpawn = RescaleDeadline(_nextSpawn, oldInterval, next.waves[Stats.Wave - 1].spawnInterval);
            _waveConfigurationJson = canonicalJson;
            RefreshHud(true);
            return true;
        }

        private bool ApplyEnemyCatalog(SurvivorEnemyCatalog next)
        {
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _enemyConfigurationJson) return false;
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (!next.Contains(enemy.id))
                {
                    CancelEnemyCast(enemy);
                    Destroy(enemy.root.gameObject);
                    _enemies.RemoveAt(i);
                    continue;
                }
                var previous = _enemyCatalog.Get(enemy.id);
                var definition = next.Get(enemy.id);
                var healthRatio = Mathf.Clamp01(enemy.health / enemy.maxHealth);
                enemy.maxHealth = definition.HealthAtWave(enemy.spawnWave);
                enemy.health = enemy.maxHealth * healthRatio;
                enemy.contactAt = RescaleDeadline(enemy.contactAt, previous.contactInterval, definition.contactInterval);
                ApplyEnemyAppearance(enemy, definition);
                enemy.root.position = Constrain(enemy.root.position, enemy.radius);
            }
            foreach (var id in new List<string>(_enemyPool.Keys))
            {
                if (next.Contains(id)) continue;
                foreach (var enemy in _enemyPool[id]) Destroy(enemy.root.gameObject);
                _enemyPool.Remove(id);
            }
            _enemyCatalog = next;
            _enemyConfigurationJson = canonicalJson;
            RefreshPresetIconColors();
            return true;
        }
    }
}
