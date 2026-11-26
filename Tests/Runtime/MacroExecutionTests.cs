using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameConsole.Extension;
using RedSaw.CommandLineInterface;

namespace GameDevTools.Tests
{
    public sealed class MacroExecutionTests
    {
        private static TaskCompletionSource<int> _completion;

        private static int _marks;

        private static bool _routineCompleted;

        private static bool _routineDisposed;

        private readonly List<string> _output = new();

        private GameObject _owner;

        private MacroSystem _system;

        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _completion = new TaskCompletionSource<int>();
            _marks = 0;
            _routineCompleted = false;
            _routineDisposed = false;
            _output.Clear();
            _owner = new GameObject("Macro tests");
            _system = new MacroSystem(new CommandSystem(), _owner.AddComponent<MacroTestOwner>(), _output.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            _completion.TrySetResult(0);
            UnityEngine.Object.DestroyImmediate(_owner);
            Time.timeScale = _timeScale;
        }

        [UnityTest]
        public IEnumerator WaitForTaskWaitsForCompletion()
        {
            Run("WaitFor(MacroTestTask())\nMacroTestMark()");
            yield return null;
            Assert.That(_marks, Is.Zero);
            _completion.SetResult(7);
            yield return WaitForIdle();
            Assert.That(_marks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WaitForGenericTaskWaitsForCompletion()
        {
            Run("WaitFor(MacroTestGenericTask())\nMacroTestMark()");
            yield return null;
            Assert.That(_marks, Is.Zero);
            _completion.SetResult(7);
            yield return WaitForIdle();
            Assert.That(_marks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OrdinaryTaskCallContinuesAndReportsLateFailureOnce()
        {
            Run("MacroTestTask()\nMacroTestMark()");
            Assert.That(_marks, Is.EqualTo(1));
            _completion.SetException(new InvalidOperationException("late task failure"));
            yield return null;
            yield return null;
            Assert.That(_output.Count(message => message.Contains("late task failure")), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WaitedTaskFailureStopsFollowingCommands()
        {
            Run("WaitFor(MacroTestTask())\nMacroTestMark()");
            _completion.SetException(new InvalidOperationException("waited task failure"));
            yield return WaitForIdle();
            Assert.That(_marks, Is.Zero);
            Assert.That(_output.Any(message => message.Contains("waited task failure")), Is.True);
        }

        [UnityTest]
        public IEnumerator StopCancelsWaitingWithoutCancelingBusinessTask()
        {
            Run("WaitFor(MacroTestTask())\nMacroTestMark()");
            _system.Stop();
            yield return WaitForIdle();
            Assert.That(_completion.Task.IsCompleted, Is.False);
            _completion.SetResult(7);
            yield return null;
            Assert.That(_marks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WaitForFrameAdvancesAtZeroTimeScale()
        {
            Time.timeScale = 0;
            var frame = Time.frameCount;

            Run("WaitFor()\nMacroTestMark()");
            Assert.That(_marks, Is.Zero);
            yield return WaitForIdle();
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(_marks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DelayUsesRealtimeAtZeroTimeScale()
        {
            Time.timeScale = 0;
            var started = Time.realtimeSinceStartup;

            Run("WaitFor(0.05)\nMacroTestMark()");
            yield return WaitForIdle();
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(_marks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DelayCanBeStoppedImmediately()
        {
            Run("WaitFor(30)\nMacroTestMark()");
            _system.Stop();
            yield return WaitForIdle();
            Assert.That(_marks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WaitForNestedCoroutineCompletesInOrder()
        {
            Run("WaitFor(MacroTestRoutine())\nMacroTestMark()");
            Assert.That(_marks, Is.Zero);
            yield return WaitForIdle();
            Assert.That(_routineCompleted, Is.True);
            Assert.That(_marks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CoroutineFailureDisposesRoutineAndStopsMacro()
        {
            Run("WaitFor(MacroTestFailedRoutine())\nMacroTestMark()");
            yield return WaitForIdle();
            Assert.That(_routineDisposed, Is.True);
            Assert.That(_marks, Is.Zero);
            Assert.That(_output.Any(message => message.Contains("routine failure")), Is.True);
        }

        [UnityTest]
        public IEnumerator DisposeStopsOwnedCoroutine()
        {
            Run("WaitFor(MacroTestFailedRoutine())\nMacroTestMark()");
            _system.Dispose();
            yield return null;
            yield return null;
            Assert.That(_routineDisposed, Is.True);
            Assert.That(_marks, Is.Zero);
            Assert.That(_output.Any(message => message.Contains("routine failure")), Is.False);
        }

        [Test]
        public void SynchronousCommandsAndRepeatsStillRun()
        {
            Run("Repeat(3)\nWaitFor(MacroTestMark())\nEndRepeat()");
            Assert.That(_marks, Is.EqualTo(3));
            Assert.That(_system.RunningId, Is.Null);
        }

        private void Run(string body)
        {
            var macro = new Macro(Macro.Compose("test", string.Empty, body));

            Assert.That(_system.Run(macro), Is.True, _system.Status);
        }

        private IEnumerator WaitForIdle()
        {
            var deadline = Time.realtimeSinceStartup + 3;

            while (_system.RunningId != null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_system.RunningId, Is.Null, _system.Status);
        }

        [Command("MacroTestTask")]
        private static Task TaskCommand() => _completion.Task;

        [Command("MacroTestGenericTask")]
        private static Task<int> GenericTaskCommand() => _completion.Task;

        [Command("MacroTestMark")]
        private static void MarkCommand() => _marks++;

        [Command("MacroTestRoutine")]
        private static IEnumerator RoutineCommand()
        {
            yield return null;
            yield return NestedRoutine();
        }

        private static IEnumerator NestedRoutine()
        {
            yield return null;
            _routineCompleted = true;
        }

        [Command("MacroTestFailedRoutine")]
        private static IEnumerator FailedRoutineCommand()
        {
            try
            {
                yield return null;
                throw new InvalidOperationException("routine failure");
            }
            finally
            {
                _routineDisposed = true;
            }
        }
    }

    public sealed class MacroTestOwner : MonoBehaviour
    {
    }
}
