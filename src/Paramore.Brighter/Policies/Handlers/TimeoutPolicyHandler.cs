#region Licence
/* 
------------------------------------------------------------------------------------------------  
TimeoutAfter method derived from work by Joe Hoag http://blogs.msdn.com/b/pfxteam/archive/2011/11/10/10235834.aspx 
Under http://msdn.microsoft.com/en-us/cc300389.aspx section 2b, this code is considered covered by the MLPL

Microsoft Limited Public License (Ms-LPL)

This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.

1. Definitions

The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law. A "contribution" is the original software, or any additions or changes to the software. A "contributor" is any person that distributes its contribution under this license. "Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights

(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations

(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees, or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.

4. (F) Platform Limitation- The licenses granted in sections 2(A) & 2(B) extend only to the software or derivative works that you create that run on a Microsoft Windows operating system product.

Read more about this license at http://www.codeplex.com/clrinterop/license
  
------------------------------------------------------------------------------------------------   
  
------------------------------------------------------------------------------------------------  
Non derivative portions are covered by the MIT Licence
 
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
THE SOFTWARE.
--------------------------------------------------------------------------------------------------
 */

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Policies.Attributes;

namespace Paramore.Brighter.Policies.Handlers
{
    /// <summary>
    /// Class TimeoutPolicyHandler.
    /// The handler is injected into the pipeline if the <see cref="TimeoutPolicyAttribute"/>
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    [Obsolete("Migrate to UsePolicyAttribute or UseResiliencePipelineAttribute instead")]
    public class TimeoutPolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// The context holds a timeout cancellation token with this key, that can be used by handlers to cancel an operation
        /// and kill the thread which manages the timeout
        /// </summary>
        public const string CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN = "TimeoutCancellationToken";
        private int _milliseconds;

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object?[] initializerList)
        {
            _milliseconds = (int?)initializerList[0] ?? 0;
        }

        /// <summary>
        /// Runs the remainder of the pipeline within a parentTask that will timeout if it does not complete within the
        /// configured number of milliseconds
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override TRequest Handle(TRequest command)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var timeoutTask = Task<TRequest>.Factory.StartNew(
                function: () =>
                {
                    //we already cancelled the parentTask
                    ct.ThrowIfCancellationRequested();
                    //allow the handlers that can timeout to grab the cancellation token
                    Context?.Bag.AddOrUpdate(CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN, ct, (s, o) => o = ct);
                    return base.Handle(command);
                },
                cancellationToken: ct,
                creationOptions: TaskCreationOptions.LongRunning,
                scheduler: TaskScheduler.Current
                );

            var task = TimeoutAfter(task: timeoutTask, millisecondsTimeout: _milliseconds, cancellationTokenSource: cts);

            task.Wait(ct);

            return task.Result;
        }

        #region Covered by MLPL licence

        private Task<TRequest> TimeoutAfter(Task<TRequest> task, int millisecondsTimeout, CancellationTokenSource cancellationTokenSource)
        {
            // Short-circuit #1: infinite timeout or parentTask already completed
            if (task.IsCompleted || (millisecondsTimeout == Timeout.Infinite))
            {
                // Either the parentTask has already completed or timeout will never occur.
                // No proxy necessary.
                return task;
            }

            // tcs.Task will be returned as a proxy to the caller
            var tcs = new TaskCompletionSource<TRequest>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                    var myTcs = (TaskCompletionSource<TRequest>?)state;

                    //signal cancellation to tasks that have run out of time - its up to them to try and abort
                    cancellationTokenSource.Cancel();

                    // Fault our proxy with a TimeoutException
                    myTcs!.TrySetException(new TimeoutException());
                },
                tcs,
                millisecondsTimeout,
                Timeout.Infinite
            );

            // Wire up the logic for what happens when source parentTask completes
            task.ContinueWith((antecedent, state) =>
                {
                    // Recover our state data
                    var tuple = (Tuple<Timer, TaskCompletionSource<TRequest>>?)state;

                    // Cancel the Timer
                    tuple!.Item1.Dispose();

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
                    proxy.TrySetException(source.Exception!);
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
