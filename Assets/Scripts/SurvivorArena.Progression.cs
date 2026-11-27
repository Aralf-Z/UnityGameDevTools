using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public readonly struct SurvivorRewardChoice
    {
        public readonly SurvivorRewardKind kind;
        public readonly SkillKind skill;
        public readonly int modificationIndex;

        public SurvivorRewardChoice(SurvivorRewardKind kind, SkillKind skill, int modificationIndex = -1)
        {
            this.kind = kind;
            this.skill = skill;
            this.modificationIndex = modificationIndex;
        }
    }

    public sealed partial class SurvivorArena
    {
        private readonly Queue<SurvivorRewardKind> _pendingRewards = new();
        private readonly List<SurvivorRewardChoice> _rewardChoices = new();
        private int _highestRewardLevel = 1;

        public IReadOnlyList<SurvivorRewardChoice> RewardChoices => _rewardChoices;
        public int SkillLevel(SkillKind skill) => HasSkill(skill) ? 1 + ModificationCount(skill) : 0;
        public bool HasSkill(SkillKind skill) => _skillLevels[(int)skill] > 0 && (skill == SkillKind.Dash || _skills.Get(skill).enabled);
        public int ModificationMask(SkillKind skill) => _skillModifications[(int)skill];
        private SurvivorSkillStats Parameters(SkillKind skill) => _skills.Resolve(skill, ModificationMask(skill));
        private string SkillName(SkillKind skill) => _skills.Get(skill).displayName;
        private float SkillInterval(SkillKind skill) => Parameters(skill).cooldown;
        public float SkillCooldown(SkillKind skill) => Mathf.Max(0, _readyAt[(int)skill] - Stats.Elapsed);

        public int ModificationCount(SkillKind skill)
        {
            var mask = ModificationMask(skill);
            var count = 0;
            while (mask != 0) { count += mask & 1; mask >>= 1; }
            return count;
        }

        private void ResetProgression()
        {
            Array.Clear(_skillLevels, 0, _skillLevels.Length);
            Array.Clear(_skillModifications, 0, _skillModifications.Length);
            Array.Clear(_readyAt, 0, _readyAt.Length);
            _skillLevels[(int)SkillKind.Dash] = 1;
            _pendingRewards.Clear();
            _rewardChoices.Clear();
            PendingModifications = PendingSkills = 0;
            _highestRewardLevel = 1;
            Stats.ExperienceRequired = _levels.Get(2).totalExperience;
            QueueLevelRewards(1);
            EnsureRewardChoices();
        }

        private void QueueLevelRewards(int level)
        {
            var definition = _levels.Get(level);
            // 同一等级先获取技能, 再从更新后的已拥有技能池选择改造.
            for (var i = 0; i < definition.skillRewards; i++)
            {
                _pendingRewards.Enqueue(SurvivorRewardKind.Skill);
                PendingSkills++;
            }
            for (var i = 0; i < definition.modificationRewards; i++)
            {
                _pendingRewards.Enqueue(SurvivorRewardKind.Modification);
                PendingModifications++;
            }
        }

        public void AddExperience(int amount)
        {
            Stats.Experience = (int)Math.Min((long)Stats.Experience + Math.Max(0, amount), int.MaxValue);
            var levelsGained = 0;
            while (Stats.Level < _levels.MaxLevel && Stats.Experience >= Stats.ExperienceRequired)
            {
                Stats.Level++;
                var definition = _levels.Get(Stats.Level);
                Stats.ExperienceRequired = _levels.Get(Stats.Level + 1).totalExperience;
                if (Stats.Level > _highestRewardLevel)
                {
                    Stats.MaxHealth += definition.maxHealthGain;
                    Stats.HealthRegenerationPerSecond += definition.healthRegenerationGain;
                    Stats.BaseDamage += definition.baseDamageGain;
                    QueueLevelRewards(Stats.Level);
                    _highestRewardLevel = Stats.Level;
                }
                levelsGained++;
            }
            if (levelsGained == 0) return;
            EnsureRewardChoices();
            Burst(PlayerPosition, 4, SurvivorPalette.Gold);
            Toast("LEVEL " + Stats.Level + "  /  选择技能或改造");
            PlayTone(2);
            ApplyTimeScale();
            RefreshHud(true);
        }

        private void EnsureRewardChoices()
        {
            if (_rewardChoices.Count > 0) return;
            while (_pendingRewards.Count > 0)
            {
                var kind = _pendingRewards.Peek();
                var candidates = BuildRewardCandidates(kind);
                while (candidates.Count > 0 && _rewardChoices.Count < 3)
                {
                    var index = _random.Next(candidates.Count);
                    _rewardChoices.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }
                if (_rewardChoices.Count > 0) return;
                ConsumeReward();
            }
        }

        private List<SurvivorRewardChoice> BuildRewardCandidates(SurvivorRewardKind kind)
        {
            var candidates = new List<SurvivorRewardChoice>();
            var count = kind == SurvivorRewardKind.Skill ? SurvivorSkills.COMBAT_SKILL_COUNT : SurvivorSkills.SKILL_COUNT;
            for (var i = 0; i < count; i++)
            {
                var skill = (SkillKind)i;
                if (kind == SurvivorRewardKind.Skill)
                {
                    if (_skills.Get(skill).enabled && !HasSkill(skill)) candidates.Add(new SurvivorRewardChoice(kind, skill));
                    continue;
                }
                if (!HasSkill(skill)) continue;
                var available = new List<int>();
                var modifications = _skills.Get(skill).modifications;
                for (var j = 0; j < modifications.Length; j++)
                    if (modifications[j].enabled && (ModificationMask(skill) & (1 << j)) == 0) available.Add(j);
                if (available.Count == 0) continue;
                var modification = available[_random.Next(available.Count)];
                candidates.Add(new SurvivorRewardChoice(kind, skill, modification));
            }
            return candidates;
        }

        public bool ChooseReward(int index)
        {
            if (!IsChoosingReward || index < 0 || index >= _rewardChoices.Count) return false;
            var choice = _rewardChoices[index];
            var previousStats = SnapshotSkillStats();
            if (choice.kind == SurvivorRewardKind.Skill)
            {
                if (HasSkill(choice.skill) || !_skills.Get(choice.skill).enabled) return false;
                _skillLevels[(int)choice.skill] = 1;
                Toast("获得技能 / " + SkillName(choice.skill));
            }
            else
            {
                if (!HasSkill(choice.skill) || (ModificationMask(choice.skill) & (1 << choice.modificationIndex)) != 0) return false;
                _skillModifications[(int)choice.skill] |= 1 << choice.modificationIndex;
                Toast(SkillName(choice.skill) + " / " + _skills.Get(choice.skill).modifications[choice.modificationIndex].displayName);
            }
            ConsumeReward();
            _rewardChoices.Clear();
            RefreshActiveSkillEffects(previousStats);
            EnsureRewardChoices();
            ApplyTimeScale();
            RefreshHud(true);
            return true;
        }

        private void ConsumeReward()
        {
            if (_pendingRewards.Dequeue() == SurvivorRewardKind.Skill) PendingSkills--;
            else PendingModifications--;
        }

        /// <summary> 调试接口: 0 移除技能, 1 获取基础技能, 2–6 按配置顺序应用改造; 冲刺始终保留. </summary>
        public void SetSkillLevel(SkillKind skill, int level)
        {
            var previousStats = SnapshotSkillStats();
            level = Mathf.Clamp(level, skill == SkillKind.Dash ? 1 : 0, SurvivorSkills.MODIFICATION_COUNT + 1);
            _skillLevels[(int)skill] = level > 0 ? 1 : 0;
            _skillModifications[(int)skill] = 0;
            var remaining = level - 1;
            for (var i = 0; i < SurvivorSkills.MODIFICATION_COUNT && remaining > 0; i++)
            {
                if (!_skills.Get(skill).modifications[i].enabled) continue;
                _skillModifications[(int)skill] |= 1 << i;
                remaining--;
            }
            RefreshActiveSkillEffects(previousStats);
            _rewardChoices.Clear();
            EnsureRewardChoices();
            ApplyTimeScale();
            RefreshHud(true);
        }
    }
}
