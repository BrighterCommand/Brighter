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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// The async twin of <see cref="NestedPipelineSubscriber"/>: from inside its own <see cref="HandleAsync"/>,
/// issues a nested <see cref="AmbientAdoptionCommand"/> <c>Send</c> and a nested
/// <see cref="AmbientAdoptionPostCommand"/> <c>Post</c> on the singleton <see cref="IAmACommandProcessor"/>.
/// </summary>
public sealed class NestedPipelineSubscriberAsync : RequestHandlerAsync<NestedPipelinePublishedEvent>
{
    private readonly IAmACommandProcessor _commandProcessor;

    public NestedPipelineSubscriberAsync(IAmACommandProcessor commandProcessor)
    {
        _commandProcessor = commandProcessor;
    }

    public override async Task<NestedPipelinePublishedEvent> HandleAsync(NestedPipelinePublishedEvent @event, CancellationToken cancellationToken = default)
    {
        _commandProcessor.Send(new AmbientAdoptionCommand());
        _commandProcessor.Post(new AmbientAdoptionPostCommand());
        return await base.HandleAsync(@event, cancellationToken);
    }
}
