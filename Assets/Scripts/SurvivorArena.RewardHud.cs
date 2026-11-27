using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private void BuildRewardHud(VisualElement root)
        {
            _rewardModal = new VisualElement { name = "survivorRewardScreen", pickingMode = PickingMode.Position };
            _rewardModal.style.position = Position.Absolute;
            _rewardModal.style.left = _rewardModal.style.right = _rewardModal.style.top = _rewardModal.style.bottom = 0;
            _rewardModal.style.alignItems = Align.Center;
            _rewardModal.style.justifyContent = Justify.Center;
            _rewardModal.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.94f);
            _rewardModal.style.display = DisplayStyle.None;
            root.Add(_rewardModal);
            var dialog = new VisualElement();
            StyleCard(dialog);
            dialog.style.width = 980;
            dialog.style.maxWidth = Length.Percent(95);
            dialog.style.paddingLeft = dialog.style.paddingRight = 24;
            dialog.style.paddingTop = dialog.style.paddingBottom = 24;
            _rewardModal.Add(dialog);
            _rewardTitle = Text(dialog, "获得技能", 30, SurvivorPalette.Gold);
            _rewardSubtitle = Text(dialog, string.Empty, 16, SurvivorPalette.Text);
            _rewardSubtitle.style.marginBottom = 12;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            dialog.Add(row);
            for (var i = 0; i < 3; i++) BuildRewardCard(row, i);
            Text(dialog, "点击选择一项。每项改造仅能获得一次, 候选不足三项时显示全部可选项。", 14, SurvivorPalette.Muted);
        }

        private void BuildRewardCard(VisualElement row, int index)
        {
            var button = new Button(() => ChooseReward(index)) { name = "rewardChoice" + index, focusable = false };
            StyleCard(button);
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.height = 280;
            button.style.marginLeft = button.style.marginRight = 4;
            button.style.marginBottom = 8;
            button.style.alignItems = Align.Stretch;
            button.style.unityTextAlign = TextAnchor.UpperLeft;
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = 0;
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = SurvivorPalette.Stone);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.92f));
            row.Add(button);
            _rewardButtons[index] = button;
            _rewardSkillTitles[index] = Text(button, string.Empty, 16, SurvivorPalette.Text);
            _rewardNames[index] = Text(button, string.Empty, 23, SurvivorPalette.Gold);
            _rewardNames[index].style.whiteSpace = WhiteSpace.Normal;
            _rewardDescriptions[index] = Text(button, string.Empty, 15, SurvivorPalette.Text);
            _rewardDescriptions[index].style.whiteSpace = WhiteSpace.Normal;
            _rewardDescriptions[index].style.flexGrow = 1;
            _rewardProgress[index] = Text(button, string.Empty, 13, SurvivorPalette.Muted);
        }

        private void RefreshRewardHud()
        {
            if (_rewardModal == null) return;
            _rewardModal.style.display = IsChoosingReward ? DisplayStyle.Flex : DisplayStyle.None;
            if (!IsChoosingReward) return;
            _rewardTitle.text = IsChoosingSkill ? "获得技能 / 三选一" : "技能改造 / 三选一";
            _rewardSubtitle.text = "LEVEL " + Stats.Level + " · 待获取技能 " + PendingSkills + " · 待选改造 " + PendingModifications;
            for (var i = 0; i < _rewardButtons.Length; i++)
            {
                var available = i < _rewardChoices.Count;
                _rewardButtons[i].style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
                if (!available) continue;
                var choice = _rewardChoices[i];
                var definition = _skills.Get(choice.skill);
                _rewardSkillTitles[i].text = definition.displayName;
                if (choice.kind == SurvivorRewardKind.Skill)
                {
                    _rewardNames[i].text = "获取 " + definition.displayName;
                    _rewardDescriptions[i].text = definition.description;
                    _rewardProgress[i].text = "新技能 · 拥有 5 项独立改造";
                    _rewardButtons[i].tooltip = _skills.Description(choice.skill, 0);
                }
                else
                {
                    var modification = definition.modifications[choice.modificationIndex];
                    _rewardNames[i].text = modification.displayName;
                    _rewardDescriptions[i].text = modification.additionalDescription;
                    _rewardProgress[i].text = "已获得改造 " + ModificationCount(choice.skill) + "/5 · 点击获取";
                    _rewardButtons[i].tooltip = _skills.Description(choice.skill, ModificationMask(choice.skill) | (1 << choice.modificationIndex));
                }
            }
        }
    }
}
