using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GameConsole.Extension
{
    /// <summary> 将命令异步返回值观察一次, 宏停止只取消等待而不终止业务操作. </summary>
    internal static class MacroAwaiter
    {
        /// <summary> 返回可重复观察的 Task, 未等待的调用也会报告异步异常. </summary>
        internal static Task ObserveAsync(object value, MonoBehaviour owner, CancellationToken lifetime, Action<string> output)
        {
            var task = CompleteAsync(value, owner, lifetime);

            _ = ReportAsync(task, output);
            return task;
        }

        // 仅取消等待, 后续业务 Task 的异常仍会被观察.
        internal static async Task WaitAsync(Task task, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!task.IsCompleted && cancellation.CanBeCanceled)
            {
                var stopped = new TaskCompletionSource<bool>();

                using (cancellation.Register(() => stopped.TrySetResult(true)))
                {
                    if (await Task.WhenAny(task, stopped.Task) != task)
                    {
                        _ = ReportAsync(task, _ => { });
                        cancellation.ThrowIfCancellationRequested();
                    }
                }
            }

            await task;
        }

        internal static Task NextFrameAsync(MonoBehaviour owner, CancellationToken cancellation) => CompleteAsync(WaitForFrame(), owner, cancellation);

        internal static Task DelayAsync(float seconds, MonoBehaviour owner, CancellationToken cancellation) => CompleteAsync(WaitForSeconds(seconds), owner, cancellation);

        private static IEnumerator WaitForFrame()
        {
            yield return null;
        }

        private static IEnumerator WaitForSeconds(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static async Task CompleteAsync(object value, MonoBehaviour owner, CancellationToken lifetime)
        {
            lifetime.ThrowIfCancellationRequested();
            if (value == null) return;

            if (value is Task task)
            {
                await WaitAsync(task, lifetime);
                return;
            }

            if (value is IEnumerator routine)
            {
                var completion = new TaskCompletionSource<bool>();
                var guarded = GuardRoutine(routine, completion);
                var coroutine = owner.StartCoroutine(guarded);

                try
                {
                    await WaitAsync(completion.Task, lifetime);
                }
                finally
                {
                    if (lifetime.IsCancellationRequested)
                    {
                        if (owner != null && coroutine != null)
                        {
                            owner.StopCoroutine(coroutine);
                        }

                        (guarded as IDisposable)?.Dispose();
                    }
                }

                return;
            }
        }

        // 观察异常不改变原 Task 的完成状态, WaitFor 仍会收到同一异常.
        private static async Task ReportAsync(Task task, Action<string> output)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                output(exception.GetBaseException().Message);
            }
        }

        // 展开嵌套迭代器并捕获 MoveNext/Current 异常, 其他 yield 对象交给 Unity.
        private static IEnumerator GuardRoutine(IEnumerator root, TaskCompletionSource<bool> completion)
        {
            var stack = new Stack<IEnumerator>();

            stack.Push(root);
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    var moved = false;
                    Exception failure = null;

                    try
                    {
                        moved = stack.Peek()
                            .MoveNext();
                        if (moved)
                        {
                            current = stack.Peek().Current;
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }

                    if (failure != null)
                    {
                        completion.TrySetException(failure);
                        yield break;
                    }

                    if (!moved)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }

                    if (current is IEnumerator child)
                    {
                        stack.Push(child);
                    }
                    else
                    {
                        yield return current;
                    }
                }

                completion.TrySetResult(true);
            }
            finally
            {
                while (stack.Count > 0)
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                }

                completion.TrySetCanceled();
            }
        }
    }
}
