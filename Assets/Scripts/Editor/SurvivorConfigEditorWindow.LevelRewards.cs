using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        [SerializeField] private string skillRewardLevelInput, modificationRewardLevelInput;
        [SerializeField] private string skillRewardInputError, modificationRewardInputError;
        private TextField _skillRewardLevelField, _modificationRewardLevelField;

        private void DrawLevelRewardInputs()
        {
            var rewards = new Foldout { text = "技能解锁与改造获取", value = true };
            _content.Add(rewards);
            var skillText = skillRewardInputError == null ? FormatRewardLevels(true) : skillRewardLevelInput;
            _skillRewardLevelField = new TextField("解锁技能") { value = skillText, isDelayed = true };
            _skillRewardLevelField.RegisterValueChangedCallback(evt => ApplyRewardLevelInput(evt.newValue, true));
            rewards.Add(_skillRewardLevelField);
            var modificationText = modificationRewardInputError == null ? FormatRewardLevels(false) : modificationRewardLevelInput;
            _modificationRewardLevelField = new TextField("获得改造") { value = modificationText, multiline = true, isDelayed = true };
            _modificationRewardLevelField.style.minHeight = 44;
            _modificationRewardLevelField.RegisterValueChangedCallback(evt => ApplyRewardLevelInput(evt.newValue, false));
            rewards.Add(_modificationRewardLevelField);
            rewards.Add(new HelpBox("填写等级列表, 如解锁技能: 1, 2, 5, 10。支持中英文逗号、空格和换行。离开输入框后自动解析为 JSON 中各级的奖励次数; 每填写一个等级发放一次, 重复等级可发放多次, 留空表示不发放。保存全部修改后写入配置文件。", HelpBoxMessageType.Info));
        }

        private string FormatRewardLevels(bool skills)
        {
            var values = document.levels.levels.SelectMany(level =>
            {
                var count = skills ? level.skillRewards : level.modificationRewards;
                return Enumerable.Repeat(level.level.ToString(CultureInfo.InvariantCulture), count);
            });
            return string.Join(", ", values);
        }

        private int[] ParseRewardLevels(string input, bool skills)
        {
            var counts = new int[document.levels.MaxLevel];
            var tokens = (input ?? "").Split(new[] { ',', '，', ';', '；', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var limit = skills ? SurvivorSkills.COMBAT_SKILL_COUNT : SurvivorSkills.SKILL_COUNT * SurvivorSkills.MODIFICATION_COUNT;
            var label = skills ? "解锁技能" : "获得改造";
            foreach (var token in tokens)
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level < 1 || level > counts.Length)
                    throw new ArgumentException(label + "的等级必须在 1–" + counts.Length + " 之间: " + token);
                counts[level - 1]++;
                if (counts[level - 1] > limit) throw new ArgumentException(label + "在 " + level + " 级的次数不能超过 " + limit + "。");
            }
            return counts;
        }

        private void ApplyRewardLevelInput(string input, bool skills)
        {
            if (skills) skillRewardLevelInput = input;
            else modificationRewardLevelInput = input;
            int[] counts;
            try { counts = ParseRewardLevels(input, skills); }
            catch (Exception exception)
            {
                if (skills) skillRewardInputError = exception.Message;
                else modificationRewardInputError = exception.Message;
                dirty[3] = hasUnsavedChanges = true;
                SetStatus(exception.Message, true);
                return;
            }
            if (skills) skillRewardInputError = null;
            else modificationRewardInputError = null;
            var fieldName = skills ? "skillRewards" : "modificationRewards";
            Change("levels.levels", target =>
            {
                for (var i = 0; i < counts.Length; i++)
                    target.GetArrayElementAtIndex(i).FindPropertyRelative(fieldName).intValue = counts[i];
            });
        }

        private void RefreshLevelRewardFields()
        {
            if (skillRewardInputError == null) _skillRewardLevelField?.SetValueWithoutNotify(FormatRewardLevels(true));
            if (modificationRewardInputError == null) _modificationRewardLevelField?.SetValueWithoutNotify(FormatRewardLevels(false));
            var levels = document.levels.levels;
            if (_levelSkillRewardLabels == null || _levelModificationRewardLabels == null) return;
            var count = Mathf.Min(levels.Length, _levelSkillRewardLabels.Length);
            for (var i = 0; i < count; i++)
            {
                _levelSkillRewardLabels[i].text = levels[i].skillRewards.ToString();
                _levelModificationRewardLabels[i].text = levels[i].modificationRewards.ToString();
            }
        }

        private void ValidateLevelRewardInputs()
        {
            if (skillRewardInputError != null) throw new ArgumentException(skillRewardInputError);
            if (modificationRewardInputError != null) throw new ArgumentException(modificationRewardInputError);
        }

        private void ResetLevelRewardInputs()
        {
            skillRewardLevelInput = modificationRewardLevelInput = null;
            skillRewardInputError = modificationRewardInputError = null;
        }
    }
}
