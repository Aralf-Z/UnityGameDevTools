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
    internal sealed class SurvivorSkillEditorDocument : ScriptableObject
    {
        public SurvivorSkillCatalog catalog;
    }

    public sealed class SurvivorSkillEditorWindow : EditorWindow
    {
        [SerializeField] private SurvivorSkillEditorDocument _document;
        [SerializeField] private int _selectedSkill, _selectedTier;
        [SerializeField] private string _rawJson;
        [SerializeField] private bool _rawMode, _rawDirty;
        [SerializeField] private string _loadError;
        private SerializedObject _serialized;
        private ScrollView _content;
        private DropdownField _skillPicker;
        private HelpBox _status;
        private Label _preview;
        private readonly List<ToolbarButton> _tierButtons = new();
        private string FilePath => Path.Combine(Application.dataPath, SurvivorSkills.AssetPath.Substring("Assets/".Length));

        [MenuItem("Demo/技能配置", false, 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<SurvivorSkillEditorWindow>();
            window.titleContent = new GUIContent("Demo 技能配置");
            window.minSize = new Vector2(600, 520);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "技能配置有未保存的修改。是否保存到 Resources 中的 Skills.json？";
            Undo.undoRedoPerformed += OnUndoRedo;
            if (_document == null) LoadFile();
            else
            {
                _document.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                _serialized = new SerializedObject(_document);
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            rootVisualElement.Unbind();
            _serialized?.Dispose();
            _serialized = null;
        }

        private void OnDestroy() { if (_document != null) DestroyImmediate(_document); }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 8;
            root.style.paddingTop = root.style.paddingBottom = 6;
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(SaveChanges) { text = "保存 JSON" });
            toolbar.Add(new ToolbarButton(() => { LoadFile(); CreateGUI(); }) { text = "重新载入", tooltip = "从磁盘载入，放弃未保存的修改" });
            toolbar.Add(new ToolbarButton(() => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(SurvivorSkills.AssetPath))) { text = "定位 JSON" });
            var raw = new ToolbarToggle { text = "原始 JSON", value = _rawMode };
            raw.RegisterValueChangedCallback(evt => { _rawMode = evt.newValue; BuildContent(); });
            toolbar.Add(raw);
            root.Add(toolbar);
            root.Add(new Label(SurvivorSkills.AssetPath) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 5, marginBottom = 5 } });
            _status = new HelpBox("保存后立即同步到运行中的对局。T3 默认预留关闭。", HelpBoxMessageType.Info);
            if (!string.IsNullOrEmpty(_loadError)) { _status.text = _loadError; _status.messageType = HelpBoxMessageType.Error; }
            root.Add(_status);
            _skillPicker = new DropdownField("技能", SkillNames(), Mathf.Clamp(_selectedSkill, 0, 3));
            _skillPicker.RegisterValueChangedCallback(_ => { _selectedSkill = _skillPicker.index; BuildContent(); });
            root.Add(_skillPicker);
            var tabs = new Toolbar();
            _tierButtons.Clear();
            for (var i = 0; i <= 3; i++)
            {
                var tier = i;
                var button = new ToolbarButton(() => { _selectedTier = tier; BuildContent(); }) { text = i == 0 ? "基础技能" : "T" + i };
                _tierButtons.Add(button);
                tabs.Add(button);
            }
            root.Add(tabs);
            _content = new ScrollView { style = { flexGrow = 1, paddingTop = 8 } };
            root.Add(_content);
            root.UnregisterCallback<SerializedPropertyChangeEvent>(OnPropertyChanged);
            root.RegisterCallback<SerializedPropertyChangeEvent>(OnPropertyChanged);
            BuildContent();
        }

        private List<string> SkillNames() => _document?.catalog?.skills?.Select(skill => skill.displayName.Replace('/', '／') + "（" + skill.id + "）").ToList()
            ?? new List<string> { "Bolt", "Orbit", "Nova", "Frost" };

        private void LoadFile()
        {
            rootVisualElement.Unbind();
            _serialized?.Dispose();
            if (_document == null) _document = CreateInstance<SurvivorSkillEditorDocument>();
            _document.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            try
            {
                _rawJson = File.ReadAllText(FilePath, Encoding.UTF8);
                _document.catalog = SurvivorSkills.Parse(_rawJson);
                _rawDirty = false;
                _loadError = null;
                hasUnsavedChanges = false;
            }
            catch (Exception exception)
            {
                _document.catalog = null;
                _loadError = exception.Message;
                _rawMode = true;
                if (_status != null) SetStatus(exception.Message, true);
            }
            _serialized = new SerializedObject(_document);
        }

        public override void SaveChanges()
        {
            try
            {
                if (_rawDirty && !ApplyRawJson()) return;
                if (_document?.catalog == null) throw new InvalidOperationException("请先载入有效的技能 JSON。");
                _document.catalog.Rebuild();
                var json = JsonUtility.ToJson(_document.catalog, true) + "\n";
                File.WriteAllText(FilePath, json, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(SurvivorSkills.AssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var arena = SurvivorArena.Instance;
                if (EditorApplication.isPlaying && arena != null) arena.ApplySkillConfiguration(json);
                _rawJson = json;
                _rawDirty = false;
                hasUnsavedChanges = false;
                base.SaveChanges();
                SetStatus(EditorApplication.isPlaying && arena != null ? "已保存并实时应用到当前对局。" : "已保存。运行游戏时将读取此配置。", false);
            }
            catch (Exception exception) { SetStatus("无法保存：" + exception.Message, true); }
        }

        public override void DiscardChanges() { LoadFile(); base.DiscardChanges(); if (_content != null) CreateGUI(); }

        private bool ApplyRawJson()
        {
            try
            {
                var catalog = SurvivorSkills.Parse(_rawJson);
                Undo.RecordObject(_document, "应用技能 JSON");
                _document.catalog = catalog;
                _loadError = null;
                _serialized.Update();
                _rawDirty = false;
                MarkChanged();
                return true;
            }
            catch (Exception exception) { SetStatus(exception.Message, true); return false; }
        }

        private void BuildContent()
        {
            if (_content == null) return;
            _content.Unbind();
            _content.Clear();
            _preview = null;
            for (var i = 0; i < _tierButtons.Count; i++) _tierButtons[i].SetEnabled(i != _selectedTier);
            if (_rawMode || _document?.catalog == null || _rawDirty)
            {
                if (!_rawDirty && _document?.catalog != null) _rawJson = JsonUtility.ToJson(_document.catalog, true);
                var text = new TextField("JSON") { multiline = true, value = _rawJson ?? "" };
                text.style.minHeight = 360;
                text.RegisterValueChangedCallback(evt => { _rawJson = evt.newValue; _rawDirty = true; hasUnsavedChanges = true; });
                _content.Add(text);
                _content.Add(new Button(() => { if (ApplyRawJson()) { _rawMode = false; CreateGUI(); } }) { text = "应用 JSON 并返回表单" });
                return;
            }
            _serialized.Update();
            _selectedSkill = Mathf.Clamp(_selectedSkill, 0, _document.catalog.skills.Length - 1);
            _selectedTier = Mathf.Clamp(_selectedTier, 0, 3);
            var definition = _document.catalog.skills[_selectedSkill];
            Enum.TryParse(definition.id, out SkillKind kind);
            var path = "catalog.skills.Array.data[" + _selectedSkill + "]";
            if (_selectedTier == 0)
            {
                AddToggle("启用技能", path + ".enabled");
                AddText("技能名称", path + ".displayName");
                AddText("基础描述", path + ".description", true);
                var stats = path + ".baseStats";
                foreach (var parameter in SurvivorSkills.Parameters(kind)) AddNumber(_content, SurvivorSkills.Label(parameter), stats + "." + SurvivorSkills.FieldName(parameter), SurvivorSkills.IsCount(parameter));
                AddColor("特效颜色", stats + ".effectColor", false);
                DrawDebuffs(stats + ".debuffs", false);
            }
            else
            {
                path += ".t" + _selectedTier;
                AddToggle("启用 T" + _selectedTier, path + ".enabled");
                AddText("强化名称", path + ".displayName");
                AddText("T" + _selectedTier + " 额外描述", path + ".additionalDescription", true);
                _content.Add(new HelpBox("未修改的数值和 Debuff 继承前一阶。数值支持设定、增加或倍率；关闭同类 Debuff 可移除继承效果。", HelpBoxMessageType.Info));
                AddColor("覆盖特效颜色", path + ".effectColor", true);
                DrawOverrides(path + ".overrides", kind);
                DrawDebuffs(path + ".debuffOverrides", true);
            }
            var preview = new Foldout { text = "继承后的最终效果", value = true };
            _preview = new Label { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 8, paddingBottom = 12 } };
            preview.Add(_preview);
            _content.Add(preview);
            RefreshPreview();
        }

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
                _content.Add(new Label(string.IsNullOrEmpty(value) ? "当前继承前一阶颜色。调整色块即可覆盖。" : "当前覆盖颜色。") { style = { opacity = 0.7f } });
                _content.Add(new Button(() => { Change(path, target => target.stringValue = ""); QueueBuild(); }) { text = "使用前一阶颜色" });
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
                var definition = _document.catalog.skills[_selectedSkill];
                float value;
                try
                {
                    _document.catalog.Rebuild();
                    value = _document.catalog.Resolve(kind, Mathf.Min(_selectedTier - 1, definition.MaxTier)).Get(parameter);
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
            var section = new Foldout { text = overrides ? "Debuff 强化 / 覆盖" : "基础 Debuff", value = true };
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
                var enabled = new Toggle(overrides ? "施加（关闭则移除继承效果）" : "施加 Debuff");
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
                    var help = _document.catalog.skills[_selectedSkill].id == "Frost"
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

        private void Change(string path, Action<SerializedProperty> update)
        {
            _serialized.Update();
            update(_serialized.FindProperty(path));
            _serialized.ApplyModifiedProperties();
            MarkChanged();
        }
        private void QueueBuild() => rootVisualElement.schedule.Execute(BuildContent);
        private void OnPropertyChanged(SerializedPropertyChangeEvent evt) => MarkChanged();
        private void MarkChanged() { hasUnsavedChanges = true; RefreshPreview(); }
        private void OnUndoRedo() { _serialized?.Update(); hasUnsavedChanges = true; if (_content != null) BuildContent(); }
        private void SetStatus(string text, bool error) { if (_status != null) { _status.text = text; _status.messageType = error ? HelpBoxMessageType.Error : HelpBoxMessageType.Info; } }

        private void RefreshPreview()
        {
            if (_document?.catalog == null || _rawDirty) return;
            try
            {
                _document.catalog.Rebuild();
                var definition = _document.catalog.skills[_selectedSkill];
                Enum.TryParse(definition.id, out SkillKind kind);
                var tier = Mathf.Min(_selectedTier, definition.MaxTier);
                var stats = _document.catalog.Resolve(kind, tier);
                var result = new StringBuilder(_document.catalog.Description(kind, tier));
                result.Append("\n\n").Append(_selectedTier > definition.MaxTier ? "此阶尚未启用，以下为当前可用最高阶：" : "最终数值：");
                foreach (var parameter in SurvivorSkills.Parameters(kind)) result.Append('\n').Append(SurvivorSkills.Label(parameter)).Append(" = ").Append(stats.Get(parameter).ToString("0.###"));
                foreach (var debuff in stats.debuffs.Where(item => item.enabled)) result.Append("\nDebuff: ").Append(debuff.type).Append(" / ").Append(debuff.duration).Append("s");
                if (_preview != null) _preview.text = result.ToString();
                SetStatus("配置有效。保存后立即同步运行中的对局；T3 可以保持关闭。", false);
                if (_skillPicker != null)
                {
                    var names = SkillNames();
                    _skillPicker.choices = names;
                    _skillPicker.SetValueWithoutNotify(names[_selectedSkill]);
                }
            }
            catch (Exception exception) { SetStatus(exception.Message, true); if (_preview != null) _preview.text = "请修正上方配置后查看最终效果。"; }
        }
    }

    internal sealed class SurvivorSkillConfigPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!EditorApplication.isPlaying || SurvivorArena.Instance == null || Array.IndexOf(imported, SurvivorSkills.AssetPath) < 0) return;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SurvivorSkills.AssetPath);
            if (asset == null) return;
            try { SurvivorArena.Instance.ApplySkillConfiguration(asset.text); }
            catch (Exception exception) { Debug.LogError("技能配置未应用，当前对局保留原配置：" + exception.Message); }
        }
    }
}
