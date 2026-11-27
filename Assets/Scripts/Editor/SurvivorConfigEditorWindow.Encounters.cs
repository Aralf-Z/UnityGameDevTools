using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private string[] EnemyIds => document.enemies?.enemies?.Select(enemy => enemy.id).ToArray() ?? Array.Empty<string>();

        private void BuildWaveContent()
        {
            var settings = new Foldout { text = "对局与生成设置", value = true };
            _content.Add(settings);
            AddNumber(settings, "对局时长（秒）", "waves.runDuration");
            AddNumber(settings, "场上敌人上限", "waves.enemyLimit", true);
            AddNumber(settings, "首次自动生成延迟（秒）", "waves.firstSpawnDelay");
            AddNumber(settings, "最小生成距离", "waves.spawnDistanceMin");
            AddNumber(settings, "最大生成距离", "waves.spawnDistanceMax");
            DrawSpawnGroups("开局生成", "waves.initialSpawns");
            _content.Add(new HelpBox("波次按开始时间递增排列, 第一波从 0 秒开始。最后一波持续到对局结束。保存时更新当前波次与生成间隔, 不重复执行开局或入场生成。", HelpBoxMessageType.Info));
            selectedWave = Mathf.Clamp(selectedWave, 0, document.waves.waves.Length - 1);
            var path = "waves.waves.Array.data[" + selectedWave + "]";
            AddText("波次名称", path + ".displayName");
            AddNumber(_content, "开始时间（秒）", path + ".startTime");
            AddNumber(_content, "生成间隔（秒）", path + ".spawnInterval");
            AddNumber(_content, "每批生成数量", path + ".spawnCount", true);
            DrawEnemyPool(path + ".enemyPool");
            DrawSpawnGroups("进入此波时生成", path + ".entrySpawns");
            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            actions.Add(new Button(AddWave) { text = "追加波次" });
            var remove = new Button(RemoveWave) { text = "删除当前波次" };
            remove.SetEnabled(document.waves.waves.Length > 1);
            actions.Add(remove);
            _content.Add(actions);
            AddPreview("波次预览");
        }

        private void DrawEnemyPool(string path)
        {
            var pool = new Foldout { text = "每批随机选择的敌人池", value = true };
            _content.Add(pool);
            var array = _serialized.FindProperty(path);
            var selected = new HashSet<string>();
            for (var i = 0; i < array.arraySize; i++) selected.Add(array.GetArrayElementAtIndex(i).stringValue);
            foreach (var id in EnemyIds)
            {
                var toggle = new Toggle(EnemyLabel(id)) { value = selected.Contains(id) };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    Change(path, target =>
                    {
                        var values = new List<string>();
                        for (var i = 0; i < target.arraySize; i++) values.Add(target.GetArrayElementAtIndex(i).stringValue);
                        values.RemoveAll(value => value == id);
                        if (evt.newValue) values.Add(id);
                        target.arraySize = values.Count;
                        for (var i = 0; i < values.Count; i++) target.GetArrayElementAtIndex(i).stringValue = values[i];
                    });
                });
                pool.Add(toggle);
            }
        }

        private string EnemyLabel(string id)
        {
            var enemy = document.enemies?.enemies?.FirstOrDefault(item => item.id == id);
            return enemy == null ? id : enemy.displayName + "（" + id + "）";
        }

        private void DrawSpawnGroups(string title, string path)
        {
            var groups = new Foldout { text = title, value = true };
            _content.Add(groups);
            var array = _serialized.FindProperty(path);
            var ids = EnemyIds;
            for (var i = 0; i < array.arraySize; i++)
            {
                var index = i;
                var itemPath = path + ".Array.data[" + i + "]";
                var row = new VisualElement { style = { paddingLeft = 8, marginBottom = 8 } };
                var id = _serialized.FindProperty(itemPath + ".enemy").stringValue;
                var selected = ids.Length == 0 ? -1 : Mathf.Max(0, Array.IndexOf(ids, id));
                var selector = new DropdownField("怪物", ids.Select(EnemyLabel).ToList(), selected);
                selector.SetEnabled(ids.Length > 0);
                selector.RegisterValueChangedCallback(_ => Change(itemPath + ".enemy", target => target.stringValue = ids[selector.index]));
                row.Add(selector);
                AddNumber(row, "数量", itemPath + ".count", true);
                row.Add(new Button(() => { Change(path, target => target.DeleteArrayElementAtIndex(index)); QueueBuild(); }) { text = "移除此组" });
                groups.Add(row);
            }
            var addGroup = new Button(() =>
            {
                Change(path, target =>
                {
                    var index = target.arraySize;
                    target.InsertArrayElementAtIndex(index);
                    var item = target.GetArrayElementAtIndex(index);
                    item.FindPropertyRelative("enemy").stringValue = ids[0];
                    item.FindPropertyRelative("count").intValue = 1;
                });
                QueueBuild();
            }) { text = "添加生成组" };
            addGroup.SetEnabled(ids.Length > 0);
            groups.Add(addGroup);
        }

        private void AddWave()
        {
            Change("waves", target =>
            {
                var array = target.FindPropertyRelative("waves");
                var index = array.arraySize;
                var time = array.GetArrayElementAtIndex(index - 1).FindPropertyRelative("startTime").floatValue + 20;
                array.InsertArrayElementAtIndex(index);
                var item = array.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("displayName").stringValue = "第 " + (index + 1) + " 波";
                item.FindPropertyRelative("startTime").floatValue = time;
                item.FindPropertyRelative("entrySpawns").arraySize = 0;
                var duration = target.FindPropertyRelative("runDuration");
                duration.floatValue = Mathf.Max(duration.floatValue, time + 20);
                selectedWave = index;
            });
            QueueBuild();
        }

        private void RemoveWave()
        {
            Change("waves.waves", target =>
            {
                target.DeleteArrayElementAtIndex(selectedWave);
                target.GetArrayElementAtIndex(0).FindPropertyRelative("startTime").floatValue = 0;
                selectedWave = Mathf.Clamp(selectedWave, 0, target.arraySize - 1);
            });
            QueueBuild();
        }

        private void BuildEnemyContent()
        {
            selectedEnemy = Mathf.Clamp(selectedEnemy, 0, document.enemies.enemies.Length - 1);
            var enemy = document.enemies.enemies[selectedEnemy];
            var path = "enemies.enemies.Array.data[" + selectedEnemy + "]";
            DrawEnemyIdentity(enemy);
            AddText("怪物名称", path + ".displayName");
            DrawEnemyClassification(path, enemy);
            DrawEnemyShape(path, enemy);
            AddToggle("重型怪物（使用重型击退倍率）", path + ".heavy");
            AddNumber(_content, "基础生命", path + ".health");
            AddNumber(_content, "每波生命增长比例", path + ".healthGrowthPerWave");
            _content.Add(new HelpBox("生命 = 基础生命 × [1 + (波次 - 1) × 增长比例]。0.18 表示每波增加基础生命的 18%。保存后现有敌人保留生命比例, 并按生成时的波次重算上限。", HelpBoxMessageType.Info));
            AddNumber(_content, "移动速度", path + ".moveSpeed");
            AddNumber(_content, "与玩家保持距离（0 为近战）", path + ".preferredDistance");
            AddNumber(_content, "技能伤害倍率", path + ".skillDamageMultiplier");
            DrawEnemySkillBindings(path);
            AddNumber(_content, "接触伤害", path + ".contactDamage");
            AddNumber(_content, "接触间隔（秒）", path + ".contactInterval");
            AddNumber(_content, "掉落经验", path + ".experience", true);
            AddNumber(_content, "碰撞半径", path + ".radius");
            AddColor("外观颜色", path + ".bodyColor", false);
            var wave = new IntegerField("预览波次") { value = _previewWave };
            wave.RegisterValueChangedCallback(evt =>
            {
                _previewWave = Mathf.Clamp(evt.newValue, 1, 10000);
                wave.SetValueWithoutNotify(_previewWave);
                RefreshPreview();
            });
            _content.Add(wave);
            AddPreview("敌人数值预览");
            DrawEnemyActions();
        }

        private void AddPreview(string title)
        {
            var preview = new Foldout { text = title, value = true };
            _preview = new Label { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 8, paddingBottom = 12 } };
            preview.Add(_preview);
            _content.Add(preview);
        }

        private void RefreshWavePreview()
        {
            var catalog = document.waves;
            var wave = catalog.waves[selectedWave];
            var end = catalog.runDuration;
            if (selectedWave + 1 < catalog.waves.Length) end = catalog.waves[selectedWave + 1].startTime;
            if (_preview != null)
                _preview.text = "第 " + (selectedWave + 1) + " 波: " + wave.startTime + "–" + end + " 秒\n每 " + wave.spawnInterval + " 秒生成 " + wave.spawnCount + " 个敌人\n敌人池: " + string.Join("、", wave.enemyPool.Select(EnemyLabel)) + "\n场上敌人上限: " + catalog.enemyLimit;
        }

        private void RefreshEnemyPreview()
        {
            var enemy = document.enemies.enemies[selectedEnemy];
            if (_preview != null)
                _preview.text = "系别: " + enemy.archetype + " / 强度: " + enemy.strength + "\n形状: " + enemy.shape + " / Scale: " + enemy.scaleMin + "–" + enemy.scaleMax + "\n技能: " + string.Join("、", enemy.skills) + "\n第 " + _previewWave + " 波生命: " + enemy.HealthAtWave(_previewWave).ToString("0.##") + "\n移动速度: " + enemy.moveSpeed + "\n接触伤害: " + enemy.contactDamage + " / " + enemy.contactInterval + " 秒\n掉落经验: " + enemy.experience;
        }
    }
}
