using System.Collections;
using System.Linq;
using GameConsole;
using GameConsole.Extension;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sanmon.SurvivorDemo.Tests
{
    public sealed class SurvivorIntegrationTests
    {
        private GameObject _owner;
        private GameDevToolsController _tools;
        private SurvivorArena _arena;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _owner = new GameObject("Survivor integration test");
            _tools = _owner.AddComponent<GameDevToolsController>();
            _arena = _owner.AddComponent<SurvivorArena>();
            _arena.Initialize(_tools, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            _arena.AutoSpawn = _arena.AutoAttack = false;
            _arena.Stats.GodMode = true;
            _arena.enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_owner);
            yield return null;
            Assert.That(SurvivorArena.Instance, Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1).Within(0.001));
        }

        private object Execute(string text)
        {
            var error = _tools.Console.commandSystem.Execute(text, out var result);
            Assert.That(error, Is.Null, error?.ToString());
            return result;
        }

        [UnityTest]
        public IEnumerator MovementPauseAndToolPropertiesWorkTogether()
        {
            _arena.ClearEnemies(false);
            Execute("@DemoSpeed = 8");
            _arena.TickSimulation(0.1f, Vector2.right);
            Assert.That(_arena.PlayerPosition.x, Is.EqualTo(0.8f).Within(0.001));
            Execute("@DemoTimeScale = 0.5");
            Execute("DemoPause(true)");
            var time = _arena.Stats.Elapsed;
            var position = _arena.PlayerPosition;
            _arena.TickSimulation(0.1f, Vector2.one);
            Assert.That(_arena.PlayerPosition, Is.EqualTo(position));
            Assert.That(_arena.Stats.Elapsed, Is.EqualTo(time));
            Assert.That(Time.timeScale, Is.Zero);
            Execute("@DemoHealth = 70");
            Assert.That(_arena.Stats.Health, Is.EqualTo(70));
            Execute("DemoPause(false)");
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Execute("DemoTeleport(\"6,3\")");
            Assert.That(_arena.PlayerPosition.x, Is.EqualTo(6).Within(0.001));
            Assert.That(_arena.PlayerPosition.z, Is.EqualTo(3).Within(0.001));
            Assert.That(_tools.Console.commandSystem.Execute("DemoTeleport(\"NaN,3\")", out _), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkillsExperienceEndStatesAndRestartArePlayable()
        {
            _arena.ClearEnemies(false);
            Execute("@DemoDamage = 10");
            Execute("DemoSpawnAt(\"2,0\", \"Grunt\", 5)");
            Assert.That(_arena.EnemyCount, Is.EqualTo(5));
            Execute("DemoCast(\"Nova\")");
            Assert.That(_arena.EnemyCount, Is.Zero);
            Assert.That(_arena.Stats.Kills, Is.EqualTo(5));
            Execute("DemoExperience(30)");
            Assert.That(_arena.Stats.Level, Is.GreaterThan(1));
            Execute("DemoSkillLevel(\"Frost\", 3)");
            Assert.That(_arena.SkillLevel(SkillKind.Frost), Is.EqualTo(3));
            _arena.EndRun(true);
            Assert.That(_arena.HasWon && _arena.HasEnded, Is.True);
            Execute("DemoRestart(123)");
            Assert.That(_arena.Stats.Kills, Is.Zero);
            Assert.That(_arena.Stats.Level, Is.EqualTo(1));
            Assert.That(_arena.HasEnded, Is.False);
            _arena.SetHealth(0);
            Assert.That(_arena.HasEnded && !_arena.HasWon, Is.True);
            Execute("DemoRevive()");
            Assert.That(_arena.HasEnded, Is.False);
            Assert.That(_arena.Stats.Health, Is.EqualTo(_arena.Stats.MaxHealth));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllCommandFeaturesAndMacrosAreRegistered()
        {
            _tools.Macros.RequestCommands();
            for (var i = 0; i < 1000 && !_tools.Macros.IsReady; i++) _tools.Macros.Tick();
            Assert.That(_tools.Macros.IsReady, Is.True);
            var spawn = _tools.Macros.Commands.Single(command => command.Command.name == "DemoSpawn");
            Assert.That(spawn.Alias, Is.EqualTo("生成敌人"));
            Assert.That(spawn.ParameterDefines.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(spawn.ParameterDefines[0].Icon, Is.Not.Null);
            Assert.That(_tools.Macros.Commands.Single(command => command.Command.name == "DemoConsoleOnly").IsCheatIgnore, Is.True);
            Assert.That(_tools.Macros.Macros.Any(macro => macro.IsBuiltIn && macro.name == "[快捷] 冲击波"), Is.True);
            var count = _tools.Macros.Macros.Count;
            SurvivorCommands.InstallExampleMacros();
            Assert.That(_tools.Macros.Macros.Count, Is.EqualTo(count));
            foreach (var example in SurvivorCommands.ExampleMacros)
            {
                var macro = new Macro(Macro.Compose(example.name, example.description, example.body));
                Assert.That(_tools.Macros.Validate(macro), Is.Empty, example.name);
            }
            Assert.That((string)Execute("DemoInspect()"), Does.Contain("Health").Or.Contain("生命"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AsyncAndCoroutineMacroCompletesWhilePaused()
        {
            _arena.ClearEnemies(false);
            _arena.SetHealth(50);
            _arena.SetPaused(true);
            var sample = SurvivorCommands.ExampleMacros.Single(example => example.name.Contains("异步"));
            var macro = new Macro(Macro.Compose(sample.name, sample.description, sample.body));
            Assert.That(_tools.Macros.Run(macro), Is.True);
            var deadline = Time.realtimeSinceStartup + 5;
            while (_tools.Macros.RunningId != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_tools.Macros.RunningId, Is.Null);
            Assert.That(_arena.EnemyCount, Is.EqualTo(3));
            Assert.That(_arena.Stats.Health, Is.EqualTo(60));
            Assert.That(_arena.IsPaused, Is.True);
            Assert.That(_arena.Stats.Elapsed, Is.EqualTo(0).Within(0.001));
        }

        [UnityTest]
        public IEnumerator EnemyLimitAndToolsRestartAreBounded()
        {
            _arena.ClearEnemies(false);
            Execute("DemoSpawn(\"Grunt\", 999999)");
            Assert.That(_arena.EnemyCount, Is.EqualTo(SurvivorArena.EnemyLimit));
            Assert.That((int)Execute("DemoSpawn(\"Tank\", 20)"), Is.Zero);
            _tools.Open();
            Assert.That(_tools.Restart(), Is.True);
            Assert.That(_tools.IsOpen, Is.True);
            Assert.That((string)Execute("DemoStatus()"), Does.Contain("Enemies 180"));
            _tools.Shutdown();
            _arena.ToggleTools();
            Assert.That(_tools.IsOpen, Is.True);
            yield return null;
        }
    }
}
