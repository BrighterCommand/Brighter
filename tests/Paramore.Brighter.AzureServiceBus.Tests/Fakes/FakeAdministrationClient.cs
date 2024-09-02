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
    
    public int ExistCount { get; private set; } = 0;

    public Exception CreateSubscriptionException { get; set; } = null;

    public Exception ExistsException { get; set; } = null;
    public int CreateCount { get; private set; } = 0;

    public bool TopicExists(string topicName)
    {
        ExistCount++;
        if (ExistsException != null)
            throw ExistsException;
        return Topics.Keys.Any(t => t.Equals(topicName, StringComparison.InvariantCultureIgnoreCase));
    }

    public bool QueueExists(string queueName)
    {
        ExistCount++;
        if (ExistsException != null)
            throw ExistsException;
        return Queues.Any(q => q.Equals(queueName, StringComparison.InvariantCultureIgnoreCase));
    }

    public void CreateQueue(string queueName, TimeSpan? autoDeleteOnIdle = null)
    {
        CreateCount++;
        Queues.Add(queueName);   
    }

    public void CreateTopic(string topicName, TimeSpan? autoDeleteOnIdle = null)
    {
        CreateCount++;
        Topics.Add(topicName, []);
    }

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
        ExistCount++;
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
        ExistCount = 0;
        CreateCount = 0;

        CreateSubscriptionException = null;
        ExistsException = null;
    }
}
