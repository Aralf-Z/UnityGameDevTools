using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GameConsole.Editor;
using GameConsole.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GameDevTools.Tests
{
    public sealed class StyleInitializationTests
    {
        private const string Root = "Assets/__GameDevToolsStyleTests";

        private string _templates;

        [SetUp]
        public void SetUp()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Style).Assembly);
            _templates = Path.Combine(package.resolvedPath, "DefaultResources~");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void FirstInitializationCopiesTemplatesAndRemapsReferences()
        {
            var sourceHash = Fingerprint(_templates);
            var style = StyleInitializer.Initialize(_templates, Root);

            Assert.That(style.uiStyles, Has.Length.EqualTo(4));
            Assert.That(style.languages, Has.Length.EqualTo(2));
            Assert.That(style.fonts[0].path, Is.EqualTo("Font/MapleMono-Light"));
            Assert.That(AssetDatabase.AssetPathToGUID(Root + "/Style.asset"), Is.Not.EqualTo(ReadGuid(Path.Combine(_templates, "Style.asset.meta"))));
            Assert.That(AssetDatabase.AssetPathToGUID(Root + "/Font/MapleMono-Light.ttf"), Is.Not.EqualTo(ReadGuid(Path.Combine(_templates, "Font/MapleMono-Light.ttf.meta"))));

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UIStyle/Shared/GameDevToolPanel.asset");
            Assert.That(AssetDatabase.GetAssetPath(panel.themeStyleSheet), Is.EqualTo(Root + "/UIStyle/Shared/GameDevToolTheme.tss"));
            Assert.That(AssetDatabase.GetDependencies(Root + "/UIStyle/Shared/GameDevTool.uxml", true), Does.Contain(Root + "/UIStyle/Shared/ToolkitControls.uss"));
            Assert.That(Fingerprint(_templates), Is.EqualTo(sourceHash));

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Style).Assembly);
            Assert.That(AssetDatabase.LoadAssetAtPath<Style>(package.assetPath + "/DefaultResources~/Style.asset"), Is.Null);
        }

        [Test]
        public void ExistingFilesArePreservedAndUsedByCopiedDependencies()
        {
            const string relative = "/UIStyle/Shared/GameDevToolTheme.tss";
            Directory.CreateDirectory(Root + "/UIStyle/Shared");
            const string custom = "/* custom standalone theme */\n";
            File.WriteAllText(Root + relative, custom);
            AssetDatabase.ImportAsset(Root + relative, ImportAssetOptions.ForceSynchronousImport);

            StyleInitializer.Initialize(_templates, Root);
            Assert.That(File.ReadAllText(Root + relative), Is.EqualTo(custom));
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UIStyle/Shared/GameDevToolPanel.asset");
            Assert.That(AssetDatabase.GetAssetPath(panel.themeStyleSheet), Is.EqualTo(Root + relative));

            var before = Fingerprint(Root);
            StyleInitializer.Initialize(_templates, Root);
            Assert.That(Fingerprint(Root), Is.EqualTo(before));
        }

        [Test]
        public void ExistingStyleDoesNotValidatePathsOrAppendResources()
        {
            var style = ScriptableObject.CreateInstance<Style>();
            style.uiStyles = new[] { new ToolkitUIStyleEntry { name = "Custom", path = "UIStyle/Missing" } };
            style.languages = new[] { new ToolkitLanguageEntry { code = "custom", path = "Localization/Missing" } };
            style.fonts = null;
            AssetDatabase.CreateAsset(style, Root + "/Style.asset");
            Directory.CreateDirectory(Root + "/UIStyle/New");
            File.WriteAllText(Root + "/UIStyle/New/GameDevToolUIStyles.uss", "Label { color: white; }");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Assert.That(StyleInitializer.Initialize("missing-templates", Root), Is.SameAs(style));
            Assert.That(style.uiStyles, Has.Length.EqualTo(1));
            Assert.That(style.uiStyles[0].name, Is.EqualTo("Custom"));
            Assert.That(style.languages, Has.Length.EqualTo(1));
            Assert.That(style.fonts, Is.Null);
            Assert.That(Directory.Exists(Root + "/Font"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EmptyRequiredListsOnlyWarnAndDoNotRepair()
        {
            var style = ScriptableObject.CreateInstance<Style>();
            style.uiStyles = null;
            style.fonts = null;
            AssetDatabase.CreateAsset(style, Root + "/Style.asset");
            LogAssert.Expect(LogType.Warning, new Regex(@"Style\.uiStyles is empty"));
            LogAssert.Expect(LogType.Warning, new Regex(@"Style\.languages is empty"));

            StyleInitializer.Initialize("missing-templates", Root);
            Assert.That(style.uiStyles, Is.Null);
            Assert.That(style.languages, Is.Empty);
            Assert.That(style.fonts, Is.Null);
            Assert.That(Directory.GetFiles(Root, "*", SearchOption.AllDirectories), Has.Length.EqualTo(2));
        }

        [Test]
        public void MissingTemplatesCanBeRetriedWithoutPartialStyle()
        {
            Assert.Throws<DirectoryNotFoundException>(() => StyleInitializer.Initialize("missing-templates", Root));
            Assert.That(File.Exists(Root + "/Style.asset"), Is.False);
            Assert.That(StyleInitializer.Initialize(_templates, Root), Is.Not.Null);
        }

        private static string ReadGuid(string path) => Regex.Match(File.ReadAllText(path), @"guid: ([0-9a-f]{32})").Groups[1].Value;

        private static string Fingerprint(string root)
        {
            using var hash = SHA256.Create();
            return string.Join("\n", Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path.Substring(root.Length) + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)))));
        }
    }
}
