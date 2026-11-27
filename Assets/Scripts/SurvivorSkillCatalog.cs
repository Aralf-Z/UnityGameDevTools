using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    [Serializable]
    public sealed class SurvivorSkillModification
    {
        public string id;
        public bool enabled = true;
        public string displayName = "新改造";
        [TextArea] public string additionalDescription = "";
        public string effectColor = "";
        public SurvivorSkillValueOverride[] overrides = Array.Empty<SurvivorSkillValueOverride>();
        public SurvivorDebuffConfig[] debuffOverrides = Array.Empty<SurvivorDebuffConfig>();
    }

    [Serializable]
    public sealed class SurvivorSkillDefinition
    {
        public string id;
        public bool enabled = true;
        public string displayName;
        [TextArea] public string description;
        public SurvivorSkillStats baseStats = new();
        public SurvivorSkillModification[] modifications = new SurvivorSkillModification[SurvivorSkills.MODIFICATION_COUNT];
    }

    [Serializable]
    public sealed class SurvivorSkillCatalog
    {
        public int version = 3;
        public SurvivorSkillDefinition[] skills;
        public SurvivorSkillDefinition dash;
        [NonSerialized] private SurvivorSkillDefinition[] _ordered;
        [NonSerialized] private SurvivorSkillStats[][] _resolved;

        public SurvivorSkillDefinition Get(SkillKind skill)
        {
            if (_ordered == null) Rebuild();
            return _ordered[(int)skill];
        }

        /// <summary> 每位表示一项已获得改造, 多项改造按配置顺序叠加, 不要求逐阶解锁. </summary>
        public SurvivorSkillStats Resolve(SkillKind skill, int modificationMask)
        {
            if (_resolved == null) Rebuild();
            return _resolved[(int)skill][modificationMask & SurvivorSkills.ALL_MODIFICATIONS];
        }

        public string Description(SkillKind skill, int modificationMask)
        {
            var definition = Get(skill);
            var text = new StringBuilder(definition.description ?? "");
            for (var i = 0; i < definition.modifications.Length; i++)
            {
                var modification = definition.modifications[i];
                if ((modificationMask & (1 << i)) == 0 || !modification.enabled) continue;
                text.Append("\n").Append(modification.displayName).Append(": ").Append(modification.additionalDescription);
            }
            return text.ToString();
        }

        public void Rebuild()
        {
            if (version != 3 || skills == null || skills.Length != SurvivorSkills.COMBAT_SKILL_COUNT || dash == null)
                throw new ArgumentException("技能配置版本必须为 3, 包含 8 个战斗技能以及独立的 Dash 配置。");
            var ordered = new SurvivorSkillDefinition[SurvivorSkills.SKILL_COUNT];
            foreach (var definition in skills)
            {
                var kind = ParseKind(definition);
                if (kind == SkillKind.Dash) throw new ArgumentException("Dash 必须放在独立的 dash 配置中。");
                AddDefinition(ordered, kind, definition);
            }
            if (ParseKind(dash) != SkillKind.Dash) throw new ArgumentException("冲刺配置的 id 必须为 Dash。");
            if (!dash.enabled) throw new ArgumentException("冲刺必须保持启用。");
            AddDefinition(ordered, SkillKind.Dash, dash);
            var resolved = new SurvivorSkillStats[SurvivorSkills.SKILL_COUNT][];
            for (var i = 0; i < ordered.Length; i++)
            {
                var definition = ordered[i];
                ValidateDefinition(definition, (SkillKind)i);
                resolved[i] = new SurvivorSkillStats[SurvivorSkills.ALL_MODIFICATIONS + 1];
                for (var mask = 0; mask <= SurvivorSkills.ALL_MODIFICATIONS; mask++)
                    resolved[i][mask] = ResolveDefinition(definition, (SkillKind)i, mask);
            }
            _ordered = ordered;
            _resolved = resolved;
        }

        private static SkillKind ParseKind(SurvivorSkillDefinition definition)
        {
            if (definition == null || !Enum.TryParse(definition.id, false, out SkillKind kind) || !Enum.IsDefined(typeof(SkillKind), kind))
                throw new ArgumentException("技能 id 无效。");
            return kind;
        }

        private static void AddDefinition(SurvivorSkillDefinition[] ordered, SkillKind kind, SurvivorSkillDefinition definition)
        {
            if (ordered[(int)kind] != null) throw new ArgumentException("重复的技能 id: " + definition.id);
            ordered[(int)kind] = definition;
        }

        private static void ValidateDefinition(SurvivorSkillDefinition definition, SkillKind kind)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.displayName) || definition.baseStats == null)
                throw new ArgumentException(kind + " 缺少名称或基础数值。");
            if (definition.modifications == null || definition.modifications.Length != SurvivorSkills.MODIFICATION_COUNT)
                throw new ArgumentException(kind + " 必须配置 5 个改造。");
            var ids = new HashSet<string>();
            foreach (var modification in definition.modifications)
            {
                if (modification == null || string.IsNullOrWhiteSpace(modification.id) || !ids.Add(modification.id))
                    throw new ArgumentException(kind + " 的改造 id 必须非空且唯一。");
                if (string.IsNullOrWhiteSpace(modification.displayName)) throw new ArgumentException(kind + " 的改造缺少名称。");
                SurvivorSkills.ValidateDebuffs(modification.debuffOverrides);
            }
            SurvivorSkills.ValidateDebuffs(definition.baseStats.debuffs);
        }

        private static SurvivorSkillStats ResolveDefinition(SurvivorSkillDefinition definition, SkillKind kind, int mask)
        {
            var stats = definition.baseStats.Copy();
            for (var i = 0; i < definition.modifications.Length; i++)
            {
                var modification = definition.modifications[i];
                if ((mask & (1 << i)) == 0 || !modification.enabled) continue;
                ApplyModification(stats, kind, modification);
            }
            SurvivorSkills.ValidateStats(kind, stats);
            return stats;
        }

        private static void ApplyModification(SurvivorSkillStats stats, SkillKind kind, SurvivorSkillModification modification)
        {
            var seen = new HashSet<SurvivorSkillParameter>();
            foreach (var change in modification.overrides ?? Array.Empty<SurvivorSkillValueOverride>())
            {
                if (change == null || !Enum.TryParse(change.parameter, false, out SurvivorSkillParameter parameter) || Array.IndexOf(SurvivorSkills.Parameters(kind), parameter) < 0)
                    throw new ArgumentException(kind + " 的改造包含无效参数。");
                if (!seen.Add(parameter)) throw new ArgumentException(modification.displayName + " 重复覆盖 " + parameter);
                if (!SurvivorArena.IsFinite(change.value)) throw new ArgumentException("改造数值必须是有限数字。");
                var value = change.operation switch
                {
                    "Set" => change.value,
                    "Add" => stats.Get(parameter) + change.value,
                    "Multiply" => stats.Get(parameter) * change.value,
                    _ => throw new ArgumentException("改造方式必须为 Set、Add 或 Multiply。")
                };
                SurvivorSkills.ValidateValue(parameter, value);
                stats.Set(parameter, value);
            }
            if (!string.IsNullOrWhiteSpace(modification.effectColor)) stats.effectColor = modification.effectColor;
            var debuffs = new List<SurvivorDebuffConfig>(stats.debuffs);
            foreach (var debuff in modification.debuffOverrides ?? Array.Empty<SurvivorDebuffConfig>())
            {
                debuffs.RemoveAll(item => item.type == debuff.type);
                if (debuff.enabled) debuffs.Add(debuff);
            }
            stats.debuffs = debuffs.ToArray();
        }
    }
}
