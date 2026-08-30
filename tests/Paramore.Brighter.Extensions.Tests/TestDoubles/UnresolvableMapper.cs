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
/// A mapper for <see cref="UnresolvableCommand"/> whose constructor depends on
/// <see cref="IUnregisteredDependency"/>, a type deliberately never registered in a test's container,
/// so building the pipeline's <c>Scoped</c> mapper always fails with the container's own
/// "unable to resolve service" exception.
/// </summary>
public sealed class UnresolvableMapper : IAmAMessageMapper<UnresolvableCommand>
{
    public UnresolvableMapper(IUnregisteredDependency dependency)
    {
    }

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(UnresolvableCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

    public UnresolvableCommand MapToRequest(Message message) => new();
}
