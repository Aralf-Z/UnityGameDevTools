using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private void DrawEnemyIdentity(SurvivorEnemyDefinition enemy)
        {
            var field = new TextField("怪物 ID") { value = enemy.id, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                if (!RenameEnemy(enemy, evt.newValue)) field.SetValueWithoutNotify(enemy.id);
            });
            _content.Add(field);
            _content.Add(new HelpBox("ID 可自定义且必须唯一。修改 ID 会同步波次中的引用。删除怪物会移除开局及入场生成组中的引用; 若某波敌人池因此为空, 自动使用剩余第一种怪物。", HelpBoxMessageType.Info));
        }

        private void DrawEnemyShape(string path, SurvivorEnemyDefinition enemy)
        {
            var shape = new DropdownField("形状", new[] { "立方体", "球体", "胶囊体", "圆柱体" }.ToList(), Mathf.Max(0, Array.IndexOf(SurvivorEnemies.shapes, enemy.shape)));
            shape.RegisterValueChangedCallback(_ => Change(path + ".shape", target => target.stringValue = SurvivorEnemies.shapes[shape.index]));
            _content.Add(shape);
            var minimum = new Vector3Field("最小 Scale");
            minimum.BindProperty(_serialized.FindProperty(path + ".scaleMin"));
            _content.Add(minimum);
            var maximum = new Vector3Field("最大 Scale");
            maximum.BindProperty(_serialized.FindProperty(path + ".scaleMax"));
            _content.Add(maximum);
            _content.Add(new HelpBox("每次生成（含对象池复用）在最小/最大 Scale 之间随机取样, XYZ 使用相同插值比例。血条高度和碰撞半径随大小调整, 保存时保留当前个体的随机比例。", HelpBoxMessageType.Info));
        }

        private void DrawEnemyActions()
        {
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            var add = new Button(AddEnemy) { text = "新增怪物" };
            add.SetEnabled(document.enemies.enemies.Length < 256);
            actions.Add(add);
            var remove = new Button(RemoveEnemy) { text = "删除当前怪物" };
            remove.SetEnabled(document.enemies.enemies.Length > 1);
            actions.Add(remove);
            _content.Add(actions);
        }

        private void AddEnemy()
        {
            if (document.enemySkills == null) { SetStatus("请先载入有效的怪物技能配置。", true); return; }
            _serialized.Update();
            Undo.RecordObject(document, "新增怪物");
            var definitions = document.enemies.enemies.ToList();
            var suffix = 1;
            while (definitions.Any(enemy => enemy.id == "Enemy" + suffix)) suffix++;
            definitions.Add(new SurvivorEnemyDefinition { id = "Enemy" + suffix, displayName = "新怪物 " + suffix, skills = new[] { document.enemySkills.skills[0].id } });
            document.enemies.enemies = definitions.ToArray();
            selectedEnemy = definitions.Count - 1;
            _serialized.Update();
            MarkChanged();
            QueueBuild();
        }

        private bool RenameEnemy(SurvivorEnemyDefinition enemy, string id)
        {
            if (enemy.id == id) return true;
            try
            {
                SurvivorConfiguration.ValidateEnemyId(id);
                if (document.enemies.enemies.Any(item => item != enemy && item.id == id)) throw new ArgumentException("重复的怪物 ID: " + id);
                RequireEditableWaveReferences();
                _serialized.Update();
                Undo.RecordObject(document, "修改怪物 ID 与波次引用");
                var previous = enemy.id;
                enemy.id = id;
                foreach (var group in document.waves.initialSpawns ?? Array.Empty<SurvivorSpawnGroup>())
                    if (group.enemy == previous) group.enemy = id;
                foreach (var wave in document.waves.waves)
                {
                    wave.enemyPool = wave.enemyPool.Select(value => value == previous ? id : value).ToArray();
                    foreach (var group in wave.entrySpawns ?? Array.Empty<SurvivorSpawnGroup>())
                        if (group.enemy == previous) group.enemy = id;
                }
                FinishEnemyReferenceChange();
                return true;
            }
            catch (Exception exception) { SetStatus(exception.Message, true); return false; }
        }

        private void RemoveEnemy()
        {
            if (document.enemies.enemies.Length <= 1) return;
            try
            {
                RequireEditableWaveReferences();
                _serialized.Update();
                Undo.RecordObject(document, "删除怪物与波次引用");
                var id = document.enemies.enemies[selectedEnemy].id;
                document.enemies.enemies = document.enemies.enemies.Where(enemy => enemy.id != id).ToArray();
                var fallback = document.enemies.enemies[0].id;
                document.waves.initialSpawns = (document.waves.initialSpawns ?? Array.Empty<SurvivorSpawnGroup>()).Where(group => group.enemy != id).ToArray();
                foreach (var wave in document.waves.waves)
                {
                    wave.enemyPool = wave.enemyPool.Where(value => value != id).ToArray();
                    if (wave.enemyPool.Length == 0) wave.enemyPool = new[] { fallback };
                    wave.entrySpawns = (wave.entrySpawns ?? Array.Empty<SurvivorSpawnGroup>()).Where(group => group.enemy != id).ToArray();
                }
                selectedEnemy = Mathf.Clamp(selectedEnemy, 0, document.enemies.enemies.Length - 1);
                FinishEnemyReferenceChange();
            }
            catch (Exception exception) { SetStatus(exception.Message, true); }
        }

        private void RequireEditableWaveReferences()
        {
            if (document.waves == null || rawDirty[1])
                throw new InvalidOperationException("请先在波次页修正并应用原始 JSON, 再修改怪物 ID 或删除怪物。");
        }

        private void FinishEnemyReferenceChange()
        {
            _serialized.Update();
            dirty[1] = dirty[2] = hasUnsavedChanges = true;
            RefreshPreview();
            QueueBuild();
        }
    }
}
