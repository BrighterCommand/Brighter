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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A mapper for <see cref="PoisonedScopeCompletingCommand"/> whose constructor resolves
/// <see cref="IPoisonedDependency"/> successfully — so the pipeline scope tracks it for disposal — and
/// whose <see cref="MapToMessage"/> carries no <c>[WrapWith]</c> transform, so the wrap pipeline always
/// builds and completes. Disposing the pipeline afterwards disposes the scope, which then throws from
/// <see cref="IPoisonedDependency"/>'s own <c>Dispose()</c> — unlike <see cref="PoisonedScopeMapper"/>,
/// where the pipeline never finishes building in the first place.
/// </summary>
public sealed class PoisonedScopeCompletingMapper : IAmAMessageMapper<PoisonedScopeCompletingCommand>
{
    public PoisonedScopeCompletingMapper(IPoisonedDependency poisoned)
    {
    }

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(PoisonedScopeCompletingCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

    public PoisonedScopeCompletingCommand MapToRequest(Message message) => new();
}
