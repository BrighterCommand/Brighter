#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public class OutboxSweeper
    {
        private readonly TimeSpan _timeSinceSent;
        private readonly IAmAnOutboxProducerMediator _outboxProducerMediator;
        private readonly int _batchSize;
        private readonly bool _useBulk;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly Dictionary<string, object>? _args;

        private const string IMPLICITCLEAROUTBOX = "Implicit Clear Outbox";

        /// <summary>
        /// This sweeper clears an outbox of any outstanding messages within the time interval
        /// </summary>
        /// <param name="timeSinceSent">How long can a message sit in the box before we attempt to resend</param>
        /// <param name="outboxProducerMediator">Outbox Producer Mediator</param>
        /// <param name="requestContextFactory">Allows us to create a request context to pass down the pipeline when clearing the Outbox</param>
        /// <param name="batchSize">The maximum number of messages to dispatch.</param>
        /// <param name="useBulk">Use the producers bulk dispatch functionality.</param>
        /// <param name="args">Optional bag of parameters to pass to the Outbox</param>
        public OutboxSweeper(
            TimeSpan timeSinceSent, 
            IAmAnOutboxProducerMediator outboxProducerMediator, 
            IAmARequestContextFactory requestContextFactory,
            int batchSize = 100,
            bool useBulk = false,
            Dictionary<string, object>? args = null)
        {
            _timeSinceSent = timeSinceSent;
            _outboxProducerMediator = outboxProducerMediator;
            _batchSize = batchSize;
            _useBulk = useBulk;
            _requestContextFactory = requestContextFactory;
            _args = args;
        }

        /// <summary>
        /// Dispatches the oldest un-dispatched messages from the outbox in a background thread.
        /// </summary>
        public async Task SweepAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var span = ApplicationTelemetry.ActivitySource.StartActivity(IMPLICITCLEAROUTBOX, ActivityKind.Server);
#pragma warning restore CS0618 // Type or member is obsolete
            var context = _requestContextFactory.Create();
            context.Span = span;
            
            await _outboxProducerMediator.ClearOutstandingFromOutboxAsync(_batchSize, _timeSinceSent, _useBulk, context, _args);
        }
    }
}
