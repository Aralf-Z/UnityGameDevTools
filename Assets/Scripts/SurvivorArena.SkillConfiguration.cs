using System;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        public bool ApplySkillConfiguration(string json)
        {
            if (!_initialized) return false;
            var next = SurvivorSkills.Parse(json);
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _skillConfigurationJson) return false;
            var previousStats = SnapshotSkillStats();
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var oldDefinition = _skills.Get((SkillKind)i);
                var nextDefinition = next.Get((SkillKind)i);
                var mask = 0;
                for (var oldIndex = 0; oldIndex < oldDefinition.modifications.Length; oldIndex++)
                {
                    if ((_skillModifications[i] & (1 << oldIndex)) == 0) continue;
                    var id = oldDefinition.modifications[oldIndex].id;
                    var nextIndex = Array.FindIndex(nextDefinition.modifications, item => item.id == id);
                    if (nextIndex >= 0) mask |= 1 << nextIndex;
                }
                _skillModifications[i] = mask;
            }
            _skills = next;
            _skillLevels[(int)SkillKind.Dash] = 1;
            RefreshActiveSkillEffects(previousStats);
            _rewardChoices.Clear();
            EnsureRewardChoices();
            _skillConfigurationJson = canonicalJson;
            ApplyTimeScale();
            RefreshHud(true);
            return true;
        }

        public bool ApplyLevelConfiguration(string json)
        {
            if (!_initialized) return false;
            var next = SurvivorLevels.Parse(json);
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _levelConfigurationJson) return false;
            _levels = next;
            Stats.MaxHealth = next.MaxHealthAtLevel(_highestRewardLevel);
            Stats.Health = Mathf.Min(Stats.Health, Stats.MaxHealth);
            Stats.HealthRegenerationPerSecond = next.HealthRegenerationAtLevel(_highestRewardLevel);
            Stats.BaseDamage = next.BaseDamageAtLevel(_highestRewardLevel);
            Stats.Level = Mathf.Min(Stats.Level, _levels.LevelAtTotalExperience(Stats.Experience));
            Stats.ExperienceRequired = _levels.Get(Stats.Level + 1).totalExperience;
            _levelConfigurationJson = canonicalJson;
            // 已发放的选择次数保留, 修改后的奖励配置从后续升级开始使用.
            AddExperience(0);
            RefreshHud(true);
            return true;
        }

        private SurvivorSkillStats[] SnapshotSkillStats()
        {
            var result = new SurvivorSkillStats[SurvivorSkills.SKILL_COUNT];
            for (var i = 0; i < result.Length; i++) result[i] = Parameters((SkillKind)i);
            return result;
        }

        private void RefreshActiveSkillEffects(SurvivorSkillStats[] previous)
        {
            var next = SnapshotSkillStats();
            for (var i = 0; i < next.Length; i++)
                _readyAt[i] = RescaleDeadline(_readyAt[i], previous[i].cooldown, next[i].cooldown);
            var dash = (int)SkillKind.Dash;
            _dashReadyAt = RescaleDeadline(_dashReadyAt, previous[dash].cooldown, next[dash].cooldown);
            if (_dashUntil > Stats.Elapsed) _dashUntil = RescaleDeadline(_dashUntil, previous[dash].duration, next[dash].duration);
            RefreshBoltVolleys(previous[(int)SkillKind.Bolt], next[(int)SkillKind.Bolt]);
            RefreshProjectiles(next);
            RefreshFrostConfiguration(next[(int)SkillKind.Frost]);
            RefreshDamageZoneConfiguration(next);
            foreach (var enemy in _enemies)
            {
                enemy.orbitAt = RescaleDeadline(enemy.orbitAt, previous[(int)SkillKind.Orbit].hitInterval, next[(int)SkillKind.Orbit].hitInterval);
                enemy.knockbackDecay = next[(int)SkillKind.Nova].knockbackDecay;
                RefreshEnemyDebuffs(enemy, previous, next);
            }
            foreach (var zone in _frostZones)
                foreach (var enemy in _enemies)
                    if ((enemy.root.position - zone.position).sqrMagnitude <= zone.radius * zone.radius)
                        ApplyDebuffs(enemy, zone.debuffs, SkillKind.Frost, zone.expiresAt);
            ConfigureOrbiters();
            RefreshPresetIconColors();
        }

        private void RefreshBoltVolleys(SurvivorSkillStats previous, SurvivorSkillStats next)
        {
            if (_boltFollowUpAt < 0) return;
            var fired = previous.volleyCount - _boltVolleysRemaining;
            _boltVolleysRemaining = Mathf.Max(0, next.volleyCount - fired);
            _boltFollowUpAt = !HasSkill(SkillKind.Bolt) || _boltVolleysRemaining == 0
                ? -1
                : RescaleDeadline(_boltFollowUpAt, previous.volleyInterval, next.volleyInterval);
        }

        private void RefreshProjectiles(SurvivorSkillStats[] next)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                var stats = next[(int)projectile.source];
                projectile.life = stats.projectileLifetime - (Stats.Elapsed - projectile.createdAt);
                if (!HasSkill(projectile.source) || projectile.life <= 0)
                {
                    projectile.root.gameObject.SetActive(false);
                    _projectilePool.Push(projectile);
                    _projectiles.RemoveAt(i);
                    continue;
                }
                projectile.damageMultiplier = stats.damageMultiplier;
                projectile.speed = stats.projectileSpeed;
                projectile.debuffs = stats.debuffs;
                projectile.explosionRadius = projectile.source == SkillKind.Missile ? stats.radius : 0;
                projectile.body.sharedMaterial = MaterialFor(stats.color);
            }
        }

        private void RefreshDamageZoneConfiguration(SurvivorSkillStats[] next)
        {
            for (var i = _damageZones.Count - 1; i >= 0; i--)
            {
                var zone = _damageZones[i];
                var stats = next[(int)zone.source];
                zone.expiresAt = zone.createdAt + stats.duration;
                if (!HasSkill(zone.source) || zone.expiresAt <= Stats.Elapsed)
                {
                    RecycleRing(zone.ring);
                    _damageZones.RemoveAt(i);
                    continue;
                }
                zone.nextDamageAt = RescaleDeadline(zone.nextDamageAt, zone.stats.tickInterval, stats.tickInterval);
                zone.stats = stats;
                if (zone.ring == null) continue;
                zone.ring.radius = stats.radius;
                zone.ring.duration = stats.duration;
                zone.ring.expiresAt = zone.expiresAt;
                zone.ring.life = zone.expiresAt - Stats.Elapsed;
                zone.ring.color = SurvivorPalette.Alpha(stats.color, 0.65f);
                zone.ring.line.transform.localScale = new Vector3(stats.radius, 1, stats.radius);
            }
        }
    }
}
