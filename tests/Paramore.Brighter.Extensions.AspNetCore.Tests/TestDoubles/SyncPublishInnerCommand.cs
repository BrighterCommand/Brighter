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

using System;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A minimal command handled only by <see cref="SyncPublishInnerCommandHandler"/>, <c>Send</c>'d
/// synchronously from inside <see cref="SyncPublishSubscriberOne"/>'s and
/// <see cref="SyncPublishSubscriberTwo"/>'s own <c>Handle</c> (AC-39). Carries the issuing subscriber's
/// own marker, so the handler can record which subscriber's nested <c>Send</c> resolved which
/// <see cref="IOrderDbContext"/> without relying on which subscriber happened to run first.
/// </summary>
public sealed class SyncPublishInnerCommand : Command
{
    public SyncPublishInnerCommand(string subscriberMarker) : base(Guid.NewGuid())
    {
        SubscriberMarker = subscriberMarker;
    }

    /// <summary>
    /// The marker of the subscriber that issued this nested <c>Send</c>.
    /// </summary>
    public string SubscriberMarker { get; }
}
