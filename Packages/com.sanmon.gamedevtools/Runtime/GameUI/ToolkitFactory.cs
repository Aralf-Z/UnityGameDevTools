using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace GameConsole.UI
{
    /// <summary> 开发工具的唯一 UI 工厂, 资源依赖由 Resources 纳入 Player. </summary>
    [Preserve]
    public static partial class ToolkitFactory
    {
        private const string _catalogPath = "Style";

        private const string _sharedRoot = "UIStyle/Shared/";

        /// <summary> 当前已提交的风格资源路径, 准备中的选择不会改变此值. </summary>
        public static string ActiveUIStyle { get; private set; } = string.Empty;

        /// <summary> 当前已提交的字体键, 准备中的选择不会改变此值. </summary>
        public static string ActiveFont { get; private set; } = string.Empty;

        internal static IReadOnlyList<ToolkitUIStyleEntry> UIStyles => _uiStyles;

        internal static IReadOnlyList<ToolkitFontEntry> Fonts => _externalFonts.Count == 0
            ? _fonts
            : _fonts.Concat(_externalFonts.Where(entry => entry.Value != null)
                .Select(entry => new ToolkitFontEntry
                {
                    name = entry.Key.Substring(Config.EXTERNAL_FONT_PREFIX.Length),
                    path = entry.Key
                })).ToArray();

        private static readonly Dictionary<string, Font> _externalFonts = new(StringComparer.Ordinal);

        private static ThemeStyleSheet _sharedTheme;

        private static PanelSettings _sharedPanel;

        private static StyleSheet _layoutSheet;

        private static StyleSheet _controlsSheet;

        private static ToolkitUIStyleEntry[] _uiStyles = Array.Empty<ToolkitUIStyleEntry>();

        private static StyleSheet _uiStyleSheet;

        private static ToolkitFontEntry[] _fonts = Array.Empty<ToolkitFontEntry>();

        private static Font _font;

        /// <summary> 配置无可用资源时报告初始化错误, 创建异常时释放临时对象. </summary>
        [Preserve]
        public static ToolkitView Create(Transform owner)
        {
            if (!Application.isPlaying || owner == null) return null;

            var catalog = ToolkitResources.Load<Style>(_catalogPath);

            _uiStyles = BuildUIStyles(catalog?.uiStyles);
            _fonts = BuildFonts(catalog?.fonts);
            ToolkitLocalization.Initialize(catalog?.languages, Config.UILanguage);
            if (string.IsNullOrEmpty(ToolkitLocalization.ActiveLanguage))
            {
                throw new InvalidOperationException(ToolkitLocalization.Get("style.languages.unavailable", "Style.languages has no loadable language package."));
            }

            LoadSharedResources(out var tree, out var theme, out var settings);
            if (!TryLoadInitialUIStyle(Config.UIStyle, out var selected, out var sheet))
            {
                throw new InvalidOperationException(ToolkitLocalization.Get("style.uiStyles.unavailable"));
            }

            LoadFont(Config.UIFont);

            var languageChanged = Config.UILanguage != ToolkitLocalization.ActiveLanguage;
            var saveFont = !Config.IsExternalFont(Config.UIFont) && Config.UIFont != ActiveFont;

            Config.UILanguage = ToolkitLocalization.ActiveLanguage;
            if (languageChanged || Config.UIStyle != selected || saveFont)
            {
                Config.UIStyle = selected;
                if (saveFont)
                {
                    Config.UIFont = ActiveFont;
                }

                Config.Save();
            }

            ActiveUIStyle = selected;
            _uiStyleSheet = sheet;
            InitializeAppearanceCache(sheet);
            GameObject window = null;
            PanelSettings instance = null;

            try
            {
                instance = UnityEngine.Object.Instantiate(settings);
                instance.hideFlags = HideFlags.DontSave;
                instance.themeStyleSheet = theme;
                window = new GameObject("GameDevTools (UI Toolkit)")
                { hideFlags = HideFlags.DontSave };
                window.SetActive(false);
                window.transform.SetParent(owner, false);
                var document = window.AddComponent<UIDocument>();

                document.panelSettings = instance;
                document.visualTreeAsset = tree;
                document.sortingOrder = 30001;
                window.SetActive(true);
                NormalizeThemeSheets(document.rootVisualElement);
                ApplyFont(document.rootVisualElement);
                ToolkitLocalizedBindings.Bind(document.rootVisualElement);
                return new ToolkitView(window, document, instance);
            }
            catch
            {
                if (window != null)
                {
                    var root = window.GetComponent<UIDocument>().rootVisualElement;

                    ToolkitMixedText.Release(root);
                }

                if (window != null)
                {
                    window.SetActive(false);
                    UnityEngine.Object.Destroy(window);
                }

                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                throw;
            }
        }

        internal static VisualTreeAsset RequireTemplate(string name)
        {
            if (_templates.TryGetValue(name, out var cached) && cached != null) return cached;

            var template = ToolkitResources.Load<VisualTreeAsset>(_sharedRoot + name);

            if (template == null)
            {
                throw new InvalidOperationException(ToolkitLocalization.Format("ui.Factory.RequireTemplate", ("p0", (object)(name))));
            }

            _templates[name] = template;
            return template;
        }

        /// <summary> 优先保留有效的保存选择, 未设置或未登记时使用 Style 列表首项. </summary>
        internal static string ResolveUIStylePath(string path)
        {
            path = NormalizeResourcePath(path);
            return _uiStyles.Any(style => style.path == path)
                ? path
                : _uiStyles.FirstOrDefault()?.path ?? string.Empty;
        }

        internal static string GetUIStyleName(string path)
        {
            var selected = ResolveUIStylePath(path);
            var entry = _uiStyles.FirstOrDefault(style => style.path == selected);

            return entry?.name ?? string.Empty;
        }

        /// <summary> 只建立配置中的独立快照, 保留首项作为默认, 不补入固定风格. </summary>
        internal static ToolkitUIStyleEntry[] BuildUIStyles(IEnumerable<ToolkitUIStyleEntry> source)
        {
            var result = new List<ToolkitUIStyleEntry>();
            var paths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in source ?? Array.Empty<ToolkitUIStyleEntry>())
            {
                var path = NormalizeResourcePath(entry?.path);

                if (path == null || !paths.Add(path))
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(entry.name)
                    ? path.Substring(path.LastIndexOf('/') + 1)
                    : entry.name.Trim();

                result.Add(new ToolkitUIStyleEntry { name = name, path = path });
            }

            return result.ToArray();
        }

        /// <summary> 只接受 Font 目录内不带扩展名的资源键, 保留首项作为默认. </summary>
        internal static ToolkitFontEntry[] BuildFonts(IEnumerable<ToolkitFontEntry> source)
        {
            var result = new List<ToolkitFontEntry>();
            var paths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in source ?? Array.Empty<ToolkitFontEntry>())
            {
                var path = NormalizeResourcePath(entry?.path);

                if (path == null || !path.StartsWith("Font/", StringComparison.Ordinal) || path.Length <= 5 || System.IO.Path.HasExtension(path) || !paths.Add(path))
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(entry.name)
                    ? path.Substring(path.LastIndexOf('/') + 1)
                    : entry.name.Trim();

                result.Add(new ToolkitFontEntry { name = name, path = path });
            }

            return result.ToArray();
        }

        internal static string ResolveFontPath(string path)
        {
            if (Config.IsExternalFont(path))
            {
                return _externalFonts.TryGetValue(path, out var font) && font != null
                    ? path
                    : _fonts.FirstOrDefault()?.path ?? string.Empty;
            }

            path = NormalizeResourcePath(path);
            return _fonts.Any(entry => entry.path == path)
                ? path
                : _fonts.FirstOrDefault()?.path ?? string.Empty;
        }

        internal static string GetFontName(string path)
        {
            if (string.IsNullOrEmpty(path)) return ToolkitLocalization.Get("ui.Options.uiFontName.text");

            var selected = ResolveFontPath(path);
            if (Config.IsExternalFont(selected)) return selected.Substring(Config.EXTERNAL_FONT_PREFIX.Length);

            var entry = _fonts.FirstOrDefault(value => value.path == selected);

            return entry?.name ?? string.Empty;
        }

        internal static bool TryRegisterFont(string key, Font font)
        {
            if (_externalFonts.TryGetValue(key, out var existing)) return existing == font;

            _externalFonts.Add(key, font);
            return true;
        }

        internal static void ReleaseViewResources(bool keepRegisteredFonts)
        {
            if (!keepRegisteredFonts)
            {
                _externalFonts.Clear();
            }

            _sharedTheme = null;
            _sharedPanel = null;
            _layoutSheet = null;
            _controlsSheet = null;
            _uiStyleSheet = null;
            _uiStyles = Array.Empty<ToolkitUIStyleEntry>();
            _fonts = Array.Empty<ToolkitFontEntry>();
            _font = null;
            ActiveUIStyle = string.Empty;
            ActiveFont = string.Empty;
            _templates.Clear();
            _fontCache.Clear();
            _appearanceCache.Clear();
            ReleaseTrackedMenus();
            ToolkitIcons.ReleaseCache();
            ToolkitLocalization.ReleaseCache();
            ToolkitHelpContents.ReleaseCache();
            ToolkitRuntimeFonts.Release();
        }

        internal static void ApplyFont(VisualElement root)
        {
            ApplyFont(root, _font);
        }

        private static void ApplyFont(VisualElement root, Font source)
        {
            if (root == null) return;
            if (source == null)
            {
                root.style.unityFont = StyleKeyword.Null;
                root.style.unityFontDefinition = StyleKeyword.Null;
                return;
            }

            root.style.unityFont = source;
            var asset = ToolkitRuntimeFonts.Get(source);

            root.style.unityFontDefinition = FontDefinition.FromSDFFont(asset);
        }

        internal static int RefreshFontResources(VisualElement root, int revision)
        {
            if (_font == null || root == null) return revision;

            var asset = ToolkitRuntimeFonts.Get(_font);

            if (revision == ToolkitRuntimeFonts.Revision && root.style.unityFontDefinition.value.fontAsset == asset) return revision;

            root.style.unityFontDefinition = StyleKeyword.Null;
            ApplyFont(root);
            foreach (var reference in _menuRoots)
            {
                if (reference.TryGetTarget(out var menu))
                {
                    menu.style.unityFontDefinition = StyleKeyword.Null;
                    ApplyFont(menu);
                }
            }

            AppearanceVersion++;
            ToolkitMixedText.RefreshAll();
            return ToolkitRuntimeFonts.Revision;
        }

        // 先尝试保存选择, 失败后仅从 Style 首项依次查找可用风格.
        private static bool TryLoadInitialUIStyle(string preferred, out string selected, out StyleSheet sheet)
        {
            foreach (var path in CandidatePaths(ResolveUIStylePath(preferred), _uiStyles.Select(entry => entry.path)))
            {
                sheet = ToolkitResources.Load<StyleSheet>(path + "/GameDevToolUIStyles");
                if (sheet == null)
                {
                    continue;
                }

                selected = path;
                return true;
            }

            selected = null;
            sheet = null;
            return false;
        }

        // 公共模板只缓存一份, 初次创建检查基础面板, 切换风格不再加载这些资源.
        private static void LoadSharedResources(out VisualTreeAsset tree, out ThemeStyleSheet theme, out PanelSettings settings)
        {
            foreach (var name in _templateNames)
            {
                RequireTemplate(name);
            }

            tree = RequireTemplate("GameDevTool");
            if (_sharedTheme == null)
            {
                _sharedTheme = ToolkitResources.Load<ThemeStyleSheet>(_sharedRoot + "GameDevToolTheme");
            }

            if (_sharedPanel == null)
            {
                _sharedPanel = ToolkitResources.Load<PanelSettings>(_sharedRoot + "GameDevToolPanel");
            }

            if (_layoutSheet == null)
            {
                _layoutSheet = ToolkitResources.Load<StyleSheet>(_sharedRoot + "GameDevToolLayout");
            }

            if (_controlsSheet == null)
            {
                _controlsSheet = ToolkitResources.Load<StyleSheet>(_sharedRoot + "ToolkitControls");
            }

            theme = _sharedTheme;
            settings = _sharedPanel;
            if (theme == null || settings == null || _layoutSheet == null || _controlsSheet == null)
            {
                throw new InvalidOperationException(ToolkitLocalization.Get("style.uiStyles.unavailable"));
            }
        }

        // 优先所选字体, 缺失时按 Style 顺序尝试内置字体.
        private static void LoadFont(string selected)
        {
            foreach (var path in FontCandidates(selected))
            {
                Font font;
                if (Config.IsExternalFont(path))
                {
                    _externalFonts.TryGetValue(path, out font);
                }
                else
                {
                    font = ToolkitResources.Load<Font>(path);
                }

                if (font == null)
                {
                    continue;
                }

                _font = font;
                ActiveFont = path;
                return;
            }

            _font = null;
            ActiveFont = string.Empty;
        }

        private static IEnumerable<string> FontCandidates(string selected) => CandidatePaths(ResolveFontPath(selected), _fonts.Select(entry => entry.path)
            .Concat(_externalFonts.Where(entry => entry.Value != null).Select(entry => entry.Key)));

        // 所选项优先, 失败后从配置首项开始, 每个路径只尝试一次且不引入固定后备.
        private static IEnumerable<string> CandidatePaths(string selected, IEnumerable<string> configured) => new[]
        { selected }.Concat(configured)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.Ordinal);

        private static string NormalizeResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            path = path.Trim()
                .Replace('\\', '/')
                .Trim('/');
            return path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".." || part.Contains(':'))
                ? null
                : path;
        }
    }
}
