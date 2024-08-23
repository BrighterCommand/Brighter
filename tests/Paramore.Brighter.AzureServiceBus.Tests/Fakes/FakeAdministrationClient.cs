using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeAdministrationClient : IAdministrationClientWrapper
{
    public Dictionary<string, List<string>> Topics { get; } = new();
    public List<string> Queues { get; } = new ();

    public int ResetCount { get; private set; } = 0;
    
    public int SubscriptionExistCount { get; private set; } = 0;

    public Exception CreateSubscriptionException { get; set; } = null;

    public bool TopicExists(string topicName)
        => Topics.Keys.Any(t => t.Equals(topicName, StringComparison.InvariantCultureIgnoreCase));

    public bool QueueExists(string queueName)
        => Queues.Any(q => q.Equals(queueName, StringComparison.InvariantCultureIgnoreCase));

    public void CreateQueue(string queueName, TimeSpan? autoDeleteOnIdle = null)
        => Queues.Add(queueName);

    public void CreateTopic(string topicName, TimeSpan? autoDeleteOnIdle = null)
        => Topics.Add(topicName, []);

    public Task DeleteQueueAsync(string queueName)
    {
        Queues.Remove(queueName);
        return Task.CompletedTask;
    }

    public Task DeleteTopicAsync(string topicName)
    {
        Topics.Remove(topicName);
        return Task.CompletedTask;
    }

    public bool SubscriptionExists(string topicName, string subscriptionName)
    {
        SubscriptionExistCount++;
        return Topics.ContainsKey(topicName) && Topics[topicName].Contains(subscriptionName);
    }

    public void CreateSubscription(string topicName, string subscriptionName,
        AzureServiceBusSubscriptionConfiguration subscriptionConfiguration)
    {
        if (CreateSubscriptionException != null) throw CreateSubscriptionException;
        Topics[topicName].Add(subscriptionName);
    }

    public void Reset()
        => ResetCount++;

    public Task<SubscriptionProperties> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void ResetState()
    {
        Topics.Clear();
        Queues.Clear();
        ResetCount = 0;
        SubscriptionExistCount = 0;

        CreateSubscriptionException = null;
    }
}
