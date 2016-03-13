#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.monitoring.Attributes;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.Logging;


namespace paramore.commandprocessor.tests.Monitoring.TestDoubles
{
    internal class MyMonitoredHandlerThatThrowsAsync : RequestHandlerAsync<MyCommand>
    {
        public MyMonitoredHandlerThatThrowsAsync(ILog logger) 
            : base(logger)
        {}

        [MonitorAsync(step: 1, timing: HandlerTiming.Before, handlerType: typeof(MyMonitoredHandlerThatThrowsAsync))]
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken? ct = null)
        {
            throw new ApplicationException("I am an exception in a monitored pipeline");
        }
    }
}
