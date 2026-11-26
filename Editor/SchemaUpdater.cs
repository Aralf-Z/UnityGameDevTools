using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using GameConsole.UI;

namespace GameConsole.Editor
{
    /// <summary> 自动追加风格, 字体和语言, 保留 Schema 中的手动名称和排列, 构建前再次同步. </summary>
    [InitializeOnLoad]
    public sealed class SchemaUpdater : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string RESOURCE_DIRECTORY = "Runtime/GameUI/Resources";

        private const string STYLE_FILE = "GameDevToolStyles.uss"; // 风格仅提供外观, 模板和面板位于 Shared.

        private static bool _queued;

        public int callbackOrder => 0;

        static SchemaUpdater() => QueueRefresh();

        public void OnPreprocessBuild(BuildReport report) => Refresh();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!TryGetResourceDirectory(out var resourceDirectory, out _)) return;

            var catalogPath = resourceDirectory + "/Schema.asset";

            if (imported.Concat(deleted)
                .Concat(moved)
                .Concat(movedFrom)
                .Any(path => path.StartsWith(resourceDirectory + "/", StringComparison.Ordinal) && path != catalogPath && path != catalogPath + ".meta"))
            {
                QueueRefresh();
            }
        }

        private static void QueueRefresh()
        {
            if (_queued) return;

            _queued = true;
            EditorApplication.delayCall += RefreshWhenReady;
        }

        private static void RefreshWhenReady()
        {
            _queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueRefresh();
                return;
            }

            Refresh();
        }

        // 仅追加尚未登记的目录, 不覆盖显示名称, 不删除手动配置的映射或改变其顺序.
        private static void Refresh()
        {
            if (!TryGetResourceDirectory(out var resourceDirectory, out var root)) return;

            var catalogPath = resourceDirectory + "/Schema.asset";
            var fontDirectory = resourceDirectory + "/Font";

            if (!Directory.Exists(root)) return;

            var styleRoot = Path.Combine(root, "Style");
            var paths = Directory.Exists(styleRoot)
                ? Directory.EnumerateDirectories(styleRoot).Where(directory => File.Exists(Path.Combine(directory, STYLE_FILE))).Select(directory => "Style/" + Path.GetFileName(directory)).OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<Schema>(catalogPath);
            var entries = (catalog?.styles ?? Array.Empty<ToolkitStyleEntry>()).ToList();
            var fonts = (catalog?.fonts ?? Array.Empty<ToolkitFontEntry>()).ToList();
            var languages = (catalog?.languages ?? Array.Empty<ToolkitLanguageEntry>()).ToList();
            var registered = new HashSet<string>(entries.Where(entry => entry != null).Select(entry => PathKey(entry.path)), StringComparer.Ordinal);
            var changed = false;

            foreach (var path in paths)
            {
                if (registered.Add(path))
                {
                    entries.Add(new ToolkitStyleEntry { name = Path.GetFileName(path), path = path });
                    changed = true;
                }
            }

            var fontPaths = AssetDatabase.IsValidFolder(fontDirectory)
                ? AssetDatabase.FindAssets("t:Font", new[] { fontDirectory }).Select(AssetDatabase.GUIDToAssetPath).Select(path => Path.ChangeExtension(path.Substring(resourceDirectory.Length + 1), null).Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            var registeredFonts = new HashSet<string>(fonts.Where(entry => entry != null).Select(entry => PathKey(entry.path)), StringComparer.Ordinal);

            foreach (var path in fontPaths)
            {
                if (registeredFonts.Add(path))
                {
                    fonts.Add(new ToolkitFontEntry { name = Path.GetFileName(path), path = path });
                    changed = true;
                }
            }

            var languageRoot = Directory.EnumerateDirectories(root)
                .FirstOrDefault(directory => string.Equals(Path.GetFileName(directory), "localization", StringComparison.OrdinalIgnoreCase));

            if (languageRoot != null)
            {
                foreach (var directory in Directory.EnumerateDirectories(languageRoot)
                    .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var code = Path.GetFileName(directory);

                    if (!new[]
                    { "ui", "help", "macro" }.All(module => File.Exists(Path.Combine(directory, module + ".json"))) || languages.Any(entry => entry != null && entry.code == code))
                    {
                        continue;
                    }

                    languages.Add(new ToolkitLanguageEntry { code = code, name = code, path = Path.GetFileName(languageRoot) + "/" + code });
                    changed = true;
                }
            }

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Schema>();
                catalog.styles = entries.ToArray();
                catalog.fonts = fonts.ToArray();
                catalog.languages = languages.ToArray();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            else if (changed)
            {
                catalog.styles = entries.ToArray();
                catalog.fonts = fonts.ToArray();
                catalog.languages = languages.ToArray();
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        // 包缓存视为只读, Git 和 Registry 安装使用随包提交的 Schema.
        private static bool TryGetResourceDirectory(out string assetDirectory, out string physicalDirectory)
        {
            assetDirectory = null;
            physicalDirectory = null;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Schema).Assembly);

            if (package == null || (package.source != PackageSource.Local && package.source != PackageSource.Embedded)) return false;

            assetDirectory = package.assetPath + "/" + RESOURCE_DIRECTORY;
            physicalDirectory = Path.Combine(package.resolvedPath, RESOURCE_DIRECTORY);
            return Directory.Exists(physicalDirectory);
        }

        // 与运行时使用相同的路径分隔符约定, 保留资源中作者填写的原始值.
        private static string PathKey(string path) => (path ?? string.Empty).Trim()
            .Replace('\\', '/')
            .Trim('/');
    }
}
