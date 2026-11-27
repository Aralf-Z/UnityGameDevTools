using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private Vector3 EnemyMovementDirection(SurvivorEnemyDefinition definition, Vector3 delta)
        {
            if (delta.sqrMagnitude < 0.001f) return Vector3.zero;
            var direction = delta.normalized;
            if (definition.preferredDistance <= 0) return direction;
            var distance = delta.magnitude;
            if (distance < definition.preferredDistance - 0.8f) return -direction;
            if (distance <= definition.preferredDistance + 0.8f) return Vector3.zero;
            return direction;
        }

        private void SynchronizeEnemySkills(Enemy enemy, SurvivorEnemyDefinition definition, SurvivorEnemySkillCatalog previous, bool reset)
        {
            CancelEnemyCast(enemy);
            enemy.chargeUntil = enemy.guardUntil = enemy.hasteUntil = 0;
            var states = new List<EnemySkillState>();
            foreach (var id in definition.skills)
            {
                var skill = _enemySkills.Get(id);
                var oldState = reset ? null : enemy.skills.Find(state => state.id == id);
                var ready = Stats.Elapsed + skill.initialCooldown + (float)_random.NextDouble() * skill.initialCooldownJitter;
                if (oldState != null)
                {
                    var oldCooldown = previous != null && previous.Contains(id) ? previous.Get(id).cooldown : skill.cooldown;
                    ready = RescaleDeadline(oldState.readyAt, oldCooldown, skill.cooldown);
                }
                states.Add(new EnemySkillState { id = id, readyAt = ready });
            }
            enemy.skills = states;
        }

        private void TickEnemyAbilities(Enemy enemy, SurvivorEnemyDefinition definition)
        {
            if (Stats.Elapsed < enemy.chargeUntil)
            {
                var reach = enemy.radius + 0.6f;
                var segment = enemy.root.position - enemy.previousPosition;
                var along = Mathf.Clamp01(Vector3.Dot(PlayerPosition - enemy.previousPosition, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                var nearest = enemy.previousPosition + segment * along;
                if (!enemy.chargeHit && (PlayerPosition - nearest).sqrMagnitude <= reach * reach)
                {
                    enemy.chargeHit = true;
                    if (enemy.chargeDamage > 0) HurtPlayer(enemy.chargeDamage);
                }
                return;
            }
            if (enemy.cast != null)
            {
                if (Stats.Elapsed >= enemy.cast.executeAt) FinishEnemyCast(enemy, definition);
                return;
            }
            foreach (var state in enemy.skills)
            {
                var skill = _enemySkills.Get(state.id);
                if (!CanEnemyCast(enemy, skill, state.readyAt)) continue;
                BeginEnemyCast(enemy, skill, state);
                if (skill.windup <= 0) FinishEnemyCast(enemy, definition);
                return;
            }
        }

        private bool CanEnemyCast(Enemy enemy, SurvivorEnemySkillDefinition skill, float readyAt)
        {
            if (!skill.enabled || Stats.Elapsed < readyAt) return false;
            if ((PlayerPosition - enemy.root.position).sqrMagnitude > skill.castRange * skill.castRange) return false;
            if (skill.behavior == "Guard" && Stats.Elapsed < enemy.guardUntil) return false;
            if (skill.behavior == "Haste" && Stats.Elapsed < enemy.hasteUntil) return false;
            if (skill.behavior == "Heal" && enemy.health / enemy.maxHealth > skill.healThreshold) return false;
            return true;
        }

        private void BeginEnemyCast(Enemy enemy, SurvivorEnemySkillDefinition skill, EnemySkillState state)
        {
            state.readyAt = Stats.Elapsed + skill.cooldown;
            var cast = new EnemyCast { id = skill.id, executeAt = Stats.Elapsed + skill.windup, target = PlayerPosition };
            if (skill.windup > 0)
            {
                var position = skill.behavior == "Bombard" ? cast.target : enemy.root.position;
                var radius = skill.radius;
                if (skill.behavior == "Projectile" || skill.behavior == "Volley" || skill.behavior == "Charge") radius = enemy.radius + 0.4f;
                cast.telegraph = Burst(position, radius, SurvivorPalette.Alpha(skill.color, 0.5f), skill.windup, true);
                if (cast.telegraph != null) cast.telegraphGeneration = cast.telegraph.generation;
            }
            enemy.cast = cast;
        }

        private void FinishEnemyCast(Enemy enemy, SurvivorEnemyDefinition definition)
        {
            var cast = enemy.cast;
            enemy.cast = null;
            RecycleEnemyTelegraph(cast);
            var skill = _enemySkills.Get(cast.id);
            if (!skill.enabled) return;
            var damage = skill.damage * definition.skillDamageMultiplier;
            switch (skill.behavior)
            {
                case "Strike": EnemyStrike(enemy, skill, cast.target, damage); break;
                case "Shockwave": EnemyAreaAttack(enemy.root.position, skill, damage); break;
                case "Bombard": EnemyAreaAttack(cast.target, skill, damage); break;
                case "Projectile":
                case "Volley": FireEnemyProjectiles(enemy, skill, cast.target, damage); break;
                case "Charge": BeginEnemyCharge(enemy, skill, cast.target, damage); break;
                case "Guard":
                    enemy.guardUntil = Stats.Elapsed + skill.duration;
                    enemy.damageReduction = skill.damageReduction;
                    Burst(enemy.root.position, enemy.radius + 0.5f, skill.color, 0.6f);
                    break;
                case "Haste":
                    enemy.hasteUntil = Stats.Elapsed + skill.duration;
                    enemy.hasteMultiplier = skill.moveSpeedMultiplier;
                    Burst(enemy.root.position, enemy.radius + 0.5f, skill.color, 0.4f);
                    break;
                case "Heal":
                    enemy.health = Mathf.Min(enemy.maxHealth, enemy.health + enemy.maxHealth * skill.healPercent);
                    Burst(enemy.root.position, enemy.radius + 0.5f, skill.color, 0.5f);
                    break;
            }
        }

        private void EnemyStrike(Enemy enemy, SurvivorEnemySkillDefinition skill, Vector3 target, float damage)
        {
            var delta = PlayerPosition - enemy.root.position;
            var aim = target - enemy.root.position;
            var inArc = aim.sqrMagnitude < 0.001f || delta.sqrMagnitude < 0.001f || Vector3.Dot(aim.normalized, delta.normalized) >= Mathf.Cos(skill.arcAngle * 0.5f * Mathf.Deg2Rad);
            Burst(enemy.root.position, skill.radius, skill.color, 0.25f);
            if (inArc && delta.sqrMagnitude <= skill.radius * skill.radius && damage > 0) HurtPlayer(damage);
        }

        private void EnemyAreaAttack(Vector3 position, SurvivorEnemySkillDefinition skill, float damage)
        {
            Burst(position, skill.radius, skill.color, 0.4f);
            EmitSkillParticles(position, skill.radius, skill.color, false);
            if ((PlayerPosition - position).sqrMagnitude <= skill.radius * skill.radius && damage > 0) HurtPlayer(damage);
        }

        private void BeginEnemyCharge(Enemy enemy, SurvivorEnemySkillDefinition skill, Vector3 target, float damage)
        {
            var direction = target - enemy.root.position;
            enemy.chargeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            enemy.chargeUntil = Stats.Elapsed + skill.duration;
            enemy.chargeSpeed = skill.chargeSpeed;
            enemy.chargeDamage = damage;
            enemy.chargeHit = false;
            Burst(enemy.root.position, enemy.radius + 0.5f, skill.color, 0.3f);
        }

        private void CancelEnemyCast(Enemy enemy)
        {
            if (enemy.cast == null) return;
            RecycleEnemyTelegraph(enemy.cast);
            enemy.cast = null;
        }

        private void RecycleEnemyTelegraph(EnemyCast cast)
        {
            if (cast.telegraph != null && cast.telegraph.generation == cast.telegraphGeneration) RecycleRing(cast.telegraph);
        }

        private sealed class EnemySkillState
        {
            public string id;
            public float readyAt;
        }

        private sealed class EnemyCast
        {
            public string id;
            public float executeAt;
            public Vector3 target;
            public RingEffect telegraph;
            public int telegraphGeneration;
        }
    }
}
