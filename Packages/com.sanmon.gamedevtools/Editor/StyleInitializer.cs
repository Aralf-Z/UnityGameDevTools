using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GameConsole.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameConsole.Editor
{
    /// <summary> 仅在缺少 Style 时导入参考模板, 已有配置只检查必需列表是否为空. </summary>
    [InitializeOnLoad]
    public sealed class StyleInitializer : AssetPostprocessor, IPreprocessBuildWithReport
    {
        internal const string AssetRoot = "Assets/Resources/Sanmon/GameDevTools";

        private const string TemplateFolder = "DefaultResources~";

        private static readonly Regex _guid = new(@"\bguid:\s*([0-9a-fA-F]{32})\b", RegexOptions.Compiled);

        private static readonly UTF8Encoding _utf8 = new(false);

        private static bool _queued;

        public int callbackOrder => 0;

        static StyleInitializer() => QueueInitialize();

        public void OnPreprocessBuild(BuildReport report) => InitializeProject();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
        {
            if (didDomainReload) QueueInitialize();
        }

        private static void QueueInitialize()
        {
            if (_queued) return;

            _queued = true;
            EditorApplication.delayCall += InitializeWhenReady;
        }

        private static void InitializeWhenReady()
        {
            _queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueInitialize();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode) InitializeProject();
        }

        private static void InitializeProject()
        {
            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Style).Assembly);
                if (package == null) throw new InvalidOperationException("Cannot locate the GameDevTools package.");

                Initialize(Path.Combine(package.resolvedPath, TemplateFolder), AssetRoot);
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameDevTools] Style initialization failed; the next initialization can retry.\n" + exception);
            }
        }

        // 允许测试使用独立的 Assets 目录, 不修改项目已有的工具资源.
        internal static Style Initialize(string templateRoot, string targetRoot)
        {
            var catalogPath = targetRoot + "/Style.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<Style>(catalogPath);
            if (catalog == null)
            {
                if (File.Exists(catalogPath)) throw new InvalidOperationException("Cannot load Style: " + catalogPath);

                CopyTemplates(templateRoot, targetRoot);
                catalog = AssetDatabase.LoadAssetAtPath<Style>(catalogPath);
                if (catalog == null) throw new InvalidOperationException("Cannot load the imported Style: " + catalogPath);
            }

            if (catalog.uiStyles == null || catalog.uiStyles.Length == 0)
            {
                Debug.LogWarning("[GameDevTools] Style.uiStyles is empty. Configure at least one UI style: " + catalogPath, catalog);
            }

            if (catalog.languages == null || catalog.languages.Length == 0)
            {
                Debug.LogWarning("[GameDevTools] Style.languages is empty. Configure at least one language: " + catalogPath, catalog);
            }

            return catalog;
        }

        private static void CopyTemplates(string templateRoot, string targetRoot)
        {
            if (!Directory.Exists(templateRoot) || !File.Exists(Path.Combine(templateRoot, "Style.asset")))
            {
                throw new DirectoryNotFoundException("Missing GameDevTools default templates: " + templateRoot);
            }

            var files = Directory.GetFiles(templateRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            var copies = new List<(string source, string target)>();

            // 先分配全部 GUID, 再复制内容, 保证前向引用和已有文件都指向目标目录.
            foreach (var source in Directory.GetDirectories(templateRoot, "*", SearchOption.AllDirectories).Concat(files))
            {
                var relative = source.Substring(templateRoot.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
                var target = targetRoot + "/" + relative;
                var exists = File.Exists(target) || Directory.Exists(target);
                var metaPath = source + ".meta";
                if (File.Exists(metaPath))
                {
                    var meta = File.ReadAllText(metaPath);
                    var match = _guid.Match(meta);
                    if (!match.Success) throw new InvalidDataException("Missing template GUID: " + metaPath);

                    var targetGuid = File.Exists(target + ".meta") ? _guid.Match(File.ReadAllText(target + ".meta")).Groups[1].Value : string.Empty;
                    if (exists && string.IsNullOrEmpty(targetGuid))
                    {
                        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                        targetGuid = AssetDatabase.AssetPathToGUID(target);
                    }

                    if (string.IsNullOrEmpty(targetGuid)) targetGuid = GUID.Generate().ToString();
                    guids[match.Groups[1].Value] = targetGuid;
                    if (!exists) metadata[target] = meta;
                }

                if (!exists && File.Exists(source)) copies.Add((source, target));
            }

            string Remap(string content) => _guid.Replace(content, match => guids.TryGetValue(match.Groups[1].Value, out var replacement)
                ? "guid: " + replacement
                : match.Value);

            AssetDatabase.StartAssetEditing();
            try
            {
                Directory.CreateDirectory(targetRoot);
                foreach (var directory in Directory.GetDirectories(templateRoot, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(Path.Combine(targetRoot, directory.Substring(templateRoot.TrimEnd('/', '\\').Length + 1)));
                }

                foreach (var entry in metadata)
                {
                    File.WriteAllText(entry.Key + ".meta", Remap(entry.Value), _utf8);
                }

                // Style.asset 是初始化完成标记; 其他资源失败时不留下半完成的配置.
                foreach (var copy in copies.OrderBy(entry => entry.target == targetRoot + "/Style.asset" ? 1 : 0))
                {
                    switch (Path.GetExtension(copy.source).ToLowerInvariant())
                    {
                        case ".asset":
                        case ".uxml":
                        case ".uss":
                        case ".tss":
                            File.WriteAllText(copy.target, Remap(File.ReadAllText(copy.source)), _utf8);
                            break;
                        default:
                            File.Copy(copy.source, copy.target, false);
                            break;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
