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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

/// <summary>
/// An in-memory stand-in for the Service Bus management API: entities exist only once they have been
/// created through it, and every subscription it creates is recorded so a test can see when provisioning happened.
/// <see cref="RequestCount"/> counts every call made to the management API.
/// </summary>
public class InMemoryServiceBusAdministrationClient : ServiceBusAdministrationClient
{
    private readonly HashSet<string> _topics = [];

    public List<(string Topic, string Subscription)> CreatedSubscriptions { get; } = [];

    public int RequestCount { get; private set; }

    public override Task<Response<bool>> TopicExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        RequestCount++;
        return Task.FromResult(Response.FromValue(_topics.Contains(name), null!));
    }

    public override Task<Response<bool>> SubscriptionExistsAsync(string topicName, string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        RequestCount++;
        return Task.FromResult(Response.FromValue(CreatedSubscriptions.Contains((topicName, subscriptionName)), null!));
    }

    public override Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options,
        CancellationToken cancellationToken = default)
    {
        RequestCount++;
        _topics.Add(options.Name);
        return Task.FromResult(Response.FromValue<TopicProperties>(null!, null!));
    }

    public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options,
        CreateRuleOptions rule, CancellationToken cancellationToken = default)
    {
        RequestCount++;
        CreatedSubscriptions.Add((options.TopicName, options.SubscriptionName));
        return Task.FromResult(Response.FromValue<SubscriptionProperties>(null!, null!));
    }
}
