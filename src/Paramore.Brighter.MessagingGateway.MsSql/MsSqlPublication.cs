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
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.MessagingGateway.MsSql;

/// <summary>
/// Publishes one copy to each configured SQL subscriber queue in a single atomic insert.
/// </summary>
/// <remarks>
/// Subscriber queues are configured on the producer; no subscription discovery is performed.
/// Copies retain their logical topic. Receivers sharing a channel compete for its copy.
/// Use a plain <see cref="Publication"/> for the existing single-queue behavior.
/// </remarks>
public class MsSqlPublication : Publication
{
    /// <summary>
    /// Gets or sets the physical queues that receive a copy of each published message.
    /// </summary>
    /// <value>Between one and 1,000 nonblank channel names, each at most 255 UTF-16 code units.</value>
    /// <remarks>
    /// Names equivalent under the queue column's collation receive one copy.
    /// Each channel name must differ from the publication's logical topic.
    /// </remarks>
    public IReadOnlyCollection<ChannelName> SubscriberChannels { get; set; } = Array.Empty<ChannelName>();

    internal ChannelName[] GetSubscriberChannels(RoutingKey? messageTopic = null)
    {
        if (Topic == null || string.IsNullOrWhiteSpace(Topic.Value))
            throw new ConfigurationException("An MS SQL fan-out publication requires a logical topic.");
        if (messageTopic != null && messageTopic != Topic)
            throw new ConfigurationException("The message topic must match the MS SQL fan-out publication's logical topic.");

        var channels = SubscriberChannels?.ToArray();
        if (channels == null || channels.Length == 0 || channels.Length > 1000)
            throw new ConfigurationException("An MS SQL fan-out publication requires between 1 and 1000 subscriber channels.");

        foreach (var channel in channels)
            ValidateSubscriberChannel(channel);

        return channels;
    }

    private void ValidateSubscriberChannel(ChannelName channel)
    {
        if (channel == null || string.IsNullOrWhiteSpace(channel.Value))
            throw new ConfigurationException("An MS SQL subscriber channel must have a non-blank name.");
        if (channel.Value.Length > 255)
            throw new ConfigurationException("An MS SQL subscriber channel name must not exceed 255 characters.");
        if (channel.Value == Topic!.Value)
            throw new ConfigurationException("An MS SQL subscriber channel must differ from its logical topic so delayed retries can be targeted safely.");
    }
}

/// <summary>
/// Configures SQL fan-out for a request type.
/// </summary>
/// <typeparam name="T">The published request type, which must implement <see cref="IRequest"/>.</typeparam>
public class MsSqlPublication<T> : MsSqlPublication where T : class, IRequest
{
    /// <summary>
    /// Initializes the publication's request type to <typeparamref name="T"/>.
    /// </summary>
    public MsSqlPublication() => RequestType = typeof(T);
}
