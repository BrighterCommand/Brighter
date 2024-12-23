#region Sources

// This class is based on Stephen Cleary's AyncContext, see <a href="https://github.com/StephenCleary/AsyncEx/blob/db32fd5db0d1051e867b36ae039ea13d2c36eb91/test/AsyncEx.Context.UnitTests/AsyncContextUnitTests.cs#L144-L149
// Used to test that BrighterSynchronizationHelper which is derived, passes the same tests as AsyncContext
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.SynchronizationContext;

public class BrighterSynchronizationContextsTests
{
        [Fact]
        public void AsyncContext_StaysOnSameThread()
        {
            var testThread = Thread.CurrentThread.ManagedThreadId;
            var contextThread = BrighterSynchronizationHelper.Run(() => Thread.CurrentThread.ManagedThreadId);
            Assert.Equal(testThread, contextThread);
        }

        [Fact]
        public void Run_AsyncVoid_BlocksUntilCompletion()
        {
            bool resumed = false;
            BrighterSynchronizationHelper.Run((Action)(async () =>
            {
                await Task.Yield();
                resumed = true;
            }));
            Assert.True(resumed);
        }

        [Fact]
        public void Run_FuncThatCallsAsyncVoid_BlocksUntilCompletion()
        {
            bool resumed = false;
            var result = BrighterSynchronizationHelper.Run((Func<int>)(() =>
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
            BrighterSynchronizationHelper.Run(async () =>
            {
                await Task.Yield();
                resumed = true;
            });
            Assert.True(resumed);
        }

        [Fact]
        public void Run_AsyncTaskWithResult_BlocksUntilCompletion()
        {
            bool resumed = false;
            var result = BrighterSynchronizationHelper.Run(async () =>
            {
                await Task.Yield();
                resumed = true;
                return 17;
            });
            Assert.True(resumed);
            Assert.Equal(17, result);
        }

        [Fact]
        public void Current_WithoutAsyncContext_IsNull()
        {
            Assert.Null(BrighterSynchronizationHelper.Current);
        }

        [Fact]
        public void Current_FromAsyncContext_IsAsyncContext()
        {
            BrighterSynchronizationHelper observedContext = null;
            var context = new BrighterSynchronizationHelper();
            
            context.Factory.StartNew(
                () =>   { observedContext = BrighterSynchronizationHelper.Current; },
                context.Factory.CancellationToken, 
                context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
                context.TaskScheduler);

            context.Execute();

            Assert.Same(context, observedContext);
        }

        [Fact]
        public void SynchronizationContextCurrent_FromAsyncContext_IsAsyncContextSynchronizationContext()
        {
            System.Threading.SynchronizationContext observedContext = null;
            var context = new BrighterSynchronizationHelper();
            
            context.Factory.StartNew(
                () =>   { observedContext = System.Threading.SynchronizationContext.Current; },
                context.Factory.CancellationToken, 
                context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
                context.TaskScheduler);

            context.Execute();

            Assert.Same(context.SynchronizationContext, observedContext);
        }

        [Fact]
        public void TaskSchedulerCurrent_FromAsyncContext_IsThreadPoolTaskScheduler()
        {
            TaskScheduler observedScheduler = null;
            var context = new BrighterSynchronizationHelper();
            
            context.Factory.StartNew(
                () =>   { observedScheduler = TaskScheduler.Current; },
                context.Factory.CancellationToken, 
                context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
                context.TaskScheduler);
            
            context.Execute();

            Assert.Same(TaskScheduler.Default, observedScheduler);
        }

        [Fact]
        public void TaskScheduler_MaximumConcurrency_IsOne()
        {
            var context = new BrighterSynchronizationHelper();
            Assert.Equal(1, context.TaskScheduler.MaximumConcurrencyLevel);
        }

        [Fact]
        public void Run_PropagatesException()
        {
            bool propogatesException = false;
            try
            {
                BrighterSynchronizationHelper.Run(() => { throw new NotImplementedException(); });
            }
            catch (Exception e)
            {
                propogatesException = true;
            }
        }

        [Fact]
        public void Run_Async_PropagatesException()
        {
            bool propogatesException = false;
            try
            {
                BrighterSynchronizationHelper.Run(async () =>
                {
                    await Task.Yield();
                    throw new NotImplementedException(); 
                }); 
            }
            catch (Exception e)
            {
                propogatesException = true;
            }
        }

        [Fact]
        public void SynchronizationContextPost_PropagatesException()
        {
            bool propogatesException = false;
            try
            {
                BrighterSynchronizationHelper.Run(async () =>
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
        }

        [Fact]
        public void Task_AfterExecute_NeverRuns()
        {
            int value = 0;
            var context = new BrighterSynchronizationHelper();
            
            context.Factory.StartNew(
                () => { value = 1; },
                context.Factory.CancellationToken, 
                context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
                context.TaskScheduler);
            
            context.Execute();

            var task = context.Factory.StartNew(
                () => { value = 2; },
                context.Factory.CancellationToken, 
                context.Factory.CreationOptions | TaskCreationOptions.DenyChildAttach, 
                context.TaskScheduler);

            task.ContinueWith(_ => { throw new Exception("Should not run"); }, TaskScheduler.Default);
            Assert.Equal(1, value);
        }

        [Fact]
        public void SynchronizationContext_IsEqualToCopyOfItself()
        {
            var synchronizationContext1 = BrighterSynchronizationHelper.Run(() => System.Threading.SynchronizationContext.Current);
            var synchronizationContext2 = synchronizationContext1.CreateCopy();
            Assert.Equal(synchronizationContext1.GetHashCode(), synchronizationContext2.GetHashCode());
            Assert.True(synchronizationContext1.Equals(synchronizationContext2));
            Assert.False(synchronizationContext1.Equals(new System.Threading.SynchronizationContext()));
        }

        [Fact]
        public void Id_IsEqualToTaskSchedulerId()
        {
            var context = new BrighterSynchronizationHelper();
            Assert.Equal(context.TaskScheduler.Id, context.Id);
        }
}
