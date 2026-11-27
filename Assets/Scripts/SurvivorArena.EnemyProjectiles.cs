using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private readonly List<EnemyProjectile> _enemyProjectiles = new();
        private readonly Stack<EnemyProjectile> _enemyProjectilePool = new();

        private void FireEnemyProjectiles(Enemy enemy, SurvivorEnemySkillDefinition skill, Vector3 target, float damage)
        {
            var aim = target - enemy.root.position;
            var direction = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector3.forward;
            var count = Mathf.Min(skill.projectileCount, _enemySkills.projectileLimit - _enemyProjectiles.Count);
            for (var i = 0; i < count; i++)
            {
                var projectile = _enemyProjectilePool.Count > 0 ? _enemyProjectilePool.Pop() : CreateEnemyProjectile();
                projectile.enemyId = enemy.id;
                projectile.skillId = skill.id;
                projectile.position = enemy.root.position;
                projectile.direction = Quaternion.Euler(0, (i - (count - 1) / 2f) * skill.projectileSpread, 0) * direction;
                projectile.damage = damage;
                projectile.speed = skill.projectileSpeed;
                projectile.radius = skill.projectileRadius;
                projectile.createdAt = Stats.Elapsed;
                projectile.expiresAt = Stats.Elapsed + skill.projectileLifetime;
                projectile.body.sharedMaterial = MaterialFor(skill.color);
                projectile.root.localScale = Vector3.one * (skill.projectileRadius * 2);
                projectile.root.position = projectile.position + Vector3.up * 0.8f;
                projectile.root.gameObject.SetActive(true);
                _enemyProjectiles.Add(projectile);
            }
        }

        private EnemyProjectile CreateEnemyProjectile()
        {
            var body = Shape("Enemy Projectile", PrimitiveType.Sphere, _world, Vector3.zero, Vector3.one * 0.3f, SurvivorPalette.Enemy);
            return new EnemyProjectile { root = body.transform, body = body };
        }

        private void TickEnemyProjectiles(float deltaTime)
        {
            for (var i = _enemyProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _enemyProjectiles[i];
                var before = projectile.position;
                projectile.position += projectile.direction * (projectile.speed * deltaTime);
                var segment = projectile.position - before;
                var along = Mathf.Clamp01(Vector3.Dot(PlayerPosition - before, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                var distance = PlayerPosition - (before + segment * along);
                var hit = distance.sqrMagnitude <= (projectile.radius + 0.5f) * (projectile.radius + 0.5f);
                projectile.root.position = projectile.position + Vector3.up * 0.8f;
                if (!hit && Stats.Elapsed < projectile.expiresAt) continue;
                if (hit && projectile.damage > 0) HurtPlayer(projectile.damage);
                RecycleEnemyProjectile(i);
                if (HasEnded) return;
            }
        }

        private void RecycleEnemyProjectile(int index)
        {
            var projectile = _enemyProjectiles[index];
            projectile.root.gameObject.SetActive(false);
            _enemyProjectilePool.Push(projectile);
            _enemyProjectiles.RemoveAt(index);
        }

        private void ClearEnemyProjectiles()
        {
            for (var i = _enemyProjectiles.Count - 1; i >= 0; i--) RecycleEnemyProjectile(i);
        }

        private void RefreshEnemyProjectiles()
        {
            for (var i = _enemyProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _enemyProjectiles[i];
                if (!_enemyCatalog.Contains(projectile.enemyId) || !_enemySkills.Contains(projectile.skillId) || !_enemySkills.Get(projectile.skillId).enabled)
                {
                    RecycleEnemyProjectile(i);
                    continue;
                }
                var skill = _enemySkills.Get(projectile.skillId);
                projectile.damage = skill.damage * _enemyCatalog.Get(projectile.enemyId).skillDamageMultiplier;
                projectile.speed = skill.projectileSpeed;
                projectile.radius = skill.projectileRadius;
                projectile.expiresAt = projectile.createdAt + skill.projectileLifetime;
                projectile.root.localScale = Vector3.one * (skill.projectileRadius * 2);
                projectile.body.sharedMaterial = MaterialFor(skill.color);
                if (Stats.Elapsed >= projectile.expiresAt || i >= _enemySkills.projectileLimit) RecycleEnemyProjectile(i);
            }
        }

        private sealed class EnemyProjectile
        {
            public Transform root;
            public Renderer body;
            public string enemyId, skillId;
            public Vector3 position, direction;
            public float damage, speed, radius, createdAt, expiresAt;
        }
    }
}
