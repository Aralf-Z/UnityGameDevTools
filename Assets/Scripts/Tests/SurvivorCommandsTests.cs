using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameConsole.Extension;
using NUnit.Framework;
using RedSaw.CommandLineInterface;
using UnityEngine;

namespace Sanmon.SurvivorDemo.Tests
{
    public sealed class SurvivorCommandsTests
    {
        private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private SurvivorArena _arena;
        private SurvivorConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = SurvivorConfiguration.Load();
            _root = new GameObject("Survivor commands test");
            _arena = _root.AddComponent<SurvivorArena>();
            SetField("_skills", _configuration.skills);
            SetField("_levels", _configuration.levels);
            SetField("_enemyCatalog", _configuration.enemies);
            SetField("_random", new System.Random(42));
            SetField("_surfacePrototype", Resources.Load<Material>("SurvivorDemo/Surface"));
            // 预建渲染对象以测试改造的即时刷新, 不创建完整场景或依赖帧更新.
            var count = Enumerable.Range(0, SurvivorSkills.ALL_MODIFICATIONS + 1).Max(mask => _configuration.skills.Resolve(SkillKind.Orbit, mask).orbiterCount);
            var orbiters = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                orbiters[i] = new GameObject("Orbiter", typeof(MeshRenderer)).transform;
                orbiters[i].SetParent(_root.transform);
            }
            SetField("_orbiters", orbiters);
            typeof(SurvivorArena).GetProperty(nameof(SurvivorArena.Instance)).SetValue(null, _arena);
            typeof(SurvivorArena).GetMethod("ResetProgression", PRIVATE_INSTANCE).Invoke(_arena, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var material in (List<Material>)GetField("_materials")) UnityEngine.Object.DestroyImmediate(material);
            typeof(SurvivorArena).GetProperty(nameof(SurvivorArena.Instance)).SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void CommandsReplaceLegacyExamplesAndAllPresetsAndMacrosCompile()
        {
            var commands = new CommandSystem();
            Assert.That(commands.vm.AllCallables.Any(item => item.Name == "SurvivorGiveSkill"), Is.True);
            Assert.That(commands.vm.AllCallables.Any(item => item.Name.StartsWith("Demo", StringComparison.Ordinal)), Is.False);
            Assert.That(commands.vm.AllProperties.Any(item => item.Name.StartsWith("Demo", StringComparison.Ordinal)), Is.False);
            using var macros = new MacroSystem(commands, _arena, _ => { });
            foreach (var method in typeof(SurvivorCommands).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                var command = method.GetCustomAttribute<CommandAttribute>();
                if (command == null) continue;
                foreach (var preset in ParamDefine.GetInfos(method) ?? new List<ParamDefine>())
                    Assert.That(macros.Validate(new Macro(Macro.Compose("preset", "", command.name + "(" + preset.param + ")"))), Is.Empty, command.name + ": " + preset.param);
                foreach (var macro in method.GetCustomAttributes<MacroAttribute>())
                    Assert.That(macros.Validate(new Macro(Macro.Compose(macro.name, macro.desc, command.name + "(" + macro.parameters + ")"))), Is.Empty, macro.name);
            }
        }

        [Test]
        public void AllCombatSkillsCanBeGrantedBeyondLevelRewardCount()
        {
            Assert.That(_arena.GiveAllSkills(), Is.EqualTo(SurvivorSkills.COMBAT_SKILL_COUNT));
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++) Assert.That(_arena.HasSkill((SkillKind)i), Is.True);
            Assert.That(_arena.GiveAllSkills(), Is.Zero);
            Assert.That(_arena.GiveAllModifications(), Is.EqualTo(45));
            Assert.That(_arena.GiveAllModifications(), Is.Zero);
            for (var i = 0; i < SurvivorSkills.SKILL_COUNT; i++) Assert.That(_arena.ModificationCount((SkillKind)i), Is.EqualTo(5));
        }

        [Test]
        public void GrantingSkillPreservesModificationsCooldownsAndAvailableRewards()
        {
            var pending = _arena.PendingSkills;
            Assert.That(_arena.GiveModification(SkillKind.Bolt, 4), Is.True);
            var deadlines = (float[])GetField("_readyAt");
            deadlines[(int)SkillKind.Bolt] = 10;
            Assert.That(_arena.GiveSkill(SkillKind.Bolt), Is.False);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.EqualTo(1 << 4));
            Assert.That(_arena.SkillCooldown(SkillKind.Bolt), Is.EqualTo(10));
            Assert.That(_arena.PendingSkills, Is.EqualTo(pending));
            Assert.That(_arena.RewardChoices.All(choice => choice.skill != SkillKind.Bolt), Is.True);
        }

        [Test]
        public void IndependentModificationUsesIdNameOrIndexAndCanBeRemoved()
        {
            var definition = _configuration.skills.Get(SkillKind.Bolt);
            SurvivorCommands.GiveModification("bolt", definition.modifications[4].id);
            SurvivorCommands.GiveModification(definition.displayName, definition.modifications[0].displayName);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.EqualTo((1 << 4) | 1));
            Assert.That(_arena.GiveModification(SkillKind.Bolt, 4), Is.False);
            Assert.That(SurvivorCommands.RemoveModification("Bolt", "1"), Is.True);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.EqualTo(1 << 4));
            Assert.That(_arena.RemoveSkill(SkillKind.Bolt), Is.True);
            Assert.That(_arena.HasSkill(SkillKind.Bolt), Is.False);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.Zero);
            Assert.Throws<ArgumentException>(() => _arena.RemoveSkill(SkillKind.Dash));
        }

        [Test]
        public void DisabledAndInvalidGrantsAreRejectedWithoutChangingProgression()
        {
            _configuration.skills.Get(SkillKind.Bolt).enabled = false;
            _configuration.skills.Get(SkillKind.Nova).modifications[0].enabled = false;
            _configuration.skills.Rebuild();
            Assert.Throws<ArgumentException>(() => _arena.GiveSkill(SkillKind.Bolt));
            Assert.Throws<ArgumentException>(() => _arena.GiveModification(SkillKind.Nova, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _arena.GiveSkill((SkillKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _arena.GiveModification(SkillKind.Nova, -1));
            Assert.Throws<ArgumentException>(() => SurvivorCommands.GiveModification("Nova", "missing"));
            Assert.That(_arena.HasSkill(SkillKind.Nova), Is.False);
            Assert.That(_arena.GiveAllSkills(), Is.EqualTo(7));
            _arena.GiveAllModifications();
            Assert.That(_arena.HasSkill(SkillKind.Bolt), Is.False);
            Assert.That(_arena.ModificationMask(SkillKind.Nova) & 1, Is.Zero);
        }

        [Test]
        public void AttributeEditsClampHealthAndRejectNonFiniteValues()
        {
            Assert.That(SurvivorCommands.SetAttribute("生命上限", 1000), Is.EqualTo(1000));
            SurvivorCommands.Health = 900;
            SurvivorCommands.MaxHealth = 50;
            Assert.That(_arena.Stats.Health, Is.EqualTo(50));
            Assert.That(SurvivorCommands.SetAttribute("基础伤害", 123), Is.EqualTo(123));
            SurvivorCommands.HealthRegeneration = 2.5f;
            Assert.That(_arena.Stats.HealthRegenerationPerSecond, Is.EqualTo(2.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorCommands.Damage = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorCommands.MoveSpeed = float.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => SurvivorCommands.SetAttribute("missing", 1));
        }

        [Test]
        public void DirectLevelChangeClearsChoicesAndSynchronizesGrowthAndExperience()
        {
            _arena.GiveModification(SkillKind.Bolt, 2);
            _arena.SetPlayerLevel(20);
            Assert.That(_arena.Stats.Level, Is.EqualTo(20));
            Assert.That(_arena.Stats.Experience, Is.EqualTo(_configuration.levels.Get(20).totalExperience));
            Assert.That(_arena.Stats.ExperienceRequired, Is.EqualTo(_configuration.levels.Get(21).totalExperience));
            Assert.That(_arena.Stats.MaxHealth, Is.EqualTo(_configuration.levels.MaxHealthAtLevel(20)));
            Assert.That(_arena.Stats.BaseDamage, Is.EqualTo(_configuration.levels.BaseDamageAtLevel(20)));
            Assert.That(_arena.PendingSkills + _arena.PendingModifications, Is.Zero);
            Assert.That(_arena.IsChoosingReward, Is.False);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.EqualTo(1 << 2));
            _arena.SetPlayerLevel(1);
            Assert.That(_arena.Stats.Health, Is.LessThanOrEqualTo(_arena.Stats.MaxHealth));
            Assert.That(_arena.Stats.Experience, Is.Zero);
        }

        [Test]
        public void ConsoleCallsAndPropertyAssignmentsModifyCurrentRun()
        {
            var commands = new CommandSystem();
            Assert.That(commands.Execute("@SurvivorDamage = 321", out _), Is.Null);
            Assert.That(_arena.Stats.BaseDamage, Is.EqualTo(321));
            Assert.That(commands.Execute("SurvivorGiveModification(\"Bolt\", \"5\")", out _), Is.Null);
            Assert.That(_arena.ModificationMask(SkillKind.Bolt), Is.EqualTo(1 << 4));
            Assert.That(commands.Execute("SurvivorGiveAllSkills()", out var added), Is.Null);
            Assert.That(added, Is.EqualTo(7));
            Assert.That(commands.Execute("SurvivorClearRewards()", out _), Is.Null);
            Assert.That(_arena.IsChoosingReward, Is.False);
        }

        [Test]
        public void OrbitModificationsRefreshVisibleEffectsImmediately()
        {
            _arena.GiveSkill(SkillKind.Orbit);
            _arena.GiveAllModifications(SkillKind.Orbit);
            var orbiters = (Transform[])GetField("_orbiters");
            var expected = _configuration.skills.Resolve(SkillKind.Orbit, SurvivorSkills.ALL_MODIFICATIONS);
            Assert.That(orbiters.Count(item => item.gameObject.activeSelf), Is.EqualTo(expected.orbiterCount));
            Assert.That(orbiters[0].GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(expected.color));
            _arena.RemoveSkill(SkillKind.Orbit);
            Assert.That(orbiters.All(item => !item.gameObject.activeSelf), Is.True);
        }

        [Test]
        public void ClearingRewardsResumesSimulationAndPreservesExplicitPause()
        {
            var originalTimeScale = Time.timeScale;
            try
            {
                SetField("_initialized", true);
                Time.timeScale = 0;
                Assert.That(_arena.ClearPendingRewards(), Is.EqualTo(1));
                Assert.That(_arena.PendingSkills + _arena.PendingModifications, Is.Zero);
                Assert.That(_arena.IsChoosingReward, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                _arena.SetPaused(true);
                _arena.ClearPendingRewards();
                Assert.That(Time.timeScale, Is.Zero);
            }
            finally
            {
                SetField("_initialized", false);
                Time.timeScale = originalTimeScale;
            }
        }

        [Test]
        public void ForcedCastDoesNotBypassRewardSelectionPauseOrEndedRun()
        {
            _arena.GiveSkill(SkillKind.Bolt);
            Assert.That(SurvivorCommands.Cast("Bolt"), Is.False);
            _arena.ClearPendingRewards();
            _arena.SetPaused(true);
            Assert.That(SurvivorCommands.Cast("Bolt"), Is.False);
            _arena.SetPaused(false);
            typeof(SurvivorArena).GetProperty(nameof(SurvivorArena.HasEnded)).SetValue(_arena, true);
            Assert.That(SurvivorCommands.Cast("Bolt"), Is.False);
        }

        [Test]
        public void CooldownResetIncludesDashAndCanTargetOneSkill()
        {
            var deadlines = (float[])GetField("_readyAt");
            Array.Fill(deadlines, 10);
            SetField("_dashReadyAt", 20f);
            _arena.ResetSkillCooldowns(SkillKind.Bolt);
            Assert.That(_arena.SkillCooldown(SkillKind.Bolt), Is.Zero);
            Assert.That(_arena.SkillCooldown(SkillKind.Nova), Is.EqualTo(10));
            _arena.ResetSkillCooldowns();
            Assert.That(deadlines, Is.All.Zero);
            Assert.That((float)GetField("_dashReadyAt"), Is.Zero);
        }

        private object GetField(string name) => typeof(SurvivorArena).GetField(name, PRIVATE_INSTANCE).GetValue(_arena);
        private void SetField(string name, object value) => typeof(SurvivorArena).GetField(name, PRIVATE_INSTANCE).SetValue(_arena, value);
    }
}
