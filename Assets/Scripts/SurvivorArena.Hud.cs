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
        private readonly Label[] _skillNames = new Label[5];
        private readonly Label[] _skillTitles = new Label[4];
        private VisualElement _upgradeModal;
        private Label _upgradeSubtitle;
        private readonly Button[] _upgradeButtons = new Button[4];
        private readonly Label[] _upgradeNames = new Label[4];
        private readonly Label[] _upgradeDescriptions = new Label[4];
        private readonly Label[] _upgradeProgress = new Label[4];
        private readonly Label[] _upgradeSkillTitles = new Label[4];
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
            Text(controls, "DemoHelp() 查看命令和宏示例", 13, SurvivorPalette.Muted);

            var bottom = new VisualElement();
            bottom.style.position = Position.Absolute;
            bottom.style.left = 20;
            bottom.style.right = 20;
            bottom.style.bottom = 22;
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.justifyContent = Justify.Center;
            root.Add(bottom);
            var names = new string[5];
            for (var i = 0; i < 4; i++) names[i] = SkillName((SkillKind)i) + " / AUTO";
            names[4] = "SPACE  冲刺";
            for (var i = 0; i < 5; i++)
            {
                var card = new VisualElement();
                StyleCard(card);
                card.style.width = 180;
                card.style.marginLeft = card.style.marginRight = 4;
                bottom.Add(card);
                var title = Text(card, names[i], 14, i == 3 ? SurvivorPalette.Frost : SurvivorPalette.Text);
                if (i < 4) _skillTitles[i] = title;
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
            BuildUpgradeHud(root);
        }

        private void BuildUpgradeHud(VisualElement root)
        {
            _upgradeModal = new VisualElement { name = "skillUpgradeScreen", pickingMode = PickingMode.Position };
            _upgradeModal.style.position = Position.Absolute;
            _upgradeModal.style.left = _upgradeModal.style.right = _upgradeModal.style.top = _upgradeModal.style.bottom = 0;
            _upgradeModal.style.alignItems = Align.Center;
            _upgradeModal.style.justifyContent = Justify.Center;
            _upgradeModal.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.94f);
            _upgradeModal.style.display = DisplayStyle.None;
            root.Add(_upgradeModal);
            var dialog = new VisualElement();
            StyleCard(dialog);
            dialog.style.width = 720;
            dialog.style.maxWidth = Length.Percent(95);
            dialog.style.paddingLeft = dialog.style.paddingRight = 24;
            dialog.style.paddingTop = dialog.style.paddingBottom = 24;
            _upgradeModal.Add(dialog);
            Text(dialog, "LEVEL UP / 技能升级", 30, SurvivorPalette.Gold);
            _upgradeSubtitle = Text(dialog, string.Empty, 16, SurvivorPalette.Text);
            _upgradeSubtitle.style.marginBottom = 12;
            for (var rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                dialog.Add(row);
                for (var column = 0; column < 2; column++)
                {
                    var index = rowIndex * 2 + column;
                    var skill = (SkillKind)index;
                    var button = new Button(() => ChooseSkillUpgrade(skill)) { name = "upgrade" + skill, focusable = false };
                    StyleCard(button);
                    button.style.flexGrow = 1;
                    button.style.flexBasis = 0;
                    button.style.height = 220;
                    button.style.marginLeft = button.style.marginRight = 4;
                    button.style.marginBottom = 8;
                    button.style.alignItems = Align.Stretch;
                    button.style.unityTextAlign = TextAnchor.UpperLeft;
                    button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = 0;
                    button.RegisterCallback<PointerEnterEvent>(_ => { if (button.enabledSelf) button.style.backgroundColor = SurvivorPalette.Stone; });
                    button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = SurvivorPalette.Alpha(SurvivorPalette.Shadow, 0.92f));
                    row.Add(button);
                    _upgradeButtons[index] = button;
                    _upgradeSkillTitles[index] = Text(button, SkillName(skill), 16, SurvivorPalette.Text);
                    _upgradeNames[index] = Text(button, string.Empty, 23, SurvivorPalette.Gold);
                    _upgradeDescriptions[index] = Text(button, string.Empty, 14, SurvivorPalette.Text);
                    _upgradeDescriptions[index].style.whiteSpace = WhiteSpace.Normal;
                    _upgradeDescriptions[index].style.flexGrow = 1;
                    _upgradeProgress[index] = Text(button, string.Empty, 13, SurvivorPalette.Muted);
                }
            }
            Text(dialog, "点击一张卡片升级，后续强化会保留已有能力。", 14, SurvivorPalette.Muted);
        }

        private void RefreshUpgradeHud()
        {
            if (_upgradeModal == null) return;
            _upgradeModal.style.display = IsChoosingUpgrade ? DisplayStyle.Flex : DisplayStyle.None;
            if (!IsChoosingUpgrade) return;
            _upgradeSubtitle.text = "LEVEL " + Stats.Level + " · 选择 1 个技能 · 剩余选择次数 " + PendingUpgrades;
            for (var i = 0; i < 4; i++)
            {
                var skill = (SkillKind)i;
                var tier = SkillTier(skill);
                var definition = _skills.Get(skill);
                var maxTier = definition.MaxTier;
                var available = CanUpgradeSkill(skill);
                var nextTier = Mathf.Min(tier + 1, maxTier);
                var upgrade = definition.Upgrade(nextTier);
                _upgradeButtons[i].SetEnabled(available);
                _upgradeButtons[i].style.opacity = available ? 1 : 0.45f;
                _upgradeSkillTitles[i].text = SkillName(skill);
                _upgradeNames[i].text = nextTier == 0 ? "暂无强化" : "T" + nextTier + "  " + UpgradeName(skill, nextTier);
                _upgradeDescriptions[i].text = "基础：" + definition.description + (upgrade == null ? "" : "\n\n本次强化：" + upgrade.additionalDescription);
                _upgradeButtons[i].tooltip = _skills.Description(skill, nextTier);
                _upgradeProgress[i].text = SkillLevel(skill) == 0 ? "技能已关闭" : available ? "已升级 " + tier + "/" + maxTier + " · 点击选择" : "已升级 " + tier + "/" + maxTier + " · 已满级";
            }
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
            var experience = Mathf.Clamp01(Stats.Experience / (float)Stats.ExperienceRequired);
            var ease = immediate ? 1 : 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            _shownHealth = Mathf.Lerp(_shownHealth, health, ease);
            _shownExperience = Mathf.Lerp(_shownExperience, experience, ease);
            _healthFill.style.width = Length.Percent(_shownHealth * 100);
            _experienceFill.style.width = Length.Percent(_shownExperience * 100);
            _healthText.text = "HP " + Mathf.CeilToInt(Stats.Health) + " / " + Mathf.CeilToInt(Stats.MaxHealth) + (Stats.GodMode ? "  [GOD]" : string.Empty);
            _experienceText.text = "LEVEL " + Stats.Level + "    " + Stats.Experience + " / " + Stats.ExperienceRequired;
            var remaining = Mathf.Max(0, Mathf.CeilToInt(RunDuration - Stats.Elapsed));
            _timeText.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            _statusText.text = "WAVE " + Stats.Wave + "   KILLS " + Stats.Kills + "   敌人 " + EnemyCount;
            for (var i = 0; i < 4; i++)
            {
                var cooldown = SkillCooldown((SkillKind)i);
                var tier = SkillTier((SkillKind)i);
                var stage = tier == 0 ? "基础" : "T" + tier + " " + UpgradeName((SkillKind)i, tier);
                _skillTitles[i].text = SkillName((SkillKind)i) + " / AUTO";
                _skillTitles[i].tooltip = _skills.Description((SkillKind)i, tier);
                _skillNames[i].text = _skillLevels[i] == 0 ? "关闭" : stage + (cooldown > 0 ? "  " + cooldown.ToString("0.0") + "s" : "  READY");
            }
            var dash = Mathf.Max(0, _dashReadyAt - Stats.Elapsed);
            _skillNames[4].text = dash > 0 ? dash.ToString("0.0") + "s" : "READY";
            _toastText.style.display = Time.unscaledTime < _toastUntil ? DisplayStyle.Flex : DisplayStyle.None;
            _modal.style.display = (IsPaused || HasEnded) && !IsChoosingUpgrade ? DisplayStyle.Flex : DisplayStyle.None;
            _resume.style.display = HasEnded ? DisplayStyle.None : DisplayStyle.Flex;
            _modalTitle.text = HasEnded ? HasWon ? "SURVIVED" : "RUN ENDED" : "PAUSED";
            _modalTitle.style.color = HasEnded && !HasWon ? SurvivorPalette.Enemy : SurvivorPalette.Player;
            _modalDescription.text = HasEnded ? "击杀 " + Stats.Kills + " · 等级 " + Stats.Level + " · 最佳 " + _bestKills + "\n重新开始即可继续测试。" : "世界模拟已暂停。\nGameDevTools 和宏仍可操作。";
            RefreshUpgradeHud();
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
