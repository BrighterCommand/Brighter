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
/// The async/Proactor twin of <see cref="MinimalMapper"/>.
/// </summary>
public sealed class MinimalMapperAsync : IAmAMessageMapperAsync<MinimalCommand>
{
    public IRequestContext? Context { get; set; }

    public Task<Message> MapToMessageAsync(MinimalCommand request, Publication publication,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Message(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test")));

    [TrackingUnwrapWith(0)]
    public Task<MinimalCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MinimalCommand());
}
