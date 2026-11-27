using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private readonly List<DamageZone> _damageZones = new();

        private float AutoCastRange(SkillKind skill)
        {
            var stats = Parameters(skill);
            return skill == SkillKind.Missile || skill == SkillKind.Venom ? stats.targetRange : stats.radius;
        }

        private Enemy NearestEnemy(Vector3 origin, float radius, HashSet<Enemy> excluded = null)
        {
            Enemy nearest = null;
            var distance = radius * radius;
            foreach (var enemy in _enemies)
            {
                if (excluded != null && excluded.Contains(enemy)) continue;
                var next = (enemy.root.position - origin).sqrMagnitude;
                if (next > distance) continue;
                nearest = enemy;
                distance = next;
            }
            return nearest;
        }

        private bool CastLightning()
        {
            var stats = Parameters(SkillKind.Lightning);
            var origin = PlayerPosition;
            var hit = new HashSet<Enemy>();
            for (var i = 0; i < stats.projectileCount; i++)
            {
                var target = NearestEnemy(origin, stats.radius, hit);
                if (target == null) break;
                hit.Add(target);
                origin = target.root.position;
                ApplyDebuffs(target, stats.debuffs, SkillKind.Lightning);
                Burst(origin, 1.1f, stats.color, 0.2f);
                EmitSkillParticles(origin, 1, stats.color, false);
                DamageEnemy(_enemies.IndexOf(target), stats.damageMultiplier * Stats.BaseDamage);
            }
            if (hit.Count > 0) PlayTone(0);
            return hit.Count > 0;
        }

        private bool CastDamageZone(SkillKind skill, bool followsPlayer)
        {
            if (_damageZones.Count >= 32) return false;
            var stats = Parameters(skill);
            var origin = PlayerPosition;
            if (!followsPlayer)
            {
                var target = NearestEnemy(origin, stats.targetRange);
                if (target == null) return false;
                origin = target.root.position;
            }
            var zone = new DamageZone
            {
                source = skill,
                position = origin,
                followsPlayer = followsPlayer,
                createdAt = Stats.Elapsed,
                expiresAt = Stats.Elapsed + stats.duration,
                nextDamageAt = Stats.Elapsed,
                stats = stats,
                ring = Burst(origin, stats.radius, SurvivorPalette.Alpha(stats.color, 0.65f), stats.duration, true)
            };
            _damageZones.Add(zone);
            EmitSkillParticles(origin, stats.radius, stats.color, false);
            PlayTone(0);
            return true;
        }

        private void TickDamageZones()
        {
            for (var i = _damageZones.Count - 1; i >= 0; i--)
            {
                var zone = _damageZones[i];
                if (Stats.Elapsed >= zone.expiresAt || !HasSkill(zone.source))
                {
                    RecycleRing(zone.ring);
                    _damageZones.RemoveAt(i);
                    continue;
                }
                if (zone.followsPlayer) zone.position = PlayerPosition;
                if (zone.ring != null) zone.ring.line.transform.position = zone.position + Vector3.up * 0.08f;
                if (Stats.Elapsed < zone.nextDamageAt) continue;
                zone.nextDamageAt = Stats.Elapsed + zone.stats.tickInterval;
                DamageArea(zone.position, zone.stats, zone.source, zone.expiresAt);
            }
        }

        private void DamageArea(Vector3 origin, SurvivorSkillStats stats, SkillKind source, float expiresAt = float.PositiveInfinity)
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if ((enemy.root.position - origin).sqrMagnitude > stats.radius * stats.radius) continue;
                ApplyDebuffs(enemy, stats.debuffs, source, expiresAt);
                if (stats.damageMultiplier > 0) DamageEnemy(i, stats.damageMultiplier * Stats.BaseDamage);
            }
        }

        private bool FireMissiles()
        {
            var stats = Parameters(SkillKind.Missile);
            var target = NearestEnemy(PlayerPosition, stats.targetRange);
            if (target == null || _projectiles.Count >= 220) return false;
            var direction = (target.root.position - PlayerPosition).normalized;
            var count = Mathf.Min(stats.projectileCount, 220 - _projectiles.Count);
            for (var i = 0; i < count; i++)
            {
                var projectile = _projectilePool.Count > 0 ? _projectilePool.Pop() : CreateProjectile();
                projectile.source = SkillKind.Missile;
                projectile.target = target;
                projectile.direction = Quaternion.Euler(0, (i - (count - 1) / 2f) * stats.projectileSpread, 0) * direction;
                projectile.position = PlayerPosition;
                projectile.damageMultiplier = stats.damageMultiplier;
                projectile.explosionRadius = stats.radius;
                projectile.createdAt = Stats.Elapsed;
                projectile.life = stats.projectileLifetime;
                projectile.speed = stats.projectileSpeed;
                projectile.debuffs = stats.debuffs;
                projectile.body.sharedMaterial = MaterialFor(stats.color);
                projectile.root.position = PlayerPosition + Vector3.up * 0.9f;
                projectile.root.rotation = Quaternion.LookRotation(projectile.direction);
                projectile.root.gameObject.SetActive(true);
                _projectiles.Add(projectile);
            }
            PlayTone(0);
            return true;
        }

        private void ExplodeMissile(Projectile projectile)
        {
            var stats = Parameters(SkillKind.Missile).Copy();
            stats.damageMultiplier = projectile.damageMultiplier;
            stats.radius = projectile.explosionRadius;
            stats.debuffs = projectile.debuffs;
            DamageArea(projectile.position, stats, SkillKind.Missile);
            Burst(projectile.position, stats.radius, stats.color, 0.35f);
            EmitSkillParticles(projectile.position, stats.radius, stats.color, false);
        }

        private sealed class DamageZone
        {
            public SkillKind source;
            public Vector3 position;
            public bool followsPlayer;
            public float createdAt, expiresAt, nextDamageAt;
            public SurvivorSkillStats stats;
            public RingEffect ring;
        }
    }
}
