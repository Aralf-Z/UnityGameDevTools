using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;
using GameConsole;
using GameConsole.UI;
using RedSaw.CommandLineInterface;

namespace GameDevTools.Tests
{
    public sealed class PackageCompatibilityTests
    {
        [Test]
        public void RuntimeBelongsToSanmonPackage()
        {
            var package = PackageInfo.FindForAssembly(typeof(GameDevToolsController).Assembly);

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.sanmon.gamedevtools"));
            Assert.That(package.version, Is.EqualTo("0.1.0"));
        }

        [Test]
        public void CommandsInConsumerAssemblyAreDiscovered()
        {
            var system = new CommandSystem();

            Assert.That(typeof(PackageCompatibilityTests).Assembly, Is.Not.EqualTo(typeof(CommandSystem).Assembly));
            Assert.That(system.vm.AllCallables.Any(command => command.Name == "UpmConsumerCommand"), Is.True);
            Assert.That(system.vm.AllCallables.Any(command => command.Name == "Print"), Is.True);
        }

        [Test]
        public void PropertiesInConsumerAssemblyAreDiscovered()
        {
            var system = new CommandSystem();

            Assert.That(system.vm.AllProperties.Any(property => property.Name == "UpmConsumerProperty"), Is.True);
        }

        [Test]
        public void ValueParsersInConsumerAssemblyAreDiscovered()
        {
            var system = new CommandSystem();
            var error = system.Execute("UpmConsumerParsedCommand(\"17\")", out var result);

            Assert.That(error, Is.Null, error?.ToString());
            Assert.That(result, Is.EqualTo(17));
        }

        [Test]
        public void BundledSchemaResolvesAllResources()
        {
            var schema = Resources.Load<Schema>("Schema");

            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.styles, Is.Not.Empty);
            Assert.That(schema.fonts, Is.Not.Empty);
            Assert.That(schema.languages, Is.Not.Empty);
            foreach (var style in schema.styles)
            {
                Assert.That(Resources.Load<StyleSheet>(style.path + "/GameDevToolStyles"), Is.Not.Null, style.path);
            }

            foreach (var font in schema.fonts)
            {
                Assert.That(Resources.Load<Font>(font.path), Is.Not.Null, font.path);
            }

            foreach (var language in schema.languages)
            {
                foreach (var module in new[] { "ui", "help", "macro" })
                {
                    Assert.That(Resources.Load<TextAsset>(language.path + "/" + module), Is.Not.Null, language.path + "/" + module);
                }
            }
        }

        [Test]
        public void SharedTemplatesAndPanelSettingsCanBeLoaded()
        {
            var template = Resources.Load<VisualTreeAsset>("Style/Shared/GameDevTool");

            Assert.That(template, Is.Not.Null);
            Assert.That(template.CloneTree().Q<VisualElement>("toolkitWindow"), Is.Not.Null);
            Assert.That(Resources.Load<PanelSettings>("Style/Shared/GameDevToolPanel"), Is.Not.Null);
        }

        [Command("UpmConsumerCommand")]
        private static int ConsumerCommand() => 17;

        [CommandProperty("UpmConsumerProperty")]
        private static int ConsumerProperty => 17;

        [Command("UpmConsumerParsedCommand")]
        private static int ConsumerParsedCommand(ParsedValue value) => value.number;

        [CommandValueParser(typeof(ParsedValue))]
        private static bool ParseConsumerValue(string input, out object value)
        {
            var success = int.TryParse(input, out var number);

            value = new ParsedValue { number = number };
            return success;
        }

        private struct ParsedValue
        {
            public int number;
        }
    }
}
