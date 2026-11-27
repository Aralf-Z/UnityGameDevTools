using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    internal sealed class SurvivorConfigEditorDocument : ScriptableObject
    {
        public SurvivorSkillCatalog catalog;
        public SurvivorWaveCatalog waves;
        public SurvivorEnemyCatalog enemies;
        public SurvivorLevelCatalog levels;
        public SurvivorEnemySkillCatalog enemySkills;
    }

    public sealed partial class SurvivorConfigEditorWindow : EditorWindow
    {
        private static readonly string[] _assetPaths = { SurvivorSkills.AssetPath, SurvivorWaves.ASSET_PATH, SurvivorEnemies.ASSET_PATH, SurvivorLevels.ASSET_PATH, SurvivorEnemySkills.ASSET_PATH };
        private static readonly string[] _sectionNames = { "玩家技能", "波次", "怪物", "玩家等级", "怪物技能" };
        private static readonly int[] _sectionOrder = { 3, 0, 1, 2, 4 };

        [SerializeField] private SurvivorConfigEditorDocument document;
        [SerializeField] private int section = 3;
        [SerializeField] private int selectedSkill, selectedModification, selectedWave, selectedEnemy, selectedEnemySkill;
        [SerializeField] private string[] rawJson = new string[5];
        [SerializeField] private bool[] rawDirty = new bool[5];
        [SerializeField] private bool[] dirty = new bool[5];
        [SerializeField] private string[] loadErrors = new string[5];
        [SerializeField] private int previewModificationMask;
        [SerializeField] private bool rawMode;
        private SerializedObject _serialized;
        private ScrollView _content;
        private HelpBox _status;
        private Label _preview;
        private DropdownField _itemPicker;
        private int _previewWave = 1;

        [MenuItem("Demo/幸存者配置器", false, 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<SurvivorConfigEditorWindow>();
            window.titleContent = new GUIContent("幸存者配置器");
            window.minSize = new Vector2(640, 560);
            window.Show();
        }

        private void OnEnable()
        {
            var previousSections = rawJson.Length;
            Array.Resize(ref rawJson, _assetPaths.Length);
            Array.Resize(ref rawDirty, _assetPaths.Length);
            Array.Resize(ref dirty, _assetPaths.Length);
            Array.Resize(ref loadErrors, _assetPaths.Length);
            saveChangesMessage = "配置有未保存的修改。是否保存到 StreamingAssets/SurvivorDemo？";
            Undo.undoRedoPerformed += OnUndoRedo;
            if (document == null || document.catalog?.version != 3 || document.enemies?.version != 3 || document.levels?.version != 3 || document.enemySkills?.version != 1) LoadFiles();
            else
            {
                if (previousSections < _assetPaths.Length) LoadLevelFile();
                _serialized = new SerializedObject(document);
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            rootVisualElement.Unbind();
            _serialized?.Dispose();
            _serialized = null;
        }

        private void OnDestroy()
        {
            if (document != null) DestroyImmediate(document);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Unbind();
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 8;
            root.style.paddingTop = root.style.paddingBottom = 6;
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(SaveChanges) { text = "保存全部修改" });
            toolbar.Add(new ToolbarButton(() => { LoadFiles(); CreateGUI(); }) { text = "重新载入", tooltip = "从磁盘载入全部配置, 放弃未保存的修改" });
            toolbar.Add(new ToolbarButton(() => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(_assetPaths[section]))) { text = "定位 JSON" });
            var raw = new ToolbarToggle { text = "原始 JSON", value = rawMode };
            raw.RegisterValueChangedCallback(evt => { rawMode = evt.newValue; BuildContent(); });
            toolbar.Add(raw);
            root.Add(toolbar);
            var sections = new Toolbar();
            for (var i = 0; i < _sectionOrder.Length; i++)
            {
                var index = _sectionOrder[i];
                var button = new ToolbarButton(() => { section = index; CreateGUI(); }) { text = _sectionNames[index] };
                button.SetEnabled(index != section);
                sections.Add(button);
            }
            root.Add(sections);
            root.Add(new Label(_assetPaths[section]) { style = { whiteSpace = WhiteSpace.Normal, marginTop = 5, marginBottom = 5 } });
            _status = new HelpBox("保存后同步到运行中的对局。切换分类会保留未保存的修改。", HelpBoxMessageType.Info);
            root.Add(_status);
            _itemPicker = null;
            if (Catalog(section) != null && section != 3) BuildPicker(root);
            _content = new ScrollView { style = { flexGrow = 1, paddingTop = 8 } };
            root.Add(_content);
            root.UnregisterCallback<SerializedPropertyChangeEvent>(OnPropertyChanged);
            root.RegisterCallback<SerializedPropertyChangeEvent>(OnPropertyChanged);
            BuildContent();
        }

        private void BuildPicker(VisualElement root)
        {
            var names = ItemNames();
            var selected = Mathf.Clamp(SelectedIndex(), 0, names.Count - 1);
            SetSelectedIndex(selected);
            _itemPicker = new DropdownField(_sectionNames[section], names, selected);
            _itemPicker.RegisterValueChangedCallback(_ => { SetSelectedIndex(_itemPicker.index); selectedModification = previewModificationMask = 0; BuildContent(); });
            root.Add(_itemPicker);
            if (section != 0) return;
            var tiers = new Toolbar();
            for (var i = 0; i <= SurvivorSkills.MODIFICATION_COUNT; i++)
            {
                var index = i;
                tiers.Add(new ToolbarButton(() => { selectedModification = index; previewModificationMask = index == 0 ? 0 : 1 << (index - 1); BuildContent(); }) { text = i == 0 ? "基础技能" : "改造 " + i });
            }
            root.Add(tiers);
        }

        private object Catalog(int index) => index switch { 0 => document.catalog, 1 => document.waves, 2 => document.enemies, 3 => document.levels, _ => document.enemySkills };
        private int SelectedIndex() => section switch { 0 => selectedSkill, 1 => selectedWave, 2 => selectedEnemy, _ => selectedEnemySkill };
        private void SetSelectedIndex(int index)
        {
            if (section == 0) selectedSkill = index;
            else if (section == 1) selectedWave = index;
            else if (section == 2) selectedEnemy = index;
            else if (section == 4) selectedEnemySkill = index;
        }

        private System.Collections.Generic.List<string> ItemNames() => section switch
        {
            0 => document.catalog.skills.Concat(new[] { document.catalog.dash }).Select(skill => skill.displayName.Replace('/', '／') + "（" + skill.id + "）").ToList(),
            1 => document.waves.waves.Select((wave, i) => (i + 1) + ". " + wave.displayName.Replace('/', '／') + "（" + wave.startTime + "s）").ToList(),
            2 => document.enemies.enemies.Select(enemy => enemy.displayName.Replace('/', '／') + "（" + enemy.id + "）").ToList(),
            4 => document.enemySkills.skills.Select(skill => skill.displayName.Replace('/', '／') + "（" + skill.id + "）").ToList(),
            _ => new System.Collections.Generic.List<string>()
        };

        private void LoadFiles()
        {
            rootVisualElement.Unbind();
            _serialized?.Dispose();
            if (document == null) document = CreateInstance<SurvivorConfigEditorDocument>();
            document.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            Undo.ClearUndo(document);
            for (var i = 0; i < _assetPaths.Length; i++)
            {
                rawJson[i] = "";
                try
                {
                    var path = Path.Combine(Application.dataPath, _assetPaths[i].Substring("Assets/".Length));
                    rawJson[i] = File.ReadAllText(path, Encoding.UTF8);
                    ParseIntoDocument(i, rawJson[i]);
                    loadErrors[i] = null;
                }
                catch (Exception exception)
                {
                    if (i == 0) document.catalog = null;
                    else if (i == 1) document.waves = null;
                    else if (i == 2) document.enemies = null;
                    else if (i == 3) document.levels = null;
                    else document.enemySkills = null;
                    loadErrors[i] = exception.Message;
                }
                rawDirty[i] = dirty[i] = false;
            }
            hasUnsavedChanges = false;
            _serialized = new SerializedObject(document);
        }

        private void ParseIntoDocument(int index, string json)
        {
            if (index == 0) document.catalog = SurvivorSkills.Parse(json);
            else if (index == 1) document.waves = SurvivorWaves.Parse(json);
            else if (index == 2) document.enemies = SurvivorEnemies.Parse(json);
            else if (index == 3)
            {
                document.levels = SurvivorLevels.Parse(json);
                ResetLevelRewardInputs();
            }
            else document.enemySkills = SurvivorEnemySkills.Parse(json);
        }

        private void ValidateCatalog(int index)
        {
            if (Catalog(index) == null) throw new InvalidOperationException("请先载入有效的" + _sectionNames[index] + " JSON。");
            if (index == 0) document.catalog.Rebuild();
            else if (index == 1) document.waves.Rebuild();
            else if (index == 2) document.enemies.Rebuild();
            else if (index == 3)
            {
                ValidateLevelRewardInputs();
                document.levels.Rebuild();
            }
            else document.enemySkills.Rebuild();
        }

        public override void SaveChanges()
        {
            _serialized.ApplyModifiedProperties();
            var json = new string[_assetPaths.Length];
            for (var i = 0; i < _assetPaths.Length; i++)
            {
                if (!dirty[i]) continue;
                try
                {
                    if (rawDirty[i]) ParseIntoDocument(i, rawJson[i]);
                    ValidateCatalog(i);
                    json[i] = JsonUtility.ToJson(Catalog(i), true) + "\n";
                }
                catch (Exception exception)
                {
                    section = i;
                    CreateGUI();
                    SetStatus("无法保存: " + exception.Message, true);
                    return;
                }
            }
            try
            {
                if (document.waves != null && document.enemies != null) document.waves.ValidateEnemies(document.enemies);
                if (document.enemies != null && document.enemySkills != null) document.enemies.ValidateSkills(document.enemySkills);
                for (var i = 0; i < _assetPaths.Length; i++)
                {
                    if (json[i] == null) continue;
                    var path = Path.Combine(Application.dataPath, _assetPaths[i].Substring("Assets/".Length));
                    File.WriteAllText(path, json[i], new UTF8Encoding(false));
                }
                for (var i = 0; i < _assetPaths.Length; i++)
                {
                    if (json[i] == null) continue;
                    AssetDatabase.ImportAsset(_assetPaths[i], ImportAssetOptions.ForceUpdate);
                    ApplyToArena(i, json[i]);
                    rawJson[i] = json[i];
                    rawDirty[i] = dirty[i] = false;
                    loadErrors[i] = null;
                }
                _serialized.Update();
                hasUnsavedChanges = dirty.Any(value => value);
                if (!hasUnsavedChanges) base.SaveChanges();
                SetStatus(EditorApplication.isPlaying ? "已保存并实时应用到当前对局。" : "已保存。运行游戏时将读取此配置。", false);
            }
            catch (Exception exception) { SetStatus("无法保存: " + exception.Message, true); }
        }

        internal static void ApplyToArena(int index, string json)
        {
            var arena = SurvivorArena.Instance;
            if (!EditorApplication.isPlaying || arena == null) return;
            if (index == 0) arena.ApplySkillConfiguration(json);
            else if (index == 1 || index == 2 || index == 4)
            {
                var waves = SurvivorConfiguration.ReadJson("Waves.json");
                var enemies = SurvivorConfiguration.ReadJson("Enemies.json");
                var enemySkills = SurvivorConfiguration.ReadJson("EnemySkills.json");
                arena.ApplyEncounterConfiguration(waves, enemies, enemySkills);
            }
            else arena.ApplyLevelConfiguration(json);
        }

        public override void DiscardChanges()
        {
            LoadFiles();
            base.DiscardChanges();
            if (_content != null) CreateGUI();
        }

        private bool ApplyRawJson()
        {
            try
            {
                Undo.RecordObject(document, "应用配置 JSON");
                ParseIntoDocument(section, rawJson[section]);
                loadErrors[section] = null;
                _serialized.Update();
                rawDirty[section] = false;
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
            if (rawMode || Catalog(section) == null || rawDirty[section])
            {
                if (!rawDirty[section] && Catalog(section) != null) rawJson[section] = JsonUtility.ToJson(Catalog(section), true);
                var text = new TextField("JSON") { multiline = true, value = rawJson[section] ?? "" };
                text.style.minHeight = 360;
                text.RegisterValueChangedCallback(evt => { rawJson[section] = evt.newValue; rawDirty[section] = dirty[section] = hasUnsavedChanges = true; });
                _content.Add(text);
                _content.Add(new Button(() => { if (ApplyRawJson()) { rawMode = false; CreateGUI(); } }) { text = "应用 JSON 并返回表单" });
                if (loadErrors[section] != null) SetStatus(loadErrors[section], true);
                return;
            }
            _serialized.Update();
            if (section == 0) BuildSkillContent();
            else if (section == 1) BuildWaveContent();
            else if (section == 2) BuildEnemyContent();
            else if (section == 3) BuildLevelContent();
            else BuildEnemySkillContent();
            RefreshPreview();
        }

        private void Change(string path, Action<SerializedProperty> update)
        {
            _serialized.Update();
            update(_serialized.FindProperty(path));
            _serialized.ApplyModifiedProperties();
            MarkChanged();
        }

        private void QueueBuild() => rootVisualElement.schedule.Execute(CreateGUI);
        private void OnPropertyChanged(SerializedPropertyChangeEvent evt) => MarkChanged();
        private void MarkChanged()
        {
            dirty[section] = hasUnsavedChanges = true;
            RefreshPreview();
        }

        private void OnUndoRedo()
        {
            _serialized?.Update();
            // Undo 可能还原其他分类, 保存时统一校验所有有效文档.
            for (var i = 0; i < dirty.Length; i++) dirty[i] = Catalog(i) != null || rawDirty[i];
            hasUnsavedChanges = dirty.Any(value => value);
            if (_content != null) CreateGUI();
        }

        private void SetStatus(string text, bool error)
        {
            if (_status == null) return;
            _status.text = text;
            _status.messageType = error ? HelpBoxMessageType.Error : HelpBoxMessageType.Info;
        }

        private void RefreshPreview()
        {
            if (Catalog(section) == null || rawDirty[section]) return;
            try
            {
                ValidateCatalog(section);
                if (section == 0) RefreshSkillPreview();
                else if (section == 1) RefreshWavePreview();
                else if (section == 2) RefreshEnemyPreview();
                else if (section == 3) RefreshLevelPreview();
                else RefreshEnemySkillPreview();
                if (_itemPicker != null)
                {
                    var names = ItemNames();
                    _itemPicker.choices = names;
                    _itemPicker.SetValueWithoutNotify(names[SelectedIndex()]);
                }
                SetStatus("配置有效。保存后同步到运行中的对局。", false);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
                if (_preview != null) _preview.text = "请修正配置后查看预览。";
            }
        }
    }

    internal sealed class SurvivorConfigPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!EditorApplication.isPlaying || SurvivorArena.Instance == null) return;
            var paths = new[] { SurvivorSkills.AssetPath, SurvivorWaves.ASSET_PATH, SurvivorEnemies.ASSET_PATH, SurvivorLevels.ASSET_PATH, SurvivorEnemySkills.ASSET_PATH };
            for (var i = 0; i < paths.Length; i++)
            {
                if (Array.IndexOf(imported, paths[i]) < 0 && Array.IndexOf(moved, paths[i]) < 0) continue;
                try
                {
                    var path = Path.Combine(Application.dataPath, paths[i].Substring("Assets/".Length));
                    SurvivorConfigEditorWindow.ApplyToArena(i, File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception exception) { Debug.LogError("配置未应用, 当前对局保留原配置: " + exception.Message); }
            }
        }
    }
}
