#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

internal sealed class InMemoryRacingAdministrationClient(int participants = 1) : IAdministrationClientWrapper
{
    private readonly ConcurrentDictionary<string, byte> _entities = new();
    private readonly TaskCompletionSource<bool> _checksCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _existsCount;
    private int _createCount;
    private int _resetCount;

    public int ExistsCount => Volatile.Read(ref _existsCount);
    public int CreateCount => Volatile.Read(ref _createCount);
    public int ResetCount => Volatile.Read(ref _resetCount);
    public bool CreateByAnotherCallerAfterCheck { get; init; }
    public Exception? ExistsException { get; set; }
    public Exception? CreateException { get; set; }

    public Task<bool> TopicExistsAsync(string topicName) => ExistsAsync("topic:" + topicName);
    public Task<bool> QueueExistsAsync(string queueName) => ExistsAsync("queue:" + queueName);

    private async Task<bool> ExistsAsync(string entity)
    {
        var exists = _entities.ContainsKey(entity);
        var count = Interlocked.Increment(ref _existsCount);
        if (ExistsException is not null)
            throw ExistsException;

        if (CreateByAnotherCallerAfterCheck)
            _entities.TryAdd(entity, 0);

        // All participants observe absence before any create is allowed to complete.
        if (count >= participants)
            _checksCompleted.TrySetResult(true);
        await _checksCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return exists;
    }

    public Task CreateQueueAsync(string queueName, TimeSpan? autoDeleteOnIdle = null,
        long? maxMessageSizeInKilobytes = default) => CreateAsync("queue:" + queueName);

    public Task CreateTopicAsync(string topicName, TimeSpan? autoDeleteOnIdle = null,
        long? maxMessageSizeInKilobytes = default) => CreateAsync("topic:" + topicName);

    private Task CreateAsync(string entity)
    {
        Interlocked.Increment(ref _createCount);
        if (CreateException is not null)
            throw CreateException;
        if (!_entities.TryAdd(entity, 0))
            throw new ServiceBusException("The entity already exists.",
                ServiceBusFailureReason.MessagingEntityAlreadyExists);
        return Task.CompletedTask;
    }

    public void Reset() => Interlocked.Increment(ref _resetCount);

    public Task DeleteQueueAsync(string queueName)
    {
        _entities.TryRemove("queue:" + queueName, out _);
        return Task.CompletedTask;
    }

    public Task DeleteTopicAsync(string topicName)
    {
        _entities.TryRemove("topic:" + topicName, out _);
        return Task.CompletedTask;
    }

    public Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName)
        => throw new NotSupportedException();

    public Task CreateSubscriptionAsync(string topicName, string subscriptionName,
        AzureServiceBusSubscriptionConfiguration subscriptionConfiguration) => throw new NotSupportedException();

    public Task<SubscriptionProperties> GetSubscriptionAsync(string topicName, string subscriptionName,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
