#region Licence
/* The MIT License (MIT)
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.policy.Attributes;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.Timeout.Test_Doubles
{
    internal class MyFailsDueToTimeoutHandler : RequestHandler<MyCommand>
    {
        [TimeoutPolicy(milliseconds: 300, step: 1, timing: HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command)
        {
            var ct = (CancellationToken)Context.Bag[TimeoutPolicyHandler<MyCommand>.CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN];
            if (ct.IsCancellationRequested)
            {
                //already died
                MyFailsDueToTimeoutHandlerStateTracker.WasCancelled = true;
                return base.Handle(command);
            }
            try
            {
                var delay = Task.Delay(500, ct).ContinueWith(
                    x =>
                    {
                        // done something I should not do, because I should of been cancel
                        MyFailsDueToTimeoutHandlerStateTracker.WasCancelled = false;
                    },
                    ct);

                Task.WaitAll(new[] { delay });
            }
            catch (AggregateException e)
            {
                foreach (var tce in e.InnerExceptions.OfType<TaskCanceledException>())
                {
                    MyFailsDueToTimeoutHandlerStateTracker.WasCancelled = true;
                    MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted = false;
                    return base.Handle(command);
                }
            }

            MyFailsDueToTimeoutHandlerStateTracker.TaskCompleted = true;
            return base.Handle(command);
        }
    }

    public static class MyFailsDueToTimeoutHandlerStateTracker
    {
        public static bool WasCancelled { get; set; }
        public static bool TaskCompleted { get; set; }
    }
}