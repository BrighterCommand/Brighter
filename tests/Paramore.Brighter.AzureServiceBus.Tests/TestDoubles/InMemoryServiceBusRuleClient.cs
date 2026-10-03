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

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public sealed class InMemoryServiceBusRuleClient : ServiceBusAdministrationClient, IServiceBusClientProvider, IAsyncDisposable
{
    private readonly InMemoryServiceBusClient _client = new(ServiceBusModelFactory.ServiceBusReceivedMessage());

    public Dictionary<string, RuleProperties> Rules { get; } = new(StringComparer.Ordinal);
    public bool SubscriptionExists { get; set; } = true;
    public int CreatedRules { get; private set; }
    public int UpdatedRules { get; private set; }
    public int DeletedRules { get; private set; }
    public int ReadRules { get; private set; }
    public Exception? CreateRuleException { get; set; }
    public Exception? UpdateRuleException { get; set; }
    public Exception? DeleteRuleException { get; set; }
    public bool SubscriptionCreatedByAnotherClient { get; set; }
    public bool RuleCreatedByAnotherClient { get; set; }

    public ServiceBusClient GetServiceBusClient() => _client;
    public ServiceBusAdministrationClient GetServiceBusAdministrationClient() => this;
    public ValueTask DisposeAsync() => _client.DisposeAsync();

    public override Task<Response<bool>> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
        => Task.FromResult(Response.FromValue(true, null!));

    public override Task<Response<bool>> SubscriptionExistsAsync(string topicName, string subscriptionName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Response.FromValue(SubscriptionExists, null!));

    public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options,
        CreateRuleOptions rule, CancellationToken cancellationToken = default)
    {
        if (SubscriptionCreatedByAnotherClient)
        {
            SubscriptionExists = true;
            Rules["$Default"] = ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter());
        }

        if (SubscriptionExists)
        {
            throw new ServiceBusException("Subscription exists.", ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        SubscriptionExists = true;
        Rules.Add(rule.Name, ServiceBusModelFactory.RuleProperties(rule.Name, rule.Filter, rule.Action));
        return Task.FromResult(Response.FromValue(ServiceBusModelFactory.SubscriptionProperties(options.TopicName,
            options.SubscriptionName, options.LockDuration, options.RequiresSession, options.DefaultMessageTimeToLive,
            options.AutoDeleteOnIdle, options.DeadLetteringOnMessageExpiration, options.MaxDeliveryCount, userMetadata: string.Empty), null!));
    }

    public override Task<Response<RuleProperties>> GetRuleAsync(string topicName, string subscriptionName, string ruleName,
        CancellationToken cancellationToken = default)
    {
        ReadRules++;
        if (!Rules.TryGetValue(ruleName, out var rule))
        {
            throw new ServiceBusException("Rule not found.", ServiceBusFailureReason.MessagingEntityNotFound);
        }

        return Task.FromResult(Response.FromValue(ServiceBusModelFactory.RuleProperties(rule.Name, rule.Filter, rule.Action), null!));
    }

    public override Task<Response<RuleProperties>> CreateRuleAsync(string topicName, string subscriptionName, CreateRuleOptions options,
        CancellationToken cancellationToken = default)
    {
        if (CreateRuleException is not null)
        {
            throw CreateRuleException;
        }

        if (RuleCreatedByAnotherClient)
        {
            Rules[options.Name] = ServiceBusModelFactory.RuleProperties(options.Name, new FalseRuleFilter());
        }

        if (Rules.ContainsKey(options.Name))
        {
            throw new ServiceBusException("Rule exists.", ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        CreatedRules++;
        var rule = ServiceBusModelFactory.RuleProperties(options.Name, options.Filter, options.Action);
        Rules.Add(rule.Name, rule);
        return Task.FromResult(Response.FromValue(rule, null!));
    }

    public override Task<Response<RuleProperties>> UpdateRuleAsync(string topicName, string subscriptionName, RuleProperties rule,
        CancellationToken cancellationToken = default)
    {
        if (UpdateRuleException is not null)
        {
            throw UpdateRuleException;
        }

        UpdatedRules++;
        Rules[rule.Name] = ServiceBusModelFactory.RuleProperties(rule.Name, rule.Filter, rule.Action);
        return Task.FromResult(Response.FromValue(rule, null!));
    }

    public override Task<Response> DeleteRuleAsync(string topicName, string subscriptionName, string ruleName,
        CancellationToken cancellationToken = default)
    {
        if (DeleteRuleException is not null)
        {
            throw DeleteRuleException;
        }

        if (!Rules.Remove(ruleName))
        {
            throw new ServiceBusException("Rule not found.", ServiceBusFailureReason.MessagingEntityNotFound);
        }

        DeletedRules++;
        return Task.FromResult<Response>(null!);
    }
}
