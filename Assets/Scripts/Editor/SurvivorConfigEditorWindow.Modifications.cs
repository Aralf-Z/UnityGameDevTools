using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private SurvivorSkillDefinition SelectedSkillDefinition => selectedSkill < document.catalog.skills.Length
            ? document.catalog.skills[selectedSkill]
            : document.catalog.dash;

        private string SelectedSkillPath => selectedSkill < document.catalog.skills.Length
            ? "catalog.skills.Array.data[" + selectedSkill + "]"
            : "catalog.dash";

        private void BuildSkillContent()
        {
            selectedSkill = Mathf.Clamp(selectedSkill, 0, document.catalog.skills.Length);
            selectedModification = Mathf.Clamp(selectedModification, 0, SurvivorSkills.MODIFICATION_COUNT);
            var definition = SelectedSkillDefinition;
            Enum.TryParse(definition.id, out SkillKind kind);
            var path = SelectedSkillPath;
            if (selectedModification == 0)
            {
                if (kind == SkillKind.Dash) _content.Add(new HelpBox("冲刺默认拥有, 不参与新技能随机池, 可以参与已有技能的改造三选一。", HelpBoxMessageType.Info));
                else AddToggle("加入技能随机池", path + ".enabled");
                AddText("技能名称", path + ".displayName");
                AddText("基础描述", path + ".description", true);
                var stats = path + ".baseStats";
                foreach (var parameter in SurvivorSkills.Parameters(kind))
                    AddNumber(_content, SurvivorSkills.Label(parameter), stats + "." + SurvivorSkills.FieldName(parameter), SurvivorSkills.IsCount(parameter));
                AddColor("特效颜色", stats + ".effectColor", false);
                DrawDebuffs(stats + ".debuffs", false);
            }
            else
            {
                path += ".modifications.Array.data[" + (selectedModification - 1) + "]";
                AddToggle("加入改造随机池", path + ".enabled");
                AddText("改造 ID", path + ".id");
                AddText("改造名称", path + ".displayName");
                AddText("改造描述", path + ".additionalDescription", true);
                _content.Add(new HelpBox("5 项改造相互独立, 每项只能获得一次。改造只从已拥有技能中随机三选一, 同一组候选优先展示不同技能。数值支持设定、增加和倍率, 多项改造按配置顺序叠加。改造 ID 用于保存后的效果匹配, 修改 ID 会视为新的改造。", HelpBoxMessageType.Info));
                AddColor("覆盖特效颜色", path + ".effectColor", true);
                DrawOverrides(path + ".overrides", kind);
                DrawDebuffs(path + ".debuffOverrides", true);
            }
            DrawModificationPreview();
            AddPreview("改造组合后的最终效果");
        }

        private void DrawModificationPreview()
        {
            var foldout = new Foldout { text = "预览已获得的改造组合", value = true };
            _content.Add(foldout);
            var modifications = SelectedSkillDefinition.modifications;
            for (var i = 0; i < modifications.Length; i++)
            {
                var bit = 1 << i;
                var toggle = new Toggle(modifications[i].displayName) { value = (previewModificationMask & bit) != 0 };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) previewModificationMask |= bit;
                    else previewModificationMask &= ~bit;
                    RefreshPreview();
                });
                foldout.Add(toggle);
            }
        }

        private void RefreshSkillPreview()
        {
            var definition = SelectedSkillDefinition;
            Enum.TryParse(definition.id, out SkillKind kind);
            var stats = document.catalog.Resolve(kind, previewModificationMask);
            var result = new StringBuilder(document.catalog.Description(kind, previewModificationMask));
            result.Append("\n\n最终数值:");
            foreach (var parameter in SurvivorSkills.Parameters(kind))
                result.Append('\n').Append(SurvivorSkills.Label(parameter)).Append(" = ").Append(stats.Get(parameter).ToString("0.###"));
            if (document.levels != null)
            {
                var baseDamage = document.levels.BaseDamageAtLevel(1);
                result.Append("\n1 级基础伤害 ").Append(baseDamage.ToString("0.###")).Append(" × 技能倍率 ").Append(stats.damageMultiplier.ToString("0.###")).Append(" = ").Append((baseDamage * stats.damageMultiplier).ToString("0.###"));
            }
            foreach (var debuff in stats.debuffs.Where(item => item.enabled))
                result.Append("\nDebuff: ").Append(debuff.type).Append(" / ").Append(debuff.duration).Append("s");
            if (_preview != null) _preview.text = result.ToString();
        }
    }
}
