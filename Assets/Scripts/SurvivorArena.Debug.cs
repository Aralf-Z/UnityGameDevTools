using System;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        public float SetPlayerAttribute(string attribute, float value)
        {
            if (!IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "玩家属性必须为有限数值。");
            switch ((attribute ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "health": case "生命": case "当前生命": SetHealth(value); return Stats.Health;
                case "maxhealth": case "生命上限":
                    Stats.MaxHealth = Mathf.Clamp(value, 0.01f, 10000000);
                    SetHealth(Mathf.Min(Stats.Health, Stats.MaxHealth));
                    return Stats.MaxHealth;
                case "healthregenerationpersecond": case "healthregen": case "每秒生命恢复":
                    value = Stats.HealthRegenerationPerSecond = Mathf.Clamp(value, 0, 10000);
                    break;
                case "movespeed": case "移速": case "移动速度":
                    value = Stats.MoveSpeed = Mathf.Clamp(value, 0, 1000);
                    break;
                case "basedamage": case "基础伤害":
                    value = Stats.BaseDamage = Mathf.Clamp(value, 0, 10000000);
                    break;
                default: throw new ArgumentException("未知玩家属性: " + attribute + "。可用 Health、MaxHealth、HealthRegenerationPerSecond、MoveSpeed、BaseDamage。");
            }
            RefreshHud(true);
            return value;
        }

        /// <summary> 直接构造目标等级, 不发放奖励, 保留当前技能与改造. </summary>
        public void SetPlayerLevel(int level)
        {
            level = Mathf.Clamp(level, 1, _levels.MaxLevel);
            Stats.Level = _highestRewardLevel = level;
            Stats.Experience = _levels.Get(level).totalExperience;
            Stats.ExperienceRequired = _levels.Get(level + 1).totalExperience;
            Stats.MaxHealth = _levels.MaxHealthAtLevel(level);
            Stats.Health = Mathf.Min(Stats.Health, Stats.MaxHealth);
            Stats.HealthRegenerationPerSecond = _levels.HealthRegenerationAtLevel(level);
            Stats.BaseDamage = _levels.BaseDamageAtLevel(level);
            ClearPendingRewards();
        }

        /// <summary> 无奖励次数及技能槽位限制, 重复获取不会覆盖改造或冷却. </summary>
        public bool GiveSkill(SkillKind skill)
        {
            ValidateDebugSkill(skill);
            if (HasSkill(skill)) return false;
            var previous = SnapshotSkillStats();
            _skillLevels[(int)skill] = 1;
            RefreshDebugSkills(previous);
            return true;
        }

        public int GiveAllSkills()
        {
            var previous = SnapshotSkillStats();
            var added = 0;
            for (var i = 0; i < SurvivorSkills.COMBAT_SKILL_COUNT; i++)
            {
                var skill = (SkillKind)i;
                if (!_skills.Get(skill).enabled || HasSkill(skill)) continue;
                _skillLevels[i] = 1;
                added++;
            }
            if (added > 0) RefreshDebugSkills(previous);
            return added;
        }

        public bool RemoveSkill(SkillKind skill)
        {
            ValidateDebugSkill(skill, false);
            if (skill == SkillKind.Dash) throw new ArgumentException("默认冲刺不能移除。");
            if (_skillLevels[(int)skill] == 0 && ModificationMask(skill) == 0) return false;
            var previous = SnapshotSkillStats();
            _skillLevels[(int)skill] = _skillModifications[(int)skill] = 0;
            RefreshDebugSkills(previous);
            return true;
        }

        public bool GiveModification(SkillKind skill, int index)
        {
            ValidateDebugModification(skill, index, true);
            if ((ModificationMask(skill) & (1 << index)) != 0 && HasSkill(skill)) return false;
            var previous = SnapshotSkillStats();
            _skillLevels[(int)skill] = 1;
            _skillModifications[(int)skill] |= 1 << index;
            RefreshDebugSkills(previous);
            return true;
        }

        public int GiveAllModifications(SkillKind? skill = null)
        {
            if (skill.HasValue) ValidateDebugSkill(skill.Value);
            var previous = SnapshotSkillStats();
            var added = 0;
            var changed = false;
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var kind = (SkillKind)i;
                if ((skill.HasValue && kind != skill.Value) || !_skills.Get(kind).enabled) continue;
                changed |= !HasSkill(kind);
                _skillLevels[i] = 1;
                var modifications = _skills.Get(kind).modifications;
                for (var j = 0; j < modifications.Length; j++)
                {
                    if (!modifications[j].enabled || (_skillModifications[i] & (1 << j)) != 0) continue;
                    _skillModifications[i] |= 1 << j;
                    added++;
                }
            }
            if (changed || added > 0) RefreshDebugSkills(previous);
            return added;
        }

        public bool RemoveModification(SkillKind skill, int index)
        {
            ValidateDebugModification(skill, index, false);
            if ((ModificationMask(skill) & (1 << index)) == 0) return false;
            var previous = SnapshotSkillStats();
            _skillModifications[(int)skill] &= ~(1 << index);
            RefreshDebugSkills(previous);
            return true;
        }

        public void ResetSkillCooldowns(SkillKind? skill = null)
        {
            if (skill.HasValue) ValidateDebugSkill(skill.Value, false);
            for (var i = 0; i < _readyAt.Length; i++)
                if (!skill.HasValue || i == (int)skill.Value) _readyAt[i] = 0;
            if (!skill.HasValue || skill.Value == SkillKind.Dash) _dashReadyAt = 0;
            RefreshHud(true);
        }

        public int ClearPendingRewards()
        {
            var count = _pendingRewards.Count;
            _pendingRewards.Clear();
            _rewardChoices.Clear();
            PendingSkills = PendingModifications = 0;
            ApplyTimeScale();
            RefreshHud(true);
            return count;
        }

        private void ValidateDebugSkill(SkillKind skill, bool requireEnabled = true)
        {
            if (!Enum.IsDefined(typeof(SkillKind), skill)) throw new ArgumentOutOfRangeException(nameof(skill), "未知技能。");
            if (requireEnabled && !_skills.Get(skill).enabled) throw new ArgumentException("技能已在配置中禁用: " + skill);
        }

        private void ValidateDebugModification(SkillKind skill, int index, bool requireEnabled)
        {
            ValidateDebugSkill(skill, requireEnabled);
            var modifications = _skills.Get(skill).modifications;
            if (index < 0 || index >= modifications.Length) throw new ArgumentOutOfRangeException(nameof(index), "改造序号超出范围。");
            if (requireEnabled && !modifications[index].enabled) throw new ArgumentException("改造已在配置中禁用: " + modifications[index].id);
        }

        private void RefreshDebugSkills(SurvivorSkillStats[] previous)
        {
            RefreshActiveSkillEffects(previous);
            // 直接修改后重建随机池, 避免旧选项指向已获得的技能或改造.
            _rewardChoices.Clear();
            EnsureRewardChoices();
            ApplyTimeScale();
            RefreshHud(true);
        }
    }
}
