using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Editor
{
    public sealed partial class SurvivorConfigEditorWindow
    {
        private void BuildEnemySkillContent()
        {
            selectedEnemySkill = Mathf.Clamp(selectedEnemySkill, 0, document.enemySkills.skills.Length - 1);
            var skill = document.enemySkills.skills[selectedEnemySkill];
            var path = "enemySkills.skills.Array.data[" + selectedEnemySkill + "]";
            AddNumber(_content, "怪物投射物总上限", "enemySkills.projectileLimit", true);
            var id = new TextField("技能 ID") { value = skill.id, isDelayed = true };
            id.RegisterValueChangedCallback(evt => { if (!RenameEnemySkill(skill, evt.newValue)) id.SetValueWithoutNotify(skill.id); });
            _content.Add(id);
            AddText("技能名称", path + ".displayName");
            AddText("技能描述", path + ".description", true);
            AddToggle("启用", path + ".enabled");
            var labels = new[] { "扇形近战", "自身范围攻击", "冲锋", "单体投射物", "散射投射物", "定点轰击", "减伤护盾", "移动加速", "自身治疗" }.ToList();
            var behavior = new DropdownField("技能行为", labels, Mathf.Max(0, Array.IndexOf(SurvivorEnemySkills.behaviors, skill.behavior)));
            behavior.RegisterValueChangedCallback(_ => { Change(path + ".behavior", target => target.stringValue = SurvivorEnemySkills.behaviors[behavior.index]); QueueBuild(); });
            _content.Add(behavior);
            AddNumber(_content, "冷却（秒）", path + ".cooldown");
            AddNumber(_content, "生成后初始冷却（秒）", path + ".initialCooldown");
            AddNumber(_content, "初始 CD 随机浮动（秒）", path + ".initialCooldownJitter");
            AddNumber(_content, "自动施放距离", path + ".castRange");
            AddNumber(_content, "施法前摇（秒）", path + ".windup");
            DrawEnemySkillParameters(path, skill.behavior);
            AddColor("技能颜色", path + ".effectColor", false);
            DrawEnemySkillActions();
            AddPreview("怪物技能说明");
        }

        private void DrawEnemySkillParameters(string path, string behavior)
        {
            if (behavior == "Guard")
            {
                AddNumber(_content, "护盾持续时间（秒）", path + ".duration");
                AddNumber(_content, "减伤比例（0–0.95）", path + ".damageReduction");
                return;
            }
            if (behavior == "Haste")
            {
                AddNumber(_content, "加速持续时间（秒）", path + ".duration");
                AddNumber(_content, "移动速度倍率", path + ".moveSpeedMultiplier");
                return;
            }
            if (behavior == "Heal")
            {
                AddNumber(_content, "回复最大生命比例", path + ".healPercent");
                AddNumber(_content, "低于此生命比例时施放", path + ".healThreshold");
                return;
            }
            AddNumber(_content, "基础伤害", path + ".damage");
            if (behavior == "Projectile" || behavior == "Volley")
            {
                AddNumber(_content, "投射物数量", path + ".projectileCount", true);
                AddNumber(_content, "相邻投射物夹角", path + ".projectileSpread");
                AddNumber(_content, "飞行速度", path + ".projectileSpeed");
                AddNumber(_content, "存活时间（秒）", path + ".projectileLifetime");
                AddNumber(_content, "碰撞半径", path + ".projectileRadius");
            }
            else if (behavior == "Charge")
            {
                AddNumber(_content, "冲锋持续时间（秒）", path + ".duration");
                AddNumber(_content, "冲锋速度", path + ".chargeSpeed");
            }
            else
            {
                AddNumber(_content, "攻击半径", path + ".radius");
                if (behavior == "Strike") AddNumber(_content, "扇形角度", path + ".arcAngle");
            }
        }

        private void DrawEnemySkillActions()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            var add = new Button(AddEnemySkill) { text = "新增怪物技能" };
            add.SetEnabled(document.enemySkills.skills.Length < 256);
            row.Add(add);
            var remove = new Button(RemoveEnemySkill) { text = "删除当前技能" };
            remove.SetEnabled(document.enemySkills.skills.Length > 1);
            row.Add(remove);
            _content.Add(row);
        }

        private void AddEnemySkill()
        {
            _serialized.Update();
            Undo.RecordObject(document, "新增怪物技能");
            var skills = document.enemySkills.skills.ToList();
            var index = 1;
            while (skills.Any(skill => skill.id == "EnemySkill" + index)) index++;
            skills.Add(new SurvivorEnemySkillDefinition { id = "EnemySkill" + index, displayName = "新怪物技能 " + index });
            document.enemySkills.skills = skills.ToArray();
            selectedEnemySkill = skills.Count - 1;
            _serialized.Update();
            MarkChanged();
            QueueBuild();
        }

        private bool RenameEnemySkill(SurvivorEnemySkillDefinition skill, string id)
        {
            if (skill.id == id) return true;
            try
            {
                SurvivorConfiguration.ValidateEnemyId(id);
                RequireEditableEnemySkillReferences();
                if (document.enemySkills.skills.Any(item => item != skill && item.id == id)) throw new ArgumentException("重复的怪物技能 ID: " + id);
                _serialized.Update();
                Undo.RecordObject(document, "修改怪物技能 ID 和引用");
                var previous = skill.id;
                skill.id = id;
                foreach (var enemy in document.enemies.enemies) enemy.skills = enemy.skills.Select(value => value == previous ? id : value).ToArray();
                FinishEnemySkillReferenceChange();
                return true;
            }
            catch (Exception exception) { SetStatus(exception.Message, true); return false; }
        }

        private void RemoveEnemySkill()
        {
            if (document.enemySkills.skills.Length <= 1) return;
            try
            {
                RequireEditableEnemySkillReferences();
                _serialized.Update();
                Undo.RecordObject(document, "删除怪物技能与引用");
                var id = document.enemySkills.skills[selectedEnemySkill].id;
                document.enemySkills.skills = document.enemySkills.skills.Where(skill => skill.id != id).ToArray();
                var fallback = document.enemySkills.skills[0].id;
                foreach (var enemy in document.enemies.enemies)
                {
                    enemy.skills = enemy.skills.Where(value => value != id).ToArray();
                    if (enemy.skills.Length == 0) enemy.skills = new[] { fallback };
                }
                selectedEnemySkill = Mathf.Clamp(selectedEnemySkill, 0, document.enemySkills.skills.Length - 1);
                FinishEnemySkillReferenceChange();
            }
            catch (Exception exception) { SetStatus(exception.Message, true); }
        }

        private void RequireEditableEnemySkillReferences()
        {
            if (document.enemies == null || rawDirty[2]) throw new InvalidOperationException("请先在怪物页修正并应用原始 JSON, 再修改技能 ID 或删除技能。");
        }

        private void FinishEnemySkillReferenceChange()
        {
            _serialized.Update();
            dirty[2] = dirty[4] = hasUnsavedChanges = true;
            RefreshPreview();
            QueueBuild();
        }

        private void RefreshEnemySkillPreview()
        {
            var skill = document.enemySkills.skills[selectedEnemySkill];
            if (_preview != null)
                _preview.text = skill.description + "\n每个怪物独立冷却: " + skill.cooldown + " 秒\n技能伤害 = " + skill.damage + " × 所属怪物技能伤害倍率\n冻结/击退中断施法, 暂停/选择奖励/对局结束时停止技能计时。";
        }
    }
}
