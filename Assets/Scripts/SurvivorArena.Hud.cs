using System;
using GameConsole;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private GameObject _hudObject;
        private PanelSettings _hudPanel;
        private FontAsset _hudFont;
        private Label _healthText, _statusText, _timeText, _experienceText, _toastText, _modalTitle, _modalDescription;
        private VisualElement _healthFill, _experienceFill, _modal;
        private Button _resume;
        private readonly Label[] _skillNames = new Label[SurvivorSkills.SKILL_COUNT];
        private readonly Label[] _skillTitles = new Label[SurvivorSkills.COMBAT_SKILL_COUNT];
        private VisualElement _rewardModal;
        private Label _rewardSubtitle, _rewardTitle;
        private readonly Button[] _rewardButtons = new Button[3];
        private readonly Label[] _rewardNames = new Label[3];
        private readonly Label[] _rewardDescriptions = new Label[3];
        private readonly Label[] _rewardProgress = new Label[3];
        private readonly Label[] _rewardSkillTitles = new Label[3];
        private float _toastUntil;
        private float _shownHealth = 1;
        private float _shownExperience;

        private void BuildHud(Font source)
        {
            _hudObject = new GameObject("Survivor HUD (code)");
            _hudObject.transform.SetParent(transform, false);
            var document = _hudObject.AddComponent<UIDocument>();
            var panelTemplate = Resources.Load<PanelSettings>("Sanmon/GameDevTools/UIStyle/Shared/GameDevToolPanel");
            _hudPanel = panelTemplate != null ? Instantiate(panelTemplate) : ScriptableObject.CreateInstance<PanelSettings>();
            _hudPanel.sortingOrder = 0;
            _hudPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _hudPanel.referenceResolution = new Vector2Int(1600, 900);
            _hudPanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _hudPanel.match = 0.5f;
            _hudPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Sanmon/GameDevTools/UIStyle/Shared/GameDevToolTheme");
            document.panelSettings = _hudPanel;
            document.sortingOrder = 20;
            var root = document.rootVisualElement;
            root.name = "survivorHud";
            root.style.flexGrow = 1;
            root.style.color = SurvivorPalette.Text;
            root.style.fontSize = 18;
            root.pickingMode = PickingMode.Ignore;
            source = source != null ? source : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hudFont = FontAsset.CreateFontAsset(source);
            _hudFont.isMultiAtlasTexturesEnabled = true;
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(_hudFont);

            var stats = Card(root, 22, 20, 340);
            Text(stats, "CAPSULE / SURVIVOR", 14, SurvivorPalette.Player);
            _timeText = Text(stats, "03:00", 34, SurvivorPalette.Text);
            _healthText = Text(stats, "HP 100 / 100", 17, SurvivorPalette.Text);
            _healthFill = Bar(stats, SurvivorPalette.Player);
            _experienceText = Text(stats, "LEVEL 1", 15, SurvivorPalette.Gold);
            _experienceText.style.marginTop = 12;
            _experienceFill = Bar(stats, SurvivorPalette.Gold);
            _statusText = Text(stats, "WAVE 1 / KILLS 0", 15, SurvivorPalette.Muted);
            _statusText.style.marginTop = 12;

            var controls = Card(root, float.NaN, 20, 300);
            controls.style.right = 22;
            Text(controls, "移动 WASD / 方向键", 16, SurvivorPalette.Text);
            Text(controls, "战斗技能自动释放", 15, SurvivorPalette.Muted);
            Text(controls, "Space 冲刺 · Esc 暂停", 15, SurvivorPalette.Muted);
            AddButton(controls, "F1  开关 GameDevTools", ToggleTools);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            controls.Add(row);
            AddButton(row, "重启工具", () => RestartTools());
            AddButton(row, "释放工具", () => Tools?.Shutdown());
            Text(controls, "SurvivorHelp() 查看开发命令", 13, SurvivorPalette.Muted);

            var bottom = new VisualElement();
            bottom.style.position = Position.Absolute;
            bottom.style.left = 20;
            bottom.style.right = 20;
            bottom.style.bottom = 22;
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.justifyContent = Justify.Center;
            root.Add(bottom);
            var names = new string[SurvivorSkills.SKILL_COUNT];
            for (var i = 0; i < SurvivorSkills.COMBAT_SKILL_COUNT; i++) names[i] = SkillName((SkillKind)i) + " / AUTO";
            names[(int)SkillKind.Dash] = "SPACE  冲刺";
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++)
            {
                var card = new VisualElement();
                StyleCard(card);
                card.style.flexGrow = 1;
                card.style.flexBasis = 0;
                card.style.maxWidth = 180;
                card.style.marginLeft = card.style.marginRight = 4;
                bottom.Add(card);
                var title = Text(card, names[i], 14, i == 3 ? SurvivorPalette.Frost : SurvivorPalette.Text);
                if (i < SurvivorSkills.COMBAT_SKILL_COUNT) _skillTitles[i] = title;
                _skillNames[i] = Text(card, "READY", 16, SurvivorPalette.Player);
            }

            _toastText = Text(root, string.Empty, 22, SurvivorPalette.Gold);
            _toastText.style.position = Position.Absolute;
            _toastText.style.top = 220;
            _toastText.style.left = 0;
            _toastText.style.right = 0;
            _toastText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _modal = new VisualElement { name = "pauseOrEndScreen" };
            _modal.style.position = Position.Absolute;
            _modal.style.left = _modal.style.right = _modal.style.top = _modal.style.bottom = 0;
            _modal.style.alignItems = Align.Center;
            _modal.style.justifyContent = Justify.Center;
            _modal.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.88f);
            _modal.pickingMode = PickingMode.Position;
            root.Add(_modal);
            var dialog = new VisualElement();
            StyleCard(dialog);
            dialog.style.width = 460;
            dialog.style.paddingLeft = dialog.style.paddingRight = 30;
            dialog.style.paddingTop = dialog.style.paddingBottom = 28;
            _modal.Add(dialog);
            _modalTitle = Text(dialog, "PAUSED", 38, SurvivorPalette.Player);
            _modalDescription = Text(dialog, string.Empty, 18, SurvivorPalette.Text);
            _modalDescription.style.whiteSpace = WhiteSpace.Normal;
            _modalDescription.style.marginTop = _modalDescription.style.marginBottom = 16;
            _resume = AddButton(dialog, "继续 / Esc", () => SetPaused(false));
            AddButton(dialog, "重新开始", () => RestartRun());
            AddButton(dialog, "打开 GameDevTools", () => { if (Tools?.Console == null) RestartTools(); else Tools.Open(); });
            BuildRewardHud(root);
        }

        private static VisualElement Card(VisualElement parent, float left, float top, float width)
        {
            var card = new VisualElement();
            StyleCard(card);
            card.style.position = Position.Absolute;
            if (!float.IsNaN(left)) card.style.left = left;
            card.style.top = top;
            card.style.width = width;
            parent.Add(card);
            return card;
        }

        private static void StyleCard(VisualElement element)
        {
            element.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.92f);
            element.style.paddingLeft = element.style.paddingRight = 15;
            element.style.paddingTop = element.style.paddingBottom = 12;
            element.style.borderLeftWidth = 2;
            element.style.borderLeftColor = SurvivorPalette.Player;
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 4;
        }

        private static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.marginTop = label.style.marginBottom = 2;
            parent.Add(label);
            return label;
        }

        private static VisualElement Bar(VisualElement parent, Color color)
        {
            var track = new VisualElement();
            track.style.height = 8;
            track.style.marginTop = 4;
            track.style.backgroundColor = SurvivorPalette.Grid;
            var fill = new VisualElement();
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = color;
            track.Add(fill);
            parent.Add(track);
            return fill;
        }

        private static Button AddButton(VisualElement parent, string title, Action action)
        {
            // 方向键和空格用于移动、冲刺，HUD 按钮仅通过鼠标点击。
            var button = new Button(action) { text = title, focusable = false };
            button.style.height = 36;
            button.style.marginTop = 8;
            button.style.marginLeft = button.style.marginRight = 0;
            button.style.marginBottom = 2;
            button.style.fontSize = 15;
            button.style.color = SurvivorPalette.Text;
            button.style.backgroundColor = SurvivorPalette.Grid;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            button.style.paddingLeft = button.style.paddingRight = 12;
            button.style.flexGrow = 1;
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = SurvivorPalette.Stone);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = SurvivorPalette.Grid);
            parent.Add(button);
            return button;
        }

        private void Toast(string message) { if (_toastText == null) return; _toastText.text = message; _toastUntil = Time.unscaledTime + 2.8f; }

        private void RefreshHud(bool immediate)
        {
            if (_healthText == null) return;
            var health = Stats.Health / Stats.MaxHealth;
            var currentThreshold = _levels.Get(Stats.Level).totalExperience;
            var required = Stats.ExperienceRequired - currentThreshold;
            var experience = Stats.Level >= _levels.MaxLevel ? 1 : Mathf.Clamp01((Stats.Experience - currentThreshold) / (float)Mathf.Max(1, required));
            var ease = immediate ? 1 : 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            _shownHealth = Mathf.Lerp(_shownHealth, health, ease);
            _shownExperience = Mathf.Lerp(_shownExperience, experience, ease);
            _healthFill.style.width = Length.Percent(_shownHealth * 100);
            _experienceFill.style.width = Length.Percent(_shownExperience * 100);
            _healthText.text = "HP " + Mathf.CeilToInt(Stats.Health) + " / " + Mathf.CeilToInt(Stats.MaxHealth) + (Stats.GodMode ? "  [GOD]" : string.Empty);
            if (Stats.HealthRegenerationPerSecond > 0) _healthText.text += "  +" + Stats.HealthRegenerationPerSecond.ToString("0.##") + "/s";
            _experienceText.text = Stats.Level >= _levels.MaxLevel
                ? "LEVEL " + Stats.Level + "  MAX · 累计 " + Stats.Experience
                : "LEVEL " + Stats.Level + "    累计 " + Stats.Experience + " / " + Stats.ExperienceRequired;
            var remaining = Mathf.Max(0, Mathf.CeilToInt(RunDuration - Stats.Elapsed));
            _timeText.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            _statusText.text = "WAVE " + Stats.Wave + "   KILLS " + Stats.Kills + "   敌人 " + EnemyCount;
            for (var i = 0; i < SurvivorSkills.COMBAT_SKILL_COUNT; i++)
            {
                var skill = (SkillKind)i;
                _skillNames[i].parent.style.display = HasSkill(skill) ? DisplayStyle.Flex : DisplayStyle.None;
                var cooldown = SkillCooldown(skill);
                _skillTitles[i].text = SkillName(skill) + " / AUTO";
                _skillTitles[i].tooltip = _skills.Description(skill, ModificationMask(skill));
                _skillNames[i].text = "改造 " + ModificationCount(skill) + "/5" + (cooldown > 0 ? "  " + cooldown.ToString("0.0") + "s" : "  READY");
            }
            var dash = Mathf.Max(0, _dashReadyAt - Stats.Elapsed);
            _skillNames[(int)SkillKind.Dash].text = "改造 " + ModificationCount(SkillKind.Dash) + "/5  " + (dash > 0 ? dash.ToString("0.0") + "s" : "READY");
            _skillNames[(int)SkillKind.Dash].tooltip = _skills.Description(SkillKind.Dash, ModificationMask(SkillKind.Dash));
            _toastText.style.display = Time.unscaledTime < _toastUntil ? DisplayStyle.Flex : DisplayStyle.None;
            _modal.style.display = (IsPaused || HasEnded) && !IsChoosingReward ? DisplayStyle.Flex : DisplayStyle.None;
            _resume.style.display = HasEnded ? DisplayStyle.None : DisplayStyle.Flex;
            _modalTitle.text = HasEnded ? HasWon ? "SURVIVED" : "RUN ENDED" : "PAUSED";
            _modalTitle.style.color = HasEnded && !HasWon ? SurvivorPalette.Enemy : SurvivorPalette.Player;
            _modalDescription.text = HasEnded ? "击杀 " + Stats.Kills + " · 等级 " + Stats.Level + " · 最佳 " + _bestKills + "\n重新开始即可继续测试。" : "世界模拟已暂停。\nGameDevTools 和宏仍可操作。";
            RefreshRewardHud();
        }

        private void ReleaseHud()
        {
            if (_hudObject != null) Destroy(_hudObject);
            if (_hudPanel != null) Destroy(_hudPanel);
            if (_hudFont == null) return;
            foreach (var texture in _hudFont.atlasTextures) if (texture != null) Destroy(texture);
            if (_hudFont.material != null) Destroy(_hudFont.material);
            Destroy(_hudFont);
        }
    }
}
