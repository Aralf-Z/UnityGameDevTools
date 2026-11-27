using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Sanmon.SurvivorDemo.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sanmon.SurvivorDemo.Tests
{
    public sealed class SurvivorConfigurationTests
    {
        private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
        private SurvivorConfiguration _configuration;

        [SetUp]
        public void LoadConfiguration() => _configuration = SurvivorConfiguration.Load();

        [Test]
        public void StreamingAssetsContainsAllCatalogs()
        {
            Assert.That(SurvivorSkills.AssetPath, Does.StartWith(SurvivorConfiguration.ASSET_DIRECTORY));
            Assert.That(_configuration.skills.skills.Length, Is.EqualTo(8));
            Assert.That(_configuration.enemies.enemies.Length, Is.EqualTo(12));
            Assert.That(_configuration.waves.waves.Length, Is.EqualTo(10));
            Assert.That(File.Exists(Path.Combine(Application.dataPath, "Scripts/Resources/SurvivorDemo/Skills.json")), Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(19.99f, 0)]
        [TestCase(20, 1)]
        [TestCase(40, 2)]
        [TestCase(180, 9)]
        [TestCase(999, 9)]
        public void WaveChangesAtConfiguredTime(float elapsed, int index)
        {
            Assert.That(_configuration.waves.IndexAt(elapsed), Is.EqualTo(index));
        }

        [TestCase(EnemyKind.Grunt, 38, 2.7f, 8, 1)]
        [TestCase(EnemyKind.Runner, 22, 4.5f, 8, 1)]
        [TestCase(EnemyKind.Tank, 110, 1.65f, 20, 5)]
        public void EnemyDefaultsPreserveBalance(EnemyKind kind, float health, float speed, float damage, int experience)
        {
            var enemy = _configuration.enemies.Get(kind);
            Assert.That(enemy.HealthAtWave(1), Is.EqualTo(health));
            Assert.That(enemy.HealthAtWave(3), Is.EqualTo(health * 1.36f).Within(0.001f));
            Assert.That(enemy.moveSpeed, Is.EqualTo(speed));
            Assert.That(enemy.contactDamage, Is.EqualTo(damage));
            Assert.That(enemy.experience, Is.EqualTo(experience));
        }

        [Test]
        public void InvalidWaveOrderIsRejected()
        {
            _configuration.waves.waves[1].startTime = 0;
            Assert.Throws<ArgumentException>(_configuration.waves.Rebuild);
        }

        [Test]
        public void UnknownEnemyReferenceIsRejected()
        {
            _configuration.waves.waves[0].entrySpawns = new[] { new SurvivorSpawnGroup { enemy = "Unknown" } };
            Assert.Throws<ArgumentException>(() => _configuration.waves.ValidateEnemies(_configuration.enemies));
        }

        [Test]
        public void EmptyEnemyPoolIsRejected()
        {
            _configuration.waves.waves[0].enemyPool = Array.Empty<string>();
            Assert.Throws<ArgumentException>(_configuration.waves.Rebuild);
        }

        [Test]
        public void DuplicateEnemyIdIsRejected()
        {
            _configuration.enemies.enemies[1].id = "Grunt";
            Assert.Throws<ArgumentException>(_configuration.enemies.Rebuild);
        }

        [Test]
        public void NonFiniteEnemyValueIsRejected()
        {
            _configuration.enemies.enemies[0].moveSpeed = float.NaN;
            Assert.Throws<ArgumentException>(_configuration.enemies.Rebuild);
        }

        [Test]
        public void CatalogsRoundTripThroughJson()
        {
            var copy = SurvivorConfiguration.Parse(JsonUtility.ToJson(_configuration.skills), JsonUtility.ToJson(_configuration.waves), JsonUtility.ToJson(_configuration.enemies), JsonUtility.ToJson(_configuration.levels), JsonUtility.ToJson(_configuration.enemySkills));
            Assert.That(copy.skills.Resolve(SkillKind.Bolt, 2).damageMultiplier, Is.EqualTo(_configuration.skills.Resolve(SkillKind.Bolt, 2).damageMultiplier));
            Assert.That(copy.waves.waves[2].enemyPool, Does.Contain("Tank"));
            Assert.That(copy.enemies.Get(EnemyKind.Tank).color, Is.EqualTo(_configuration.enemies.Get(EnemyKind.Tank).color));
        }

        [Test]
        public void WaveHotReloadDoesNotReplayEntrySpawns()
        {
            var root = new GameObject("Configuration test");
            var arena = root.AddComponent<SurvivorArena>();
            try
            {
                SetField(arena, "_waves", _configuration.waves);
                SetField(arena, "_enemyCatalog", _configuration.enemies);
                SetField(arena, "_waveConfigurationJson", JsonUtility.ToJson(_configuration.waves));
                SetField(arena, "_initialized", true);
                SetField(arena, "_nextSpawn", 30f);
                SetField(arena, "_hasAutoSpawned", true);
                arena.Stats.Elapsed = 25;
                var next = SurvivorWaves.Parse(JsonUtility.ToJson(_configuration.waves));
                next.waves[1].spawnInterval *= 2;
                Assert.That(arena.ApplyWaveConfiguration(JsonUtility.ToJson(next)), Is.True);
                Assert.That(arena.Stats.Wave, Is.EqualTo(2));
                Assert.That(arena.EnemyCount, Is.Zero);
                Assert.That(arena.ApplyWaveConfiguration(JsonUtility.ToJson(next)), Is.False);
                next.waves[0].enemyPool = new[] { "Unknown" };
                Assert.Throws<ArgumentException>(() => arena.ApplyWaveConfiguration(JsonUtility.ToJson(next)));
                Assert.That(arena.WaveCatalog.waves[0].enemyPool[0], Is.EqualTo("Grunt"));
            }
            finally
            {
                SetField(arena, "_initialized", false);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SwitchingSectionsPreservesUnsavedFormAndRawJson()
        {
            var window = ScriptableObject.CreateInstance<SurvivorConfigEditorWindow>();
            try
            {
                window.CreateGUI();
                var document = (ScriptableObject)GetField(window, "document");
                var serialized = new SerializedObject(document);
                serialized.FindProperty("catalog.skills.Array.data[0].baseStats.damageMultiplier").floatValue = 123;
                serialized.ApplyModifiedProperties();
                SetField(window, "section", 1);
                SetField(window, "rawMode", true);
                window.CreateGUI();
                var raw = window.rootVisualElement.Q<TextField>();
                raw.value = "{ invalid wave json }";
                SetField(window, "section", 2);
                window.CreateGUI();
                SetField(window, "section", 1);
                window.CreateGUI();
                Assert.That(window.rootVisualElement.Q<TextField>().value, Is.EqualTo("{ invalid wave json }"));
                serialized.Update();
                Assert.That(serialized.FindProperty("catalog.skills.Array.data[0].baseStats.damageMultiplier").floatValue, Is.EqualTo(123));
                Assert.That(window.hasUnsavedChanges, Is.True);
                window.DiscardChanges();
                serialized.Dispose();
            }
            finally
            {
                window.DiscardChanges();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void InvalidRawJsonPreventsAllFilesBeingSaved()
        {
            var window = ScriptableObject.CreateInstance<SurvivorConfigEditorWindow>();
            var original = SurvivorConfiguration.ReadJson("Skills.json");
            try
            {
                var source = (string[])GetField(window, "rawJson");
                var raw = (bool[])GetField(window, "rawDirty");
                var dirty = (bool[])GetField(window, "dirty");
                source[0] = original.Replace("21.0", "999.0");
                source[1] = "{ invalid }";
                raw[0] = raw[1] = dirty[0] = dirty[1] = true;
                window.SaveChanges();
                Assert.That(SurvivorConfiguration.ReadJson("Skills.json"), Is.EqualTo(original));
                Assert.That(GetField(window, "section"), Is.EqualTo(1));
            }
            finally
            {
                window.DiscardChanges();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static object GetField(object target, string name) => target.GetType().GetField(name, PRIVATE_INSTANCE).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PRIVATE_INSTANCE).SetValue(target, value);
    }
}
