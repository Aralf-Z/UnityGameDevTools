using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private void DrawEnemyClassification(string path, SurvivorEnemyDefinition enemy)
        {
            var archetype = new DropdownField("系别", new[] { "坦克系", "战士系", "敏捷系", "远程系" }.ToList(), Mathf.Max(0, Array.IndexOf(SurvivorEnemies.archetypes, enemy.archetype)));
            archetype.RegisterValueChangedCallback(_ => Change(path + ".archetype", target => target.stringValue = SurvivorEnemies.archetypes[archetype.index]));
            _content.Add(archetype);
            AddNumber(_content, "强度（1 普通 / 2 精英 / 3 强敌）", path + ".strength", true);
        }

        private void DrawEnemySkillBindings(string path)
        {
            var foldout = new Foldout { text = "自动施放技能（1–2 个）", value = true };
            _content.Add(foldout);
            if (document.enemySkills == null)
            {
                foldout.Add(new HelpBox("请先载入有效的怪物技能配置。", HelpBoxMessageType.Error));
                return;
            }
            var definitions = document.enemySkills.skills;
            var labels = definitions.Select(skill => skill.displayName.Replace('/', '／') + "（" + skill.id + "）").ToList();
            var arrayPath = path + ".skills";
            var array = _serialized.FindProperty(arrayPath);
            for (var i = 0; i < array.arraySize; i++)
            {
                var index = i;
                var itemPath = arrayPath + ".Array.data[" + i + "]";
                var id = _serialized.FindProperty(itemPath).stringValue;
                var selected = Array.FindIndex(definitions, skill => skill.id == id);
                var field = new DropdownField("技能 " + (i + 1), labels, Mathf.Max(0, selected));
                field.RegisterValueChangedCallback(_ => Change(itemPath, target => target.stringValue = definitions[field.index].id));
                foldout.Add(field);
                var remove = new Button(() => { Change(arrayPath, target => target.DeleteArrayElementAtIndex(index)); QueueBuild(); }) { text = "移除此技能" };
                remove.SetEnabled(array.arraySize > 1);
                foldout.Add(remove);
            }
            var add = new Button(() =>
            {
                var current = document.enemies.enemies[selectedEnemy].skills;
                var available = definitions.FirstOrDefault(skill => !current.Contains(skill.id));
                if (available == null) return;
                Change(arrayPath, target =>
                {
                    var index = target.arraySize;
                    target.InsertArrayElementAtIndex(index);
                    target.GetArrayElementAtIndex(index).stringValue = available.id;
                });
                QueueBuild();
            }) { text = "添加技能" };
            add.SetEnabled(array.arraySize < 2 && definitions.Length > array.arraySize);
            foldout.Add(add);
            foldout.Add(new HelpBox("每个怪物实例分别计算技能 CD。优先施放列表中先就绪的技能, 冻结或击退会中断前摇和冲锋。技能定义在独立的“怪物技能”页编辑。", HelpBoxMessageType.Info));
        }
    }
}
