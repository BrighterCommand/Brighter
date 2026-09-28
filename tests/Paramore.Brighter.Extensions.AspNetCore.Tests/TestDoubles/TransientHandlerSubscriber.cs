#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// The one subscriber to <see cref="TransientHandlerPublishedEvent"/> (AC-47). Its own pipeline is
/// <c>Transient</c> and therefore takes no pipeline scope of its own, unlike every other subscriber
/// double in this project; issues a nested <c>Post</c> of <see cref="TransientHandlerPublishPostedCommand"/>
/// from inside <c>HandleAsync</c>, so a test can assert suppression still applies even though this
/// subscriber's own pipeline never asked for an ambient scope.
/// </summary>
public sealed class TransientHandlerSubscriber : RequestHandlerAsync<TransientHandlerPublishedEvent>
{
    private readonly IAmACommandProcessor _commandProcessor;

    public TransientHandlerSubscriber(IAmACommandProcessor commandProcessor)
    {
        _commandProcessor = commandProcessor;
    }

    public override async Task<TransientHandlerPublishedEvent> HandleAsync(
        TransientHandlerPublishedEvent @event, CancellationToken cancellationToken = default)
    {
        _commandProcessor.Post(new TransientHandlerPublishPostedCommand());
        return await base.HandleAsync(@event, cancellationToken);
    }
}
