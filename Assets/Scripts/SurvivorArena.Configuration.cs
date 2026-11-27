using System;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private string _skillConfigurationJson;
        private string _waveConfigurationJson, _enemyConfigurationJson;
        private string _levelConfigurationJson;
        private string _enemySkillConfigurationJson;

        private void SetConfiguration(SurvivorConfiguration configuration)
        {
            _skills = configuration.skills;
            _waves = configuration.waves;
            _enemyCatalog = configuration.enemies;
            _levels = configuration.levels;
            _enemySkills = configuration.enemySkills;
            _skillConfigurationJson = JsonUtility.ToJson(_skills);
            _waveConfigurationJson = JsonUtility.ToJson(_waves);
            _enemyConfigurationJson = JsonUtility.ToJson(_enemyCatalog);
            _levelConfigurationJson = JsonUtility.ToJson(_levels);
            _enemySkillConfigurationJson = JsonUtility.ToJson(_enemySkills);
        }

        private void SpawnGroups(SurvivorSpawnGroup[] groups)
        {
            foreach (var group in groups ?? Array.Empty<SurvivorSpawnGroup>())
                Spawn(group.enemy, group.count);
        }

        private float RescaleDeadline(float deadline, float oldInterval, float newInterval)
        {
            var fraction = oldInterval > 0 ? Mathf.Clamp01((deadline - Stats.Elapsed) / oldInterval) : 0;
            return Stats.Elapsed + fraction * newInterval;
        }

        private void RefreshFrostConfiguration(SurvivorSkillStats stats)
        {
            for (var i = _frostZones.Count - 1; i >= 0; i--)
            {
                var zone = _frostZones[i];
                zone.expiresAt = zone.createdAt + stats.duration;
                if (SkillLevel(SkillKind.Frost) == 0 || zone.expiresAt <= Stats.Elapsed)
                {
                    RecycleRing(zone.outer);
                    RecycleRing(zone.middle);
                    RecycleRing(zone.inner);
                    _frostZones.RemoveAt(i);
                    continue;
                }
                zone.nextDamageAt = RescaleDeadline(zone.nextDamageAt, zone.tickInterval, stats.tickInterval);
                zone.radius = stats.radius;
                zone.damageMultiplier = stats.damageMultiplier;
                zone.tickInterval = stats.tickInterval;
                zone.debuffs = stats.debuffs;
                zone.outer = RefreshFrostRing(zone.outer, zone, stats, 1, 0.8f);
                zone.middle = RefreshFrostRing(zone.middle, zone, stats, 0.66f, 0.3f);
                zone.inner = RefreshFrostRing(zone.inner, zone, stats, 0.33f, 0.2f);
                _frostZones[i] = zone;
            }
        }

        private RingEffect RefreshFrostRing(RingEffect ring, FrostZone zone, SurvivorSkillStats stats, float radiusMultiplier, float alpha)
        {
            var radius = stats.radius * radiusMultiplier;
            var color = SurvivorPalette.Alpha(stats.color, alpha);
            if (ring == null || !_effects.Contains(ring)) ring = Burst(zone.position, radius, color, zone.expiresAt - Stats.Elapsed, true);
            if (ring == null) return null;
            ring.radius = radius;
            ring.color = color;
            ring.duration = stats.duration;
            ring.expiresAt = zone.expiresAt;
            ring.life = zone.expiresAt - Stats.Elapsed;
            ring.line.transform.localScale = new Vector3(radius, 1, radius);
            ring.line.startColor = ring.line.endColor = color;
            return ring;
        }

        private static SurvivorDebuffConfig FindDebuff(SurvivorSkillStats stats, string type)
        {
            foreach (var debuff in stats.debuffs) if (debuff.enabled && debuff.type == type) return debuff;
            return null;
        }

        private float RefreshDebuffDeadline(float deadline, SkillKind source, string type, SurvivorSkillStats[] oldStats, SurvivorSkillStats[] newStats)
        {
            if (deadline <= Stats.Elapsed || source == SkillKind.Frost || SkillLevel(source) == 0) return 0;
            var oldDebuff = FindDebuff(oldStats[(int)source], type);
            var next = FindDebuff(newStats[(int)source], type);
            return next == null ? 0 : RescaleDeadline(deadline, oldDebuff?.duration ?? next.duration, next.duration);
        }

        private void RefreshEnemyDebuffs(Enemy enemy, SurvivorSkillStats[] oldStats, SurvivorSkillStats[] newStats)
        {
            enemy.frozenUntil = RefreshDebuffDeadline(enemy.frozenUntil, enemy.freezeSource, "Freeze", oldStats, newStats);
            enemy.slowUntil = RefreshDebuffDeadline(enemy.slowUntil, enemy.slowSource, "Slow", oldStats, newStats);
            if (enemy.slowUntil > Stats.Elapsed) enemy.slowMultiplier = FindDebuff(newStats[(int)enemy.slowSource], "Slow").moveSpeedMultiplier;
            enemy.bleedUntil = RefreshDebuffDeadline(enemy.bleedUntil, enemy.bleedSource, "Bleed", oldStats, newStats);
            if (enemy.bleedUntil > Stats.Elapsed)
            {
                var bleed = FindDebuff(newStats[(int)enemy.bleedSource], "Bleed");
                enemy.nextBleedAt = RescaleDeadline(enemy.nextBleedAt, enemy.bleedInterval, bleed.tickInterval);
                enemy.bleedInterval = bleed.tickInterval;
                enemy.bleedPercent = bleed.currentHealthPercent;
            }
            else enemy.nextBleedAt = 0;
        }

        private void RefreshPresetIconColors()
        {
            foreach (var pair in _presetIcons)
            {
                var texture = pair.Value.texture;
                Color color;
                if (Enum.TryParse(pair.Key, out SkillKind kind) && Enum.IsDefined(typeof(SkillKind), kind)) color = _skills.Resolve(kind, 0).color;
                else if (_enemyCatalog.Contains(pair.Key)) color = _enemyCatalog.Get(pair.Key).color;
                else continue;
                for (var y = 2; y < 10; y++) for (var x = 2; x < 10; x++) texture.SetPixel(x, y, color);
                texture.Apply();
            }
        }
    }
}
