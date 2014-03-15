
using System;
using System.Threading;
using System.Threading.Tasks;

//TImeoutAfter method after Joe Hoag http://blogs.msdn.com/b/pfxteam/archive/2011/11/10/10235834.aspx 

namespace paramore.commandprocessor.timeoutpolicy.Handlers
{
    public class TimeoutPolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public const string CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN = "TimeoutCancellationToken"; 
        private int milliseconds;

        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            milliseconds = (int) initializerList[0];
        }

        public override TRequest Handle(TRequest command)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var task = TimeoutAfter(
                    task: Task<TRequest>.Factory.StartNew(
                        function: () =>
                            {
                                //we already cancelled the task
                                ct.ThrowIfCancellationRequested();
                                //allow the handlers that can timeout to grab the cancellation token
                                Context.Bag[CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN] = ct;
                                return base.Handle(command);
                            },
                        cancellationToken: ct,
                        creationOptions: TaskCreationOptions.PreferFairness,
                        scheduler: TaskScheduler.Current
                    ), 
                    millisecondsTimeout: milliseconds,
                    cancellationTokenSource: cts
                );

            task.Wait();

            return task.Result;
        }

        private Task<TRequest> TimeoutAfter(Task<TRequest> task, int millisecondsTimeout, CancellationTokenSource cancellationTokenSource)
        {
            // Short-circuit #1: infinite timeout or task already completed
            if (task.IsCompleted || (millisecondsTimeout == Timeout.Infinite))
            {
                // Either the task has already completed or timeout will never occur.
                // No proxy necessary.
                return task;
            }

            // tcs.Task will be returned as a proxy to the caller
            var tcs = new TaskCompletionSource<TRequest>();

            // Short-circuit #2: zero timeout
            if (millisecondsTimeout == 0)
            {
                //signal cancellation to tasks that have run out of time - its up to them to try and abort
                cancellationTokenSource.Cancel();

                // We've already timed out.
                tcs.SetException(new TimeoutException());
                return tcs.Task;
            }

            // Set up a timer to complete after the specified timeout period
            var timer = new Timer(state =>
                {
                    // Recover your state information
                    var myTcs = (TaskCompletionSource<TRequest>) state;

                    //signal cancellation to tasks that have run out of time - its up to them to try and abort
                    cancellationTokenSource.Cancel();

                    // Fault our proxy with a TimeoutException
                    myTcs.TrySetException(new TimeoutException());
                }, 
                tcs, 
                millisecondsTimeout, 
                Timeout.Infinite
            );

            // Wire up the logic for what happens when source task completes
            task.ContinueWith((antecedent, state) =>
                {
                    // Recover our state data
                    var tuple = (Tuple<Timer, TaskCompletionSource<TRequest>>) state;

                    // Cancel the Timer
                    tuple.Item1.Dispose();

                    // Marshal results to proxy
                    MarshalTaskResults(antecedent, tuple.Item2);
                },
                Tuple.Create(timer, tcs), 
                CancellationToken.None, 
                TaskContinuationOptions.ExecuteSynchronously, 
                TaskScheduler.Default
            );

            return tcs.Task;
        }

        private void MarshalTaskResults(Task<TRequest> source, TaskCompletionSource<TRequest> proxy)
        {
            switch (source.Status)
            {
                case TaskStatus.Faulted:
                    proxy.TrySetException(source.Exception);
                    break;
                case TaskStatus.Canceled:
                    proxy.TrySetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    proxy.TrySetResult(source.Result); 
                    break;
            }
        }
        
    }
}