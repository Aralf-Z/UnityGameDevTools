using System;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private string _skillConfigurationJson;
        private readonly int[] _retainedSkillLevels = { 1, 1, 1, 1 };

        public bool ApplySkillConfiguration(string json)
        {
            if (!_initialized) return false;
            // 单独反序列化，避免编辑窗口里尚未保存的修改影响正在运行的对局。
            var next = SurvivorSkills.Parse(json);
            var canonicalJson = JsonUtility.ToJson(next);
            if (canonicalJson == _skillConfigurationJson) return false;
            var previous = _skills;
            var oldStats = new SurvivorSkillStats[4];
            var newStats = new SurvivorSkillStats[4];
            for (var i = 0; i < 4; i++) oldStats[i] = Parameters((SkillKind)i);
            _skills = next;
            for (var i = 0; i < 4; i++)
            {
                var skill = (SkillKind)i;
                var definition = next.Get(skill);
                if (_skillLevels[i] > 0) _retainedSkillLevels[i] = Mathf.Max(_retainedSkillLevels[i], _skillLevels[i]);
                if (!definition.enabled) _skillLevels[i] = 0;
                else if (!previous.Get(skill).enabled || _skillLevels[i] > 0)
                    _skillLevels[i] = Mathf.Clamp(_retainedSkillLevels[i], 1, definition.MaxTier + 1);
                newStats[i] = Parameters(skill);
                _readyAt[i] = RescaleDeadline(_readyAt[i], oldStats[i].cooldown, newStats[i].cooldown);
            }

            var bolt = newStats[(int)SkillKind.Bolt];
            if (_boltFollowUpAt >= 0)
            {
                var fired = oldStats[(int)SkillKind.Bolt].volleyCount - _boltVolleysRemaining;
                _boltVolleysRemaining = Mathf.Max(0, bolt.volleyCount - fired);
                _boltFollowUpAt = SkillLevel(SkillKind.Bolt) == 0 || _boltVolleysRemaining == 0 ? -1
                    : RescaleDeadline(_boltFollowUpAt, oldStats[(int)SkillKind.Bolt].volleyInterval, bolt.volleyInterval);
            }
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                projectile.life = bolt.projectileLifetime - (Stats.Elapsed - projectile.createdAt);
                if (SkillLevel(SkillKind.Bolt) == 0 || projectile.life <= 0)
                {
                    projectile.root.gameObject.SetActive(false);
                    _projectilePool.Push(projectile);
                    _projectiles.RemoveAt(i);
                    continue;
                }
                projectile.damage = bolt.damage * projectile.damageMultiplier;
                projectile.speed = bolt.projectileSpeed;
                projectile.debuffs = bolt.debuffs;
                projectile.body.sharedMaterial = MaterialFor(bolt.color);
            }

            RefreshFrostConfiguration(newStats[(int)SkillKind.Frost]);
            foreach (var enemy in _enemies)
            {
                enemy.orbitAt = RescaleDeadline(enemy.orbitAt, oldStats[(int)SkillKind.Orbit].hitInterval, newStats[(int)SkillKind.Orbit].hitInterval);
                enemy.knockbackDecay = newStats[(int)SkillKind.Nova].knockbackDecay;
                RefreshEnemyDebuffs(enemy, oldStats, newStats);
            }
            // 领域中的持续控制立即按新范围重算，不额外触发一次伤害。
            foreach (var zone in _frostZones)
                foreach (var enemy in _enemies)
                    if ((enemy.root.position - zone.position).sqrMagnitude <= zone.radius * zone.radius)
                        ApplyDebuffs(enemy, zone.debuffs, SkillKind.Frost, zone.expiresAt);

            ConfigureOrbiters();
            RefreshPresetIconColors();
            PendingUpgrades = Mathf.Min(PendingUpgrades, RemainingSkillUpgrades());
            _skillConfigurationJson = canonicalJson;
            ApplyTimeScale();
            RefreshHud(true);
            return true;
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
                zone.damage = stats.damage * zone.damageMultiplier;
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
                if (!Enum.TryParse(pair.Key, out SkillKind kind) || !Enum.IsDefined(typeof(SkillKind), kind)) continue;
                var texture = pair.Value.texture;
                var color = _skills.Resolve(kind, 0).color;
                for (var y = 2; y < 10; y++) for (var x = 2; x < 10; x++) texture.SetPixel(x, y, color);
                texture.Apply();
            }
        }
    }
}
