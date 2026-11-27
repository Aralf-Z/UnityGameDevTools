using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private readonly Dictionary<int, VisualElement> _levelRows = new();
        private ScrollView _levelTable;
        private Label[] _levelSkillRewardLabels, _levelModificationRewardLabels;
        private FloatField[][] _levelAttributeFields;

        private void LoadLevelFile()
        {
            try
            {
                rawJson[3] = File.ReadAllText(SurvivorConfiguration.FilePath("Level.json"));
                document.levels = SurvivorLevels.Parse(rawJson[3]);
                ResetLevelRewardInputs();
                loadErrors[3] = null;
            }
            catch (Exception exception)
            {
                document.levels = null;
                loadErrors[3] = exception.Message;
            }
        }

        private void BuildLevelContent()
        {
            AddNumber(_content, "基础最大生命值", "levels.baseMaxHealth");
            AddNumber(_content, "基础每秒生命恢复", "levels.healthRegenerationPerSecond");
            AddNumber(_content, "基础伤害", "levels.baseDamage");
            DrawLevelRewardInputs();
            DrawLevelFormulas();
            _content.Add(new HelpBox("表格显示各等级的总值: 最大生命值、每秒生命恢复和基础伤害。应用上方公式会直接更新本表, 也可逐级修改总值。保存时自动换算为 JSON 所需的基础值和逐级增量。范围外的总值保持不变, 属性总值不能随等级下降。升级不会立即回血, 技能伤害 = 基础伤害 × 技能倍率。", HelpBoxMessageType.Info));
            DrawLevelTable();
            _content.Add(new HelpBox("保存后更新经验需求和等级上限。已获得的技能、改造与待选择奖励保留, 后续升级使用新配置。", HelpBoxMessageType.Info));
            AddPreview("等级配置概览");
        }

        private void DrawLevelTable()
        {
            var count = new IntegerField("最高等级") { value = document.levels.MaxLevel, isDelayed = true };
            count.RegisterValueChangedCallback(evt =>
            {
                try { SetLevelCount(evt.newValue); }
                catch (Exception exception)
                {
                    count.SetValueWithoutNotify(document.levels.MaxLevel);
                    SetStatus(exception.Message, true);
                }
            });
            _content.Add(count);
            var jump = new IntegerField("定位等级") { value = 1, isDelayed = true };
            jump.RegisterValueChangedCallback(evt =>
            {
                var level = Mathf.Clamp(evt.newValue, 1, document.levels.MaxLevel);
                jump.SetValueWithoutNotify(level);
                if (_levelRows.TryGetValue(level, out var row)) _levelTable.ScrollTo(row);
            });
            _content.Add(jump);
            _content.Add(LevelTableHeader());
            _levelTable = new ScrollView { style = { maxHeight = 420, minHeight = 160, marginBottom = 8 } };
            _content.Add(_levelTable);
            _levelRows.Clear();
            var levels = document.levels.levels;
            _levelSkillRewardLabels = new Label[levels.Length];
            _levelModificationRewardLabels = new Label[levels.Length];
            _levelAttributeFields = new FloatField[_levelAttributes.Length][];
            var totals = new double[_levelAttributes.Length][];
            for (var i = 0; i < _levelAttributes.Length; i++)
            {
                _levelAttributeFields[i] = new FloatField[levels.Length];
                totals[i] = ReadLevelAttributeTotals(_levelAttributes[i]);
            }
            for (var i = 0; i < levels.Length; i++) AddLevelTableRow(i, totals);
        }

        private static VisualElement LevelTableHeader()
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6, marginBottom = 4, paddingRight = 15 } };
            header.Add(LevelTableLabel("等级", 42));
            foreach (var title in new[] { "累计经验", "最大生命值", "每秒生命恢复", "基础伤害" })
                header.Add(new Label(title) { style = { flexGrow = 1, flexBasis = 0, minWidth = 45 } });
            header.Add(LevelTableLabel("技能", 36));
            header.Add(LevelTableLabel("改造", 36));
            return header;
        }

        private void AddLevelTableRow(int index, double[][] totals)
        {
            var definition = document.levels.levels[index];
            var path = "levels.levels.Array.data[" + index + "]";
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, minHeight = 24 } };
            row.Add(LevelTableLabel(definition.level.ToString(), 42));
            var experience = new IntegerField { isDelayed = true, tooltip = "到达 " + definition.level + " 级所需的累计总经验" };
            experience.BindProperty(_serialized.FindProperty(path + ".totalExperience"));
            experience.SetEnabled(index != 0);
            AddLevelTableField(row, experience);
            for (var column = 0; column < _levelAttributes.Length; column++)
            {
                var attribute = _levelAttributes[column];
                var field = new FloatField { value = (float)totals[column][index], isDelayed = true, tooltip = "到达 " + definition.level + " 级时的" + attribute.label + "总值" };
                field.RegisterValueChangedCallback(evt => SetLevelAttributeTotal(attribute, index, evt.newValue, field));
                _levelAttributeFields[column][index] = field;
                AddLevelTableField(row, field);
            }
            _levelSkillRewardLabels[index] = LevelTableLabel(definition.skillRewards.ToString(), 36);
            _levelModificationRewardLabels[index] = LevelTableLabel(definition.modificationRewards.ToString(), 36);
            row.Add(_levelSkillRewardLabels[index]);
            row.Add(_levelModificationRewardLabels[index]);
            _levelRows.Add(definition.level, row);
            _levelTable.Add(row);
        }

        private static Label LevelTableLabel(string text, float width)
        {
            return new Label(text) { style = { width = width, flexShrink = 0, unityTextAlign = TextAnchor.MiddleCenter } };
        }

        private static void AddLevelTableField(VisualElement row, VisualElement field)
        {
            field.style.flexGrow = 1;
            field.style.flexBasis = 0;
            field.style.minWidth = 45;
            field.style.marginLeft = field.style.marginRight = 1;
            row.Add(field);
        }

        private void SetLevelCount(int count)
        {
            if (count < 1 || count > 1000) throw new ArgumentException("最高等级必须在 1–1000 之间。");
            var current = document.levels.MaxLevel;
            if (count == current) return;
            var lastExperience = document.levels.levels.Last().totalExperience;
            if (count > current && (long)lastExperience + (count - current) * 100L > int.MaxValue)
                throw new ArgumentException("新增等级的累计经验超出整数范围。");
            Change("levels.levels", target =>
            {
                if (count < current) target.arraySize = count;
                for (var i = current; i < count; i++)
                {
                    target.InsertArrayElementAtIndex(i);
                    var item = target.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("level").intValue = i + 1;
                    item.FindPropertyRelative("totalExperience").intValue = lastExperience + (i - current + 1) * 100;
                    item.FindPropertyRelative("skillRewards").intValue = 0;
                    item.FindPropertyRelative("modificationRewards").intValue = 0;
                }
            });
            if (skillRewardInputError != null) ApplyRewardLevelInput(skillRewardLevelInput, true);
            if (modificationRewardInputError != null) ApplyRewardLevelInput(modificationRewardLevelInput, false);
            QueueBuild();
        }

        private void RefreshLevelPreview()
        {
            RefreshLevelRewardFields();
            RefreshLevelAttributeFields();
            RefreshLevelFormulaStatus();
            var levels = document.levels;
            if (_preview != null)
                _preview.text = "最高等级: " + levels.MaxLevel + "\n最高等级累计经验: " + levels.levels.Last().totalExperience + "\n技能获取次数: " + levels.levels.Sum(level => level.skillRewards) + "\n改造获取次数: " + levels.levels.Sum(level => level.modificationRewards);
        }
    }
}
