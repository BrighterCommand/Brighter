#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

namespace Paramore.Brighter.MessagingGateway.MsSql;

internal sealed class MsSqlPublicationSnapshot
{
    private readonly RoutingKey? _topic;

    internal Publication Publication { get; }
    internal ChannelName[]? SubscriberChannels { get; }
    internal int SendTimeoutSeconds { get; }

    internal MsSqlPublicationSnapshot(Publication publication)
    {
        Publication = publication ?? throw new ArgumentNullException(nameof(publication));
        if (publication is not MsSqlPublication fanOut)
            return;

        SubscriberChannels = fanOut.GetSubscriberChannels();
        _topic = fanOut.Topic;
        SendTimeoutSeconds = GetTimeoutSeconds(fanOut.SendTimeout);
    }

    internal void ValidateMessageTopic(RoutingKey topic)
    {
        if (SubscriberChannels != null && topic != _topic)
            throw new ConfigurationException("The message topic must match the MS SQL fan-out publication's logical topic.");
    }

    private static int GetTimeoutSeconds(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout.TotalSeconds > int.MaxValue)
            throw new ConfigurationException("An MS SQL fan-out send timeout must be between zero and Int32.MaxValue seconds.");

        return (int)Math.Ceiling(timeout.TotalSeconds);
    }
}
