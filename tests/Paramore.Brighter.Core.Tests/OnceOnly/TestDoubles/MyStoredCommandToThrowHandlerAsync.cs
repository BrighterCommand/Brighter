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

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;

namespace Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles
{
    internal class MyStoredCommandToThrowHandlerAsync : RequestHandlerAsync<MyCommand>
    {
        public static bool CommandReceived { get; set; }
        
        [UseInboxAsync(1, onceOnly: true, onceOnlyAction: OnceOnlyAction.Throw, contextKey: typeof(MyStoredCommandToThrowHandlerAsync))]
        public override async Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
        {
            CommandReceived = true;

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        public static bool DidReceive(MyCommand command)
        {
            return CommandReceived;
        }
    }
}
