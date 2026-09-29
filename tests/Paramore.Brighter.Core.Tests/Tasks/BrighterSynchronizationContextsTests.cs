#region Sources
// This class is based on Stephen Cleary's AyncContext, see <a href="https://github.com/StephenCleary/AsyncEx/blob/db32fd5db0d1051e867b36ae039ea13d2c36eb91/test/AsyncEx.Context.UnitTests/AsyncContextUnitTests.cs#L144-L149
// Used to test that BrighterSynchronizationHelper which is derived, passes the same tests as AsyncContext
#endregion
using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.Core.Tests.Tasks;
public class BrighterSynchronizationContextsTests
{
    [Test]
    public async Task AsyncContext_StaysOnSameThread()
    {
        var testThread = Thread.CurrentThread.ManagedThreadId;
        var contextThread = BrighterAsyncContext.Run(() => Thread.CurrentThread.ManagedThreadId);
        await Assert.That(testThread).IsEqualTo(contextThread);
    }

    [Test]
    public async Task Run_AsyncVoid_BlocksUntilCompletion()
    {
        bool resumed = false;
#pragma warning disable TUnit0031 // Intentionally testing async void behavior in BrighterAsyncContext
        BrighterAsyncContext.Run((Action)(async () =>
        {
            await Task.Yield();
            resumed = true;
        }));
#pragma warning restore TUnit0031
        await Assert.That(resumed).IsTrue();
    }

    [Test]
    public async Task Run_AsyncVoid_BlocksUntilCompletion_RunsContinuation()
    {
        bool resumed = false;
#pragma warning disable TUnit0031 // Intentionally testing async void behavior in BrighterAsyncContext
        BrighterAsyncContext.Run((Action)(async () =>
        {
            await Task.Delay(50);
            resumed = true;
        }));
#pragma warning restore TUnit0031
        await Assert.That(resumed).IsTrue();
    }

    [Test]
    public async Task Run_FuncThatCallsAsyncVoid_BlocksUntilCompletion()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run((Func<int>)(() =>
        {
#pragma warning disable TUnit0031 // Intentionally testing async void behavior in BrighterAsyncContext
            Action asyncVoid = async () =>
            {
                await Task.Yield();
                resumed = true;
            };
#pragma warning restore TUnit0031
            asyncVoid();
            return 13;
        }));
        await Assert.That(resumed).IsTrue();
        await Assert.That(result).IsEqualTo(13);
    }

    [Test]
    public async Task Run_AsyncTask_BlocksUntilCompletion()
    {
        bool resumed = false;
        BrighterAsyncContext.Run(async () =>
        {
            await Task.Yield();
            resumed = true;
        });
        await Assert.That(resumed).IsTrue();
    }

    [Test]
    public async Task Run_AsyncTask_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        BrighterAsyncContext.Run(async () =>
        {
            await Task.Delay(50);
            resumed = true;
        });
        await Assert.That(resumed).IsTrue();
    }

    [Test]
    public async Task Run_AsyncTaskWithResult_BlocksUntilCompletion()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await Task.Yield();
            resumed = true;
            return 17;
        });
        await Assert.That(resumed).IsTrue();
        await Assert.That(result).IsEqualTo(17);
    }

    [Test]
    public async Task Run_AsyncTaskWithResult_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await Task.Delay(50);
            resumed = true;
            return 17;
        });
        await Assert.That(resumed).IsTrue();
        await Assert.That(result).IsEqualTo(17);
    }

    [Test]
    public async Task Run_AsyncTaskWithResultAndConfigurateAwait_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            resumed = true;
            return 17;
        });
        await Assert.That(resumed).IsTrue();
        await Assert.That(result).IsEqualTo(17);
    }

    [Test]
    public async Task Run_AsyncTaskWithResult_ContainsMultipleAsyncTasks_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await MultiTask();
            resumed = true;
            return 17;
        });
        await Assert.That(resumed).IsTrue();
        await Assert.That(result).IsEqualTo(17);
    }

    static async Task MultiTask()
    {
        await Task.Yield();
        await Task.Yield();
    }

    [Test]
    public async Task Run_Delegate_Via_Run_Thread_Runs()
    {
        var runner = new EventRunner();
        var handlerInvoked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(bool called, int value) => handlerInvoked.TrySetResult(value);
        runner.OnMessagePublished += Handler;

        try
        {
            BrighterAsyncContext.Run(async () =>
            {
                await runner.PublishAsync(runner.Report);
            });

            var observedValue = await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(observedValue).IsEqualTo(17);
        }
        finally
        {
            runner.OnMessagePublished -= Handler;
        }
    }

    internal sealed class EventRunner
    {
        public event Action<bool, int> OnMessagePublished;
        public async Task PublishAsync(Action<int> callBack)
        {
            await Task.Yield();
            callBack(17);
        }

        public void Report(int value)
        {
            Task.Run(() => OnMessagePublished?.Invoke(true, value));
        }
    }

    [Test]
    public async Task Current_WithoutAsyncContext_IsNull()
    {
        await Assert.That(BrighterAsyncContext.Current).IsNull();
    }

    [Test]
    public async Task Current_FromBrighterSynchronizationHelper_IsBrighterSynchronizationHelper()
    {
        BrighterAsyncContext observedHelper = null;
        var helper = new BrighterAsyncContext();
        var task = helper.Factory.StartNew(() =>
        {
            observedHelper = BrighterAsyncContext.Current;
        }, helper.Factory.CancellationToken, helper.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, helper.TaskScheduler);
        helper.Execute(task);
        await Assert.That(observedHelper).IsEqualTo(helper);
    }

    [Test]
    public async Task Run_AsyncTaskWithResult_ContainsMultipleAsyncTasks_Still_Ends2()
    {
        bool resumed = false;
        var newTask = Task.Factory.StartNew(() => BrighterAsyncContext.Run(async () =>
        {
            await Task.Delay(100);
            resumed = true;
            return 17;
        }));
        await Task.Delay(100);
        var result = await Task.WhenAll(newTask);
        await Assert.That(resumed).IsTrue();
        await Assert.That(result[0]).IsEqualTo(17);
    }

    [Test]
    public async Task SynchronizationContextCurrent_FromBrighterSynchronizationHelper_IsBrighterSynchronizationHelperSynchronizationContext()
    {
        System.Threading.SynchronizationContext? observedContext = null;
        var context = new BrighterAsyncContext();
        var task = context.Factory.StartNew(() =>
        {
            observedContext = System.Threading.SynchronizationContext.Current;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, context.TaskScheduler);
        context.Execute(task);
        await Assert.That(observedContext).IsEqualTo(context.SynchronizationContext);
    }

    [Test]
    public async Task TaskSchedulerCurrent_FromAsyncContext_IsThreadPoolTaskScheduler()
    {
        TaskScheduler observedScheduler = null;
        var context = new BrighterAsyncContext();
        var task = context.Factory.StartNew(() =>
        {
            observedScheduler = TaskScheduler.Current;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, context.TaskScheduler);
        context.Execute(task);
        await Assert.That(observedScheduler).IsEqualTo(TaskScheduler.Default);
    }

    [Test]
    public async Task TaskScheduler_MaximumConcurrency_IsOne()
    {
        var context = new BrighterAsyncContext();
        await Assert.That(context.TaskScheduler.MaximumConcurrencyLevel).IsEqualTo(1);
    }

    [Test]
    public async Task Run_PropagatesException()
    {
        bool propogatesException = false;
        try
        {
            BrighterAsyncContext.Run(() =>
            {
                throw new NotImplementedException();
            });
        }
        catch (Exception e)
        {
            propogatesException = true;
        }

        await Assert.That(propogatesException).IsTrue();
    }

    [Test]
    public async Task Run_Async_PropagatesException()
    {
        bool propogatesException = false;
        try
        {
            BrighterAsyncContext.Run(async () =>
            {
                await Task.Yield();
                throw new NotImplementedException();
            });
        }
        catch (Exception e)
        {
            propogatesException = true;
        }

        await Assert.That(propogatesException).IsTrue();
    }

    [Test]
    public async Task Run_Async_InThread_PropagatesException()
    {
        bool propogatesException = false;
        try
        {
            var runningThread = new TaskFactory().StartNew(() =>
            {
                BrighterAsyncContext.Run(async () =>
                {
                    await Task.Yield();
                    throw new NotImplementedException();
                });
            });
            await runningThread;
        }
        catch (Exception e)
        {
            propogatesException = true;
        }

        await Assert.That(propogatesException).IsTrue();
    }

    [Test]
    public async Task SynchronizationContextPost_PropagatesException()
    {
        bool propogatesException = false;
        try
        {
            BrighterAsyncContext.Run(async () =>
            {
                System.Threading.SynchronizationContext.Current.Post(_ =>
                {
                    throw new NotImplementedException();
                }, null);
                await Task.Yield();
            });
        }
        catch (Exception e)
        {
            propogatesException = true;
        }

        await Assert.That(propogatesException).IsTrue();
    }

    [Test]
    public async Task Task_AfterExecute_NeverRuns()
    {
        int value = 0;
        var context = new BrighterAsyncContext();
        var task = context.Factory.StartNew(() =>
        {
            value = 1;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, context.TaskScheduler);
        context.Execute(task);
        var taskTwo = context.Factory.StartNew(() =>
        {
            value = 2;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, context.TaskScheduler);
        taskTwo.ContinueWith(_ =>
        {
            throw new Exception("Should not run");
        }, TaskScheduler.Default);
        bool exceptionRan = false;
        try
        {
            context.Execute(taskTwo);
        }
        catch (Exception e)
        {
            exceptionRan = true;
        }

        //there should be no pending work
        await Assert.That(value).IsEqualTo(1);
        await Assert.That(exceptionRan).IsFalse();
    }

    [Test]
    public async Task Task_AfterExecute_Runs_On_ThreadPool()
    {
        int value = 0;
        var context = new BrighterAsyncContext();
        var task = context.Factory.StartNew(() =>
        {
            value = 1;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, context.TaskScheduler);
        context.Execute(task);
        var taskTwo = context.Factory.StartNew(() =>
        {
            value = 2;
        }, context.Factory.CancellationToken, context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        bool threadPoolExceptionRan = false;
        try
        {
            await taskTwo.ContinueWith(_ => throw new Exception("Should run on thread pool"), TaskScheduler.Default);
        }
        catch (Exception e)
        {
            await Assert.That(e.Message).IsEqualTo("Should run on thread pool");
            threadPoolExceptionRan = true;
        }

        await Assert.That(threadPoolExceptionRan).IsTrue();
    }

    [Test]
    public async Task SynchronizationContext_IsEqualToCopyOfItself()
    {
        var synchronizationContext1 = BrighterAsyncContext.Run(() => System.Threading.SynchronizationContext.Current);
        var synchronizationContext2 = synchronizationContext1.CreateCopy();
        await Assert.That(synchronizationContext1.GetHashCode()).IsEqualTo(synchronizationContext2.GetHashCode());
        await Assert.That(synchronizationContext1.Equals(synchronizationContext2)).IsTrue();
        await Assert.That(synchronizationContext1.Equals(new System.Threading.SynchronizationContext())).IsFalse();
    }

    [Test]
    public async Task Id_IsEqualToTaskSchedulerId()
    {
        var context = new BrighterAsyncContext();
        await Assert.That(context.Id).IsEqualTo(context.TaskScheduler.Id);
    }

    [Test]
    public async Task Post_AfterContextDisposed_DoesNotThrow()
    {
        SynchronizationContext? captured = null;
        BrighterAsyncContext.Run(() =>
        {
            captured = SynchronizationContext.Current;
        });

        await Assert.That(captured).IsNotNull();
        var exception = Catch.Exception(() => captured!.Post(_ => { }, null));
        await Assert.That(exception).IsNull();
    }

    [Test]
    public async Task Send_AfterContextDisposed_Throws()
    {
        SynchronizationContext? captured = null;
        BrighterAsyncContext.Run(() =>
        {
            captured = SynchronizationContext.Current;
        });

        await Assert.That(captured).IsNotNull();

        var thrown = Task.Run(() =>
        {
            try
            {
                captured!.Send(_ => { }, null);
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        await Assert.That(thrown.Wait(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(thrown.Result).IsTypeOf<ObjectDisposedException>();
    }

    [Test]
    public async Task Send_AfterShutdown_StressIterations_NeverHangsAndOnlyThrowsObjectDisposed()
    {
        const int iterations = 1000;
        for (var i = 0; i < iterations; i++)
        {
            SynchronizationContext? captured = null;
            BrighterAsyncContext.Run(() =>
            {
                captured = SynchronizationContext.Current;
            });

            await Assert.That(captured).IsNotNull();

            var sent = Task.Run(() =>
            {
                try
                {
                    captured!.Send(_ => { }, null);
                    return (Exception?)null;
                }
                catch (Exception e)
                {
                    return e;
                }
            });

            await Assert.That(sent.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            if (sent.Result is not null)
                await Assert.That(sent.Result).IsTypeOf<ObjectDisposedException>();
        }
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        var context = new BrighterAsyncContext();
        context.Dispose();
        var exception = Catch.Exception(() => context.Dispose());
        await Assert.That(exception).IsNull();
    }

    [Test]
    public async Task Run_NestedInsideOuterRun_DoesNotDeadlock()
    {
        var innerThread = 0;
        var outerThread = BrighterAsyncContext.Run(() =>
        {
            innerThread = BrighterAsyncContext.Run(() => Thread.CurrentThread.ManagedThreadId);
            return Thread.CurrentThread.ManagedThreadId;
        });

        await Assert.That(innerThread).IsNotEqualTo(0);
        await Assert.That(outerThread).IsNotEqualTo(0);
    }

    [Test]
    public async Task Execute_CalledConcurrently_ThrowsInvalidOperationException()
    {
        var context = new BrighterAsyncContext();

        var firstStarted = new ManualResetEventSlim(false);
        var releaseFirst = new ManualResetEventSlim(false);

        var blockingTask = context.Factory.StartNew(
            () =>
            {
                firstStarted.Set();
                releaseFirst.Wait();
            },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        var firstExecute = Task.Run(() => context.Execute(blockingTask));

        try
        {
            await Assert.That(firstStarted.Wait(TimeSpan.FromSeconds(5))).IsTrue();

            var ex = Catch.Exception(() => context.Execute(blockingTask));
            await Assert.That(ex).IsTypeOf<InvalidOperationException>();
            await Assert.That(ex!.Message).Contains("not re-entrant");
        }
        finally
        {
            releaseFirst.Set();
            firstExecute.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
