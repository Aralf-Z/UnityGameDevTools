using System;
using System.Collections;
using System.Linq;
using GameConsole;
using GameConsole.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GameDevTools.Tests
{
    public sealed class StyleFontTests
    {
        private Style _style;
        private ToolkitFontEntry[] _fonts;
        private string _savedStyle;
        private string _savedFont;
        private string _savedLanguage;
        private GameObject _owner;
        private ToolkitView _view;

        [SetUp]
        public void SetUp()
        {
            ToolkitFactory.ReleaseViewResources(false);
            _style = ToolkitResources.Load<Style>("Style");
            Assert.That(_style, Is.Not.Null);
            _fonts = _style.fonts;
            _savedStyle = Config.UIStyle;
            _savedFont = Config.UIFont;
            _savedLanguage = Config.UILanguage;
            Config.UIStyle = string.Empty;
            Config.UIFont = string.Empty;
            _owner = new GameObject("Style font test");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _view?.Dispose();
            UnityEngine.Object.Destroy(_owner);
            ToolkitFactory.ReleaseViewResources(false);
            if (_style != null) _style.fonts = _fonts;
            Config.UIStyle = _savedStyle;
            Config.UIFont = _savedFont;
            Config.UILanguage = _savedLanguage;
            Config.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstCreationUsesMapleMono()
        {
            _view = ToolkitFactory.Create(_owner.transform);
            Assert.That(_view, Is.Not.Null);
            Assert.That(ToolkitFactory.ActiveFont, Is.EqualTo("Font/MapleMono-Light"));
            var text = new Label("Maple font");
            text.style.fontSize = 15;
            Document.rootVisualElement.Add(text);
            _view.IsOpen = true;
            yield return null;
            Assert.That(Document.rootVisualElement.style.unityFontDefinition.value.fontAsset, Is.Not.Null);
            Assert.That(text.MeasureTextSize("Maple font", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SavedExternalFontTakesPriority()
        {
            var key = Config.EXTERNAL_FONT_PREFIX + "Saved font";
            Assert.That(ToolkitFactory.TryRegisterFont(key, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")), Is.True);
            Config.UIFont = key;
            _view = ToolkitFactory.Create(_owner.transform);
            Assert.That(ToolkitFactory.ActiveFont, Is.EqualTo(key));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoFontsCanCreateSwitchUIStylesAndRestoreLateRegistration()
        {
            _style.fonts = Array.Empty<ToolkitFontEntry>();
            var key = Config.EXTERNAL_FONT_PREFIX + "Late font";
            Config.UIFont = key;
            _view = ToolkitFactory.Create(_owner.transform);
            Assert.That(_view, Is.Not.Null);
            Assert.That(ToolkitFactory.ActiveFont, Is.Empty);
            Assert.That(Config.UIFont, Is.EqualTo(key));
            Assert.That(ToolkitFactory.GetFontName(string.Empty), Is.Not.Empty);
            var defaultText = new Label("Default font");
            defaultText.style.fontSize = 15;
            Document.rootVisualElement.Add(defaultText);
            _view.IsOpen = true;
            yield return null;
            Assert.That(defaultText.MeasureTextSize("Default font", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x, Is.GreaterThan(0));
            defaultText.RemoveFromHierarchy();

            var next = ToolkitFactory.UIStyles.First(entry => entry.path != ToolkitFactory.ActiveUIStyle).path;
            ToolkitAppearanceResources prepared = null;
            yield return ToolkitFactory.PrepareAppearance(next, string.Empty, result => prepared = result);
            Assert.That(prepared, Is.Not.Null);
            Assert.That(prepared.font, Is.Null);
            ToolkitFactory.CommitAppearance(prepared, Document.rootVisualElement, Document.panelSettings);
            Assert.That(ToolkitFactory.ActiveUIStyle, Is.EqualTo(next));
            Assert.That(ToolkitFactory.ActiveFont, Is.Empty);
            Assert.That(Document.rootVisualElement.style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null));

            Assert.That(ToolkitFactory.TryRegisterFont(key, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")), Is.True);
            _view.RestoreRegisteredFont(key);
            for (var frame = 0; frame < 120 && ToolkitFactory.ActiveFont != key; frame++)
            {
                _view.Tick();
                yield return null;
            }

            Assert.That(ToolkitFactory.ActiveFont, Is.EqualTo(key));
            Assert.That(Config.UIFont, Is.EqualTo(key));
        }

        private UIDocument Document => _owner.GetComponentInChildren<UIDocument>(true);
    }
}
