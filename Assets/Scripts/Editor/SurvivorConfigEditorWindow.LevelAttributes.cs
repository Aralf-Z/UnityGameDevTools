using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private static readonly LevelAttribute[] _levelAttributes =
        {
            new("maxHealthGain", "baseMaxHealth", "最大生命值", catalog => catalog.baseMaxHealth, level => level.maxHealthGain, 0.01f, 10000, 10000000),
            new("healthRegenerationGain", "healthRegenerationPerSecond", "每秒生命恢复", catalog => catalog.healthRegenerationPerSecond, level => level.healthRegenerationGain, 0, 10000, 10000),
            new("baseDamageGain", "baseDamage", "基础伤害", catalog => catalog.baseDamage, level => level.baseDamageGain, 0, 100000, 10000000)
        };

        private double[] ReadLevelAttributeTotals(LevelAttribute attribute)
        {
            var levels = document.levels.levels;
            var result = new double[levels.Length];
            double current = attribute.getBase(document.levels);
            for (var i = 0; i < levels.Length; i++)
            {
                current += attribute.getGain(levels[i]);
                result[i] = current;
            }
            return result;
        }

        private LevelFormulaValues ConvertLevelAttributeTotals(LevelAttribute attribute, double[] totals, bool replaceBase)
        {
            var initial = replaceBase ? totals[0] : attribute.getBase(document.levels);
            if (initial < attribute.minimum || initial > attribute.maximumBase)
                throw new ArgumentException(attribute.label + "的基础值超出允许范围。");
            var result = new LevelFormulaValues(attribute.gainField, totals.Length, false, 0, attribute.baseField, (float)initial);
            var previous = initial;
            for (var i = 0; i < totals.Length; i++)
            {
                var value = totals[i];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < attribute.minimum)
                    throw new ArgumentException("lv=" + (i + 1) + " 的" + attribute.label + "总值无效。");
                var gain = value - previous;
                if (gain < 0 || gain > attribute.maximumGain)
                    throw new ArgumentException("lv=" + (i + 1) + " 的" + attribute.label + "不能低于前一级, 相邻增量不能超过 " + attribute.maximumGain + "。请调整总值或公式范围。");
                result.values[i] = gain;
                previous = value;
            }
            return result;
        }

        private void SetLevelAttributeTotal(LevelAttribute attribute, int index, float value, FloatField field)
        {
            try
            {
                var totals = ReadLevelAttributeTotals(attribute);
                if (!SurvivorArena.IsFinite(value) || value < attribute.minimum) throw new ArgumentException(attribute.label + "总值无效。");
                if (index > 0 && value < (float)totals[index - 1])
                    throw new ArgumentException("第 " + (index + 1) + " 级的" + attribute.label + "不能低于前一级。");
                if (index + 1 < totals.Length && value > (float)totals[index + 1])
                    throw new ArgumentException("第 " + (index + 1) + " 级的" + attribute.label + "不能高于后一级, 请先调整后续等级或使用公式批量修改。");
                totals[index] = value;
                var result = ConvertLevelAttributeTotals(attribute, totals, index == 0);
                Change("levels", target => WriteLevelFormulaValues(target, result));
            }
            catch (Exception exception)
            {
                field.SetValueWithoutNotify((float)ReadLevelAttributeTotals(attribute)[index]);
                SetStatus(exception.Message, true);
            }
        }

        private void RefreshLevelAttributeFields()
        {
            if (_levelAttributeFields == null) return;
            for (var column = 0; column < _levelAttributes.Length; column++)
            {
                var totals = ReadLevelAttributeTotals(_levelAttributes[column]);
                var fields = _levelAttributeFields[column];
                if (fields == null) continue;
                var count = Mathf.Min(totals.Length, fields.Length);
                for (var i = 0; i < count; i++) fields[i]?.SetValueWithoutNotify((float)totals[i]);
            }
        }

        private sealed class LevelAttribute
        {
            public readonly string gainField, baseField, label;
            public readonly Func<SurvivorLevelCatalog, float> getBase;
            public readonly Func<SurvivorLevelDefinition, float> getGain;
            public readonly float minimum, maximumGain, maximumBase;

            public LevelAttribute(string gainField, string baseField, string label, Func<SurvivorLevelCatalog, float> getBase, Func<SurvivorLevelDefinition, float> getGain, float minimum, float maximumGain, float maximumBase)
            {
                this.gainField = gainField;
                this.baseField = baseField;
                this.label = label;
                this.getBase = getBase;
                this.getGain = getGain;
                this.minimum = minimum;
                this.maximumGain = maximumGain;
                this.maximumBase = maximumBase;
            }
        }
    }
}
