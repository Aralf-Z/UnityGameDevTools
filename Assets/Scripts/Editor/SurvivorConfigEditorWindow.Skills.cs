using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private void AddText(string label, string path, bool multiline = false)
        {
            var field = new TextField(label) { multiline = multiline };
            if (multiline) field.style.minHeight = 65;
            field.BindProperty(_serialized.FindProperty(path));
            _content.Add(field);
        }
        private void AddToggle(string label, string path) { var field = new Toggle(label); field.BindProperty(_serialized.FindProperty(path)); _content.Add(field); }
        private void AddNumber(VisualElement parent, string label, string path, bool integer = false)
        {
            var property = _serialized.FindProperty(path);
            if (integer && property.propertyType == SerializedPropertyType.Integer)
            { var field = new IntegerField(label); field.BindProperty(property); parent.Add(field); }
            else if (integer)
            {
                var field = new IntegerField(label) { value = Mathf.RoundToInt(property.floatValue) };
                field.RegisterValueChangedCallback(evt => Change(path, target => target.floatValue = evt.newValue));
                parent.Add(field);
            }
            else { var field = new FloatField(label); field.BindProperty(property); parent.Add(field); }
        }

        private void AddColor(string label, string path, bool inherited)
        {
            var value = _serialized.FindProperty(path).stringValue;
            ColorUtility.TryParseHtmlString(value, out var color);
            var field = new ColorField(label) { value = string.IsNullOrEmpty(value) ? Color.white : color };
            field.RegisterValueChangedCallback(evt => Change(path, target => target.stringValue = "#" + ColorUtility.ToHtmlStringRGBA(evt.newValue)));
            _content.Add(field);
            if (inherited)
            {
                _content.Add(new Label(string.IsNullOrEmpty(value) ? "当前使用基础颜色。调整色块即可覆盖。" : "当前覆盖颜色。") { style = { opacity = 0.7f } });
                _content.Add(new Button(() => { Change(path, target => target.stringValue = ""); QueueBuild(); }) { text = "使用基础颜色" });
            }
        }

        private void DrawOverrides(string path, SkillKind kind)
        {
            var section = new Foldout { text = "覆盖数值", value = true };
            _content.Add(section);
            var parameters = SurvivorSkills.Parameters(kind);
            var labels = parameters.Select(parameter => SurvivorSkills.Label(parameter).Replace('/', '／')).ToList();
            var array = _serialized.FindProperty(path);
            for (var i = 0; i < array.arraySize; i++)
            {
                var index = i;
                var itemPath = path + ".Array.data[" + i + "]";
                var parameterPath = itemPath + ".parameter";
                var name = _serialized.FindProperty(parameterPath).stringValue;
                Enum.TryParse(name, out SurvivorSkillParameter parameter);
                var row = new VisualElement { style = { marginBottom = 8, paddingLeft = 8 } };
                var selector = new DropdownField("数值项目", labels, Mathf.Max(0, Array.IndexOf(parameters, parameter)));
                selector.RegisterValueChangedCallback(_ => { Change(parameterPath, target => target.stringValue = parameters[selector.index].ToString()); QueueBuild(); });
                row.Add(selector);
                var operations = new[] { "Set", "Add", "Multiply" };
                var operationPath = itemPath + ".operation";
                var operation = _serialized.FindProperty(operationPath).stringValue;
                var mode = new DropdownField("修改方式", new List<string> { "设定为", "增加", "乘以倍率" }, Mathf.Max(0, Array.IndexOf(operations, operation)));
                mode.RegisterValueChangedCallback(_ => { Change(operationPath, target => target.stringValue = operations[mode.index]); QueueBuild(); });
                row.Add(mode);
                AddNumber(row, operation == "Multiply" ? "倍率" : operation == "Add" ? "增加量" : "目标值", itemPath + ".value", SurvivorSkills.IsCount(parameter) && operation != "Multiply");
                row.Add(new Button(() => { Change(path, target => target.DeleteArrayElementAtIndex(index)); QueueBuild(); }) { text = "移除此覆盖" });
                section.Add(row);
            }
            section.Add(new Button(() =>
            {
                var used = new HashSet<string>();
                for (var i = 0; i < array.arraySize; i++) used.Add(array.GetArrayElementAtIndex(i).FindPropertyRelative("parameter").stringValue);
                var available = parameters.Where(parameter => !used.Contains(parameter.ToString())).ToArray();
                if (available.Length == 0) return;
                var parameter = available[0];
                float value;
                try
                {
                    document.catalog.Rebuild();
                    value = document.catalog.Resolve(kind, 0).Get(parameter);
                }
                catch (Exception exception) { SetStatus(exception.Message, true); return; }
                Change(path, target =>
                {
                    var index = target.arraySize;
                    target.InsertArrayElementAtIndex(index);
                    var item = target.GetArrayElementAtIndex(index);
                    item.FindPropertyRelative("parameter").stringValue = parameter.ToString();
                    item.FindPropertyRelative("operation").stringValue = "Set";
                    item.FindPropertyRelative("value").floatValue = value;
                });
                QueueBuild();
            }) { text = "添加数值覆盖" });
        }

        private void DrawDebuffs(string path, bool overrides)
        {
            var section = new Foldout { text = overrides ? "Debuff 改造 / 覆盖" : "基础 Debuff", value = true };
            _content.Add(section);
            var array = _serialized.FindProperty(path);
            var labels = new List<string> { "冻结", "减速", "流血" };
            for (var i = 0; i < array.arraySize; i++)
            {
                var index = i;
                var itemPath = path + ".Array.data[" + i + "]";
                var type = _serialized.FindProperty(itemPath + ".type").stringValue;
                var row = new VisualElement { style = { paddingLeft = 8, marginBottom = 12 } };
                var selector = new DropdownField("效果", labels, Mathf.Max(0, Array.IndexOf(SurvivorSkills.DebuffTypes, type)));
                selector.RegisterValueChangedCallback(_ => { Change(itemPath + ".type", target => target.stringValue = SurvivorSkills.DebuffTypes[selector.index]); QueueBuild(); });
                row.Add(selector);
                var enabled = new Toggle(overrides ? "施加（关闭则移除已有同类效果）" : "施加 Debuff");
                enabled.BindProperty(_serialized.FindProperty(itemPath + ".enabled"));
                row.Add(enabled);
                AddNumber(row, "持续时间（秒）", itemPath + ".duration");
                if (type == "Slow") AddNumber(row, "减速后移动倍率（0–1）", itemPath + ".moveSpeedMultiplier");
                if (type == "Bleed")
                {
                    AddNumber(row, "扣血间隔（秒）", itemPath + ".tickInterval");
                    var percentPath = itemPath + ".currentHealthPercent";
                    var percent = new FloatField("每次扣当前生命（%）") { value = _serialized.FindProperty(percentPath).floatValue * 100 };
                    percent.RegisterValueChangedCallback(evt => Change(percentPath, target => target.floatValue = evt.newValue / 100));
                    row.Add(percent);
                }
                if (type == "Freeze")
                {
                    var help = SelectedSkillDefinition.id == "Frost"
                        ? "领域会持续刷新冻结；冻结不能超过领域的结束时间。"
                        : "命中后冻结敌人，使其停止移动和攻击；重复命中刷新持续时间。";
                    row.Add(new Label(help) { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.7f } });
                }
                row.Add(new Button(() => { Change(path, target => target.DeleteArrayElementAtIndex(index)); QueueBuild(); }) { text = "移除此项" });
                section.Add(row);
            }
            section.Add(new Button(() =>
            {
                var used = new HashSet<string>();
                for (var i = 0; i < array.arraySize; i++) used.Add(array.GetArrayElementAtIndex(i).FindPropertyRelative("type").stringValue);
                var type = SurvivorSkills.DebuffTypes.FirstOrDefault(item => !used.Contains(item));
                if (type == null) return;
                Change(path, target =>
                {
                    var index = target.arraySize;
                    target.InsertArrayElementAtIndex(index);
                    var item = target.GetArrayElementAtIndex(index);
                    item.FindPropertyRelative("type").stringValue = type;
                    item.FindPropertyRelative("enabled").boolValue = true;
                    item.FindPropertyRelative("duration").floatValue = 3;
                    item.FindPropertyRelative("tickInterval").floatValue = 1;
                    item.FindPropertyRelative("currentHealthPercent").floatValue = 0.05f;
                    item.FindPropertyRelative("moveSpeedMultiplier").floatValue = 0.25f;
                });
                QueueBuild();
            }) { text = "添加 Debuff" });
        }

    }
}
