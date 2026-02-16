#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Paramore.Brighter.Actions;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal sealed class SpyRequeueCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyRequeueCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command, RequestContext? requestContext = null)
        {
            base.Send(command, requestContext);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override void Publish<T>(T @event, RequestContext? requestContext = null)
        {
            base.Publish(@event, requestContext);
            PublishCount++;

            var exceptions = new List<Exception> { new DeferMessageAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

        public override async Task SendAsync<T>(
            T command,
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            )
        {
            await base.SendAsync(command, requestContext, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new DeferMessageAction();
        }

        public override async Task PublishAsync<T>(
            T @event,
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            )
        {
            await base.PublishAsync(@event, requestContext, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new DeferMessageAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
    }

    internal sealed class SpyDontAckCommandProcessor : SpyCommandProcessor
    {
        private readonly SemaphoreSlim _handled = new(0);

        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyDontAckCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public bool WaitForHandle(int timeoutMs = 5000) => _handled.Wait(timeoutMs);

        public override void Send<T>(T command, RequestContext? requestContext = null)
        {
            base.Send(command, requestContext);
            SendCount++;
            _handled.Release();
            throw new DontAckAction();
        }

        public override void Publish<T>(T @event, RequestContext? requestContext = null)
        {
            base.Publish(@event, requestContext);
            PublishCount++;
            _handled.Release();

            var exceptions = new List<Exception> { new DontAckAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

        public override async Task SendAsync<T>(
            T command,
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            await base.SendAsync(command, requestContext, continueOnCapturedContext, cancellationToken);
            SendCount++;
            _handled.Release();
            throw new DontAckAction();
        }

        public override async Task PublishAsync<T>(
            T @event,
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            await base.PublishAsync(@event, requestContext, continueOnCapturedContext, cancellationToken);
            PublishCount++;
            _handled.Release();

            var exceptions = new List<Exception> { new DontAckAction() };

            throw new AggregateException(
                "Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }
    }

    internal sealed class SpyExceptionCommandProcessor : SpyCommandProcessor
    {
        public int SendCount { get; set; }
        public int PublishCount { get; set; }

        public SpyExceptionCommandProcessor()
        {
            SendCount = 0;
            PublishCount = 0;
        }

        public override void Send<T>(T command, RequestContext? requestContext = null)
        {
            base.Send(command, requestContext);
            SendCount++;
            throw new Exception();
        }

        public override void Publish<T>(T @event, RequestContext? requestContext = null)
        {
            base.Publish(@event, requestContext);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

        public override async Task SendAsync<T>(T command, RequestContext? requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        {
            await base.SendAsync(command, requestContext, continueOnCapturedContext, cancellationToken);
            SendCount++;
            throw new Exception();
        }

        public override async Task PublishAsync<T>(T @event, RequestContext? requestContext = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default)
        {
            await base.PublishAsync(@event, requestContext, continueOnCapturedContext, cancellationToken);
            PublishCount++;

            var exceptions = new List<Exception> { new Exception() };

            throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
        }

    }
}
