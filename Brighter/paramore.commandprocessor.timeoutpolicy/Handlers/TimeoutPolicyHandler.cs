#region Licence
/* 
 
TimeoutAfter method derived from work by Joe Hoag http://blogs.msdn.com/b/pfxteam/archive/2011/11/10/10235834.aspx 
No permissions is granted to the portions of the source code that derive from Joe Hoag's which is excluded from the following licence
 
The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;


namespace paramore.brighter.commandprocessor.timeoutpolicy.Handlers
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

        #region Not covered by MIT licence

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

        #endregion

    }
}