﻿#region Sources

// This class is based on Stephen Cleary's AyncContext, see <a href="https://github.com/StephenCleary/AsyncEx/blob/db32fd5db0d1051e867b36ae039ea13d2c36eb91/test/AsyncEx.Context.UnitTests/AsyncContextUnitTests.cs#L144-L149
// Used to test that BrighterSynchronizationHelper which is derived, passes the same tests as AsyncContext

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class BrighterSynchronizationContextsTests
{
    [Fact]
    public void AsyncContext_StaysOnSameThread()
    {
        var testThread = Thread.CurrentThread.ManagedThreadId;
        var contextThread = BrighterAsyncContext.Run(() => Thread.CurrentThread.ManagedThreadId);
        Assert.Equal(contextThread, testThread);
    }

    [Fact]
    public void Run_AsyncVoid_BlocksUntilCompletion()
    {
        bool resumed = false;
        BrighterAsyncContext.Run((Action)(async () =>
        {
            await Task.Yield();
            resumed = true;
        }));

        Assert.True(resumed);
    }

    [Fact]
    public void Run_AsyncVoid_BlocksUntilCompletion_RunsContinuation()
    {
        bool resumed = false;
        BrighterAsyncContext.Run((Action)(async () =>
        {
            await Task.Delay(50);
            resumed = true;
        }));

        Assert.True(resumed);
    }

    [Fact]
    public void Run_FuncThatCallsAsyncVoid_BlocksUntilCompletion()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run((Func<int>)(() =>
        {
            Action asyncVoid = async () =>
            {
                await Task.Yield();
                resumed = true;
            };
            asyncVoid();
            return 13;
        }));
        Assert.True(resumed);
        Assert.Equal(13, result);
    }

    [Fact]
    public void Run_AsyncTask_BlocksUntilCompletion()
    {
        bool resumed = false;
        BrighterAsyncContext.Run(async () =>
        {
            await Task.Yield();
            resumed = true;
        });
        Assert.True(resumed);
    }

    [Fact]
    public void Run_AsyncTask_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        BrighterAsyncContext.Run(() =>
        {
            Task.Delay(50).GetAwaiter().GetResult();
            resumed = true;
            return Task.CompletedTask;
        });
        Assert.True(resumed);
    }

    [Fact]
    public void Run_AsyncTaskWithResult_BlocksUntilCompletion()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await Task.Yield();
            resumed = true;
            return 17;
        });

        Assert.True(resumed);
        Assert.Equal(17, result);
    }

    [Fact]
    public void Run_AsyncTaskWithResult_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            Task.Delay(50).GetAwaiter().GetResult();
            resumed = true;
            return 17;
        });
        Assert.True(resumed);
        Assert.Equal(17, result);
    }
    
    [Fact]
    public void Run_AsyncTaskWithResultAndConfigurateAwait_BlockingCode_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            resumed = true;
            return 17;
        });
        Assert.True(resumed);
        Assert.Equal(17, result);
    }

    [Fact]
    public void Run_AsyncTaskWithResult_ContainsMultipleAsyncTasks_Still_Ends()
    {
        bool resumed = false;
        var result = BrighterAsyncContext.Run(async () =>
        {
            await MultiTask();
            resumed = true;
            return 17;
        });
        Assert.True(resumed);
        Assert.Equal(17, result);
    }

    static async Task MultiTask()
    {
        await Task.Yield();
        await Task.Yield();
    }

    [Fact]
    public async Task Run_Delegate_Via_Run_Thread_Runs()
    {
        var runner = new EventRunner();
        runner.OnMessagePublished += MessagePublishedHandler;

        s_runnerCalled = false;
        BrighterAsyncContext.Run(async () =>
        {
            await runner.PublishAsync(runner.Report);
        });

        //let callback run on thread pool
        await Task.Delay(1000);

        Assert.True(s_runnerCalled);

        runner.OnMessagePublished -= MessagePublishedHandler;
    }

    private static bool s_runnerCalled = false;
    
    static void MessagePublishedHandler(bool called, int value)
    {
        Assert.Equal(17, value);
        s_runnerCalled = true;
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
    
    [Fact]
    public void Current_WithoutAsyncContext_IsNull()
    {
        Assert.Null(BrighterAsyncContext.Current);
    }

    [Fact]
    public void Current_FromBrighterSynchronizationHelper_IsBrighterSynchronizationHelper()
    {
        BrighterAsyncContext observedHelper = null;
        var helper = new BrighterAsyncContext();

        var task = helper.Factory.StartNew(
            () => { observedHelper = BrighterAsyncContext.Current; },
            helper.Factory.CancellationToken,
            helper.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            helper.TaskScheduler);

        helper.Execute(task);

        Assert.Equal(helper, observedHelper);
    }
    
    [Fact]
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

        var result =await Task.WhenAll(newTask);

        Assert.True(resumed);
        Assert.Equal(17, result[0]);
    }

    [Fact]                 
    public void SynchronizationContextCurrent_FromBrighterSynchronizationHelper_IsBrighterSynchronizationHelperSynchronizationContext()
    {
        System.Threading.SynchronizationContext? observedContext = null;
        var context = new BrighterAsyncContext();

        var task = context.Factory.StartNew(
            () => { observedContext = System.Threading.SynchronizationContext.Current; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        context.Execute(task);

        Assert.Equal(context.SynchronizationContext, observedContext);
    }

    [Fact]
    public void TaskSchedulerCurrent_FromAsyncContext_IsThreadPoolTaskScheduler()
    {
        TaskScheduler observedScheduler = null;
        var context = new BrighterAsyncContext();

        var task = context.Factory.StartNew(
            () => { observedScheduler = TaskScheduler.Current; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        context.Execute(task);

        Assert.Equal(TaskScheduler.Default, observedScheduler);
    }

    [Fact]
    public void TaskScheduler_MaximumConcurrency_IsOne()
    {
        var context = new BrighterAsyncContext();
        Assert.Equal(1, context.TaskScheduler.MaximumConcurrencyLevel);
    }

    [Fact]
    public void Run_PropagatesException()
    {
        bool propogatesException = false;
        try
        {
            BrighterAsyncContext.Run(() => { throw new NotImplementedException(); });
        }
        catch (Exception e)
        {
            propogatesException = true;
        }

        Assert.True(propogatesException);
    }

    [Fact]
    public void Run_Async_PropagatesException()
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

        Assert.True(propogatesException);
    }

    [Fact]
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

        Assert.True(propogatesException);
    }

    [Fact]
    public void SynchronizationContextPost_PropagatesException()
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

        Assert.True(propogatesException);
    }

    [Fact]
    public void Task_AfterExecute_NeverRuns()
    {
        int value = 0;
        var context = new BrighterAsyncContext();

        var task = context.Factory.StartNew(
            () => { value = 1; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        context.Execute(task);

        var taskTwo = context.Factory.StartNew(
            () => { value = 2; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        taskTwo.ContinueWith(_ => { throw new Exception("Should not run"); }, TaskScheduler.Default);

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

        Assert.Equal(1, value);
        Assert.False(exceptionRan);
    }

    [Fact]
    public async Task Task_AfterExecute_Runs_On_ThreadPool()
    {
        int value = 0;
        var context = new BrighterAsyncContext();

        var task = context.Factory.StartNew(
            () => { value = 1; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            context.TaskScheduler);

        context.Execute(task);

        var taskTwo = context.Factory.StartNew(
            () => { value = 2; },
            context.Factory.CancellationToken,
            context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);

        bool threadPoolExceptionRan = false;
        try
        {
            await taskTwo.ContinueWith(_ => throw new Exception("Should run on thread pool"), TaskScheduler.Default);
        }
        catch (Exception e)
        {
            Assert.Equal("Should run on thread pool", e.Message);
            threadPoolExceptionRan = true;
        }

        Assert.True(threadPoolExceptionRan);
    }
    
    [Fact]
    public void SynchronizationContext_IsEqualToCopyOfItself()
    {
        var synchronizationContext1 =
            BrighterAsyncContext.Run(() => System.Threading.SynchronizationContext.Current);
        var synchronizationContext2 = synchronizationContext1.CreateCopy();

        Assert.Equal(synchronizationContext2.GetHashCode(), synchronizationContext1.GetHashCode());
        Assert.True(synchronizationContext1.Equals(synchronizationContext2));
        Assert.False(synchronizationContext1.Equals(new System.Threading.SynchronizationContext()));
    }

    [Fact]
    public void Id_IsEqualToTaskSchedulerId()
    {
        var context = new BrighterAsyncContext();
        Assert.Equal(context.TaskScheduler.Id, context.Id);
    }
}
