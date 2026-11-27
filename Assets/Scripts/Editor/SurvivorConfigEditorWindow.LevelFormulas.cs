using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        [SerializeField] private string experienceFormula = "(lv - 1) * (5 * lv + 6) / 2";
        [SerializeField] private string maxHealthTotalFormula = "100 + 8 * (lv - 1)";
        [SerializeField] private string regenerationTotalFormula = "1";
        [SerializeField] private string baseDamageTotalFormula = "10 + 0.8 * (lv - 1)";
        [SerializeField] private bool applyExperienceFormula = true;
        [SerializeField] private bool applyMaxHealthGrowthFormula = true, applyRegenerationGrowthFormula = true, applyDamageGrowthFormula = true;
        [SerializeField] private int formulaFromLevel = 1, formulaToLevel;
        private HelpBox _formulaStatus;

        private void DrawLevelFormulas()
        {
            var section = new Foldout { text = "等级成长公式（仅编辑器）", value = true };
            _content.Add(section);
            experienceFormula = NormalizeFormulaParameter(experienceFormula);
            maxHealthTotalFormula = NormalizeFormulaParameter(maxHealthTotalFormula);
            regenerationTotalFormula = NormalizeFormulaParameter(regenerationTotalFormula);
            baseDamageTotalFormula = NormalizeFormulaParameter(baseDamageTotalFormula);
            AddLevelFormulaField(section, "累计总经验", experienceFormula, applyExperienceFormula, value => experienceFormula = value, value => applyExperienceFormula = value);
            AddLevelFormulaField(section, "等级最大生命值", maxHealthTotalFormula, applyMaxHealthGrowthFormula, value => maxHealthTotalFormula = value, value => applyMaxHealthGrowthFormula = value);
            AddLevelFormulaField(section, "等级每秒生命恢复", regenerationTotalFormula, applyRegenerationGrowthFormula, value => regenerationTotalFormula = value, value => applyRegenerationGrowthFormula = value);
            AddLevelFormulaField(section, "等级基础伤害", baseDamageTotalFormula, applyDamageGrowthFormula, value => baseDamageTotalFormula = value, value => applyDamageGrowthFormula = value);
            section.Add(new HelpBox("参数为 lv, 所有公式都计算该等级的总值。例如最大生命 100 + 8 * (lv - 1)、每秒恢复 1 + 0.1 * (lv - 1)、基础伤害 10 + 0.8 * (lv - 1)。属性总值会自动换算为相邻等级的增量写入 JSON; 包含 1 级时同时更新基础值。范围外的总值保留, 相邻增量自动重算, 总值不能随等级下降。经验仍保存累计总值并四舍五入, 属性保留小数。技能伤害 = 当前基础伤害 × 技能伤害倍率。公式仅在编辑器计算。", HelpBoxMessageType.Info));
            DrawFormulaRange(section);
            var apply = new Button(ApplyLevelFormulas) { text = "应用勾选公式并更新下方等级表" };
            section.Add(apply);
            _formulaStatus = new HelpBox("", HelpBoxMessageType.Error);
            section.Add(_formulaStatus);
            RefreshLevelFormulaStatus();
        }

        private static string NormalizeFormulaParameter(string expression)
        {
            return Regex.Replace(expression ?? "", @"\blevel\b", "lv", RegexOptions.IgnoreCase);
        }

        private void AddLevelFormulaField(VisualElement parent, string label, string expression, bool enabled, Action<string> setExpression, Action<bool> setEnabled)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var toggle = new Toggle { value = enabled, tooltip = "勾选后批量生成" + label };
            toggle.style.width = 22;
            toggle.style.flexShrink = 0;
            toggle.RegisterValueChangedCallback(evt => { setEnabled(evt.newValue); RefreshLevelFormulaStatus(); });
            row.Add(toggle);
            var field = new TextField(label) { value = expression };
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.RegisterValueChangedCallback(evt =>
            {
                setExpression(evt.newValue);
                setEnabled(true);
                toggle.SetValueWithoutNotify(true);
                RefreshLevelFormulaStatus();
            });
            row.Add(field);
            parent.Add(row);
        }

        private void DrawFormulaRange(VisualElement parent)
        {
            var max = document.levels.MaxLevel;
            formulaFromLevel = Mathf.Clamp(formulaFromLevel, 1, max);
            if (formulaToLevel == 0) formulaToLevel = max;
            formulaToLevel = Mathf.Clamp(formulaToLevel, formulaFromLevel, max);
            var from = new IntegerField("应用起始等级") { value = formulaFromLevel, isDelayed = true };
            from.RegisterValueChangedCallback(evt => { formulaFromLevel = evt.newValue; RefreshLevelFormulaStatus(); });
            parent.Add(from);
            var to = new IntegerField("应用结束等级") { value = formulaToLevel, isDelayed = true };
            to.RegisterValueChangedCallback(evt => { formulaToLevel = evt.newValue; RefreshLevelFormulaStatus(); });
            parent.Add(to);
        }

        private List<LevelFormulaValues> CalculateLevelFormulas()
        {
            var levels = document.levels.levels;
            if (formulaFromLevel < 1 || formulaToLevel < formulaFromLevel || formulaToLevel > levels.Length)
                throw new ArgumentException("公式应用范围必须位于 1–" + levels.Length + " 级之间。");
            var values = new List<LevelFormulaValues>();
            if (applyExperienceFormula) values.Add(CalculateExperienceFormula());
            if (applyMaxHealthGrowthFormula) values.Add(CalculateTotalAttributeFormula(_levelAttributes[0], maxHealthTotalFormula));
            if (applyRegenerationGrowthFormula) values.Add(CalculateTotalAttributeFormula(_levelAttributes[1], regenerationTotalFormula));
            if (applyDamageGrowthFormula) values.Add(CalculateTotalAttributeFormula(_levelAttributes[2], baseDamageTotalFormula));
            if (values.Count == 0) throw new ArgumentException("请至少勾选一个需要生成的公式。");
            return values;
        }

        private LevelFormulaValues CalculateExperienceFormula()
        {
            var levels = document.levels.levels;
            var result = new LevelFormulaValues("totalExperience", formulaToLevel - formulaFromLevel + 1, true, formulaFromLevel - 1);
            var previous = 0;
            for (var i = 0; i < levels.Length; i++)
            {
                var lv = i + 1;
                var value = levels[i].totalExperience;
                if (lv >= formulaFromLevel && lv <= formulaToLevel)
                {
                    value = SurvivorLevelFormula.EvaluateExperience(experienceFormula, lv);
                    result.values[lv - formulaFromLevel] = value;
                }
                if (i == 0 && value != 0) throw new ArgumentException("1 级累计经验必须为 0。");
                if (i > 0 && value <= previous) throw new ArgumentException("lv=" + lv + " 的累计经验必须高于前一级, 请调整公式或应用范围。");
                previous = value;
            }
            return result;
        }

        private LevelFormulaValues CalculateTotalAttributeFormula(LevelAttribute attribute, string expression)
        {
            var totals = ReadLevelAttributeTotals(attribute);
            for (var lv = formulaFromLevel; lv <= formulaToLevel; lv++)
                totals[lv - 1] = SurvivorLevelFormula.Evaluate(expression, lv);
            return ConvertLevelAttributeTotals(attribute, totals, formulaFromLevel == 1);
        }

        private void RefreshLevelFormulaStatus()
        {
            if (_formulaStatus == null || document.levels == null) return;
            try
            {
                CalculateLevelFormulas();
                _formulaStatus.style.display = DisplayStyle.None;
            }
            catch (Exception exception)
            {
                _formulaStatus.text = exception.Message;
                _formulaStatus.style.display = DisplayStyle.Flex;
            }
        }

        private void ApplyLevelFormulas()
        {
            try
            {
                var results = CalculateLevelFormulas();
                if (results.Count == 0) return;
                Change("levels", target =>
                {
                    foreach (var result in results) WriteLevelFormulaValues(target, result);
                });
                RefreshLevelAttributeFields();
            }
            catch (Exception exception) { SetStatus("公式未应用: " + exception.Message, true); }
        }

        private static void WriteLevelFormulaValues(SerializedProperty target, LevelFormulaValues result)
        {
            var rows = target.FindPropertyRelative("levels");
            if (result.baseFieldName != null) target.FindPropertyRelative(result.baseFieldName).floatValue = result.baseValue;
            for (var i = 0; i < result.values.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(result.startIndex + i);
                var property = row.FindPropertyRelative(result.fieldName);
                if (result.integer) property.intValue = (int)result.values[i];
                else property.floatValue = (float)result.values[i];
            }
        }

        private sealed class LevelFormulaValues
        {
            public readonly string fieldName;
            public readonly bool integer;
            public readonly double[] values;
            public readonly int startIndex;
            public readonly string baseFieldName;
            public readonly float baseValue;

            public LevelFormulaValues(string fieldName, int count, bool integer = false, int startIndex = 0, string baseFieldName = null, float baseValue = 0)
            {
                this.fieldName = fieldName;
                this.integer = integer;
                this.startIndex = startIndex;
                this.baseFieldName = baseFieldName;
                this.baseValue = baseValue;
                values = new double[count];
            }
        }
    }
}
