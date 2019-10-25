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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyEventHandlerAsync : RequestHandlerAsync<MyEvent>
    {
        private readonly IDictionary<string, Guid> _receivedMessages;

        public MyEventHandlerAsync(IDictionary<string, Guid> receivedMessages)
        {
            _receivedMessages = receivedMessages;
        }

        public override async Task<MyEvent> HandleAsync(MyEvent command, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return command;

            _receivedMessages.Add(nameof(MyEventHandlerAsync), command.Id);

            await Task.Delay(0, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
