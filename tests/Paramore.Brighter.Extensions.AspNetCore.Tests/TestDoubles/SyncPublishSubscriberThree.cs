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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// The third of three synchronous subscribers to <see cref="SyncPublishOrderPlaced"/> (AC-39). Records
/// the <see cref="IOrderDbContext"/> it was constructed with into an injected
/// <see cref="SyncPublishRecorder"/>, keyed by its own marker. Issues no nested pipeline of its own -
/// only <see cref="SyncPublishSubscriberOne"/> and <see cref="SyncPublishSubscriberTwo"/> do.
/// </summary>
public sealed class SyncPublishSubscriberThree : RequestHandler<SyncPublishOrderPlaced>
{
    /// <summary>
    /// This subscriber's own marker, used to key its recorded instance rather than relying on execution
    /// order.
    /// </summary>
    public const string Marker = nameof(SyncPublishSubscriberThree);

    public SyncPublishSubscriberThree(IOrderDbContext orderDbContext, SyncPublishRecorder recorder)
    {
        recorder.RecordSubscriberInstance(Marker, orderDbContext);
    }

    public override SyncPublishOrderPlaced Handle(SyncPublishOrderPlaced @event) => base.Handle(@event);
}
