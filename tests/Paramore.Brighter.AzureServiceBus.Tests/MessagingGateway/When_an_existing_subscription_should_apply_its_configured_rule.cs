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
#pragma warning disable CS0618 // Exercise compatibility with the legacy SqlFilter field.

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusSubscriptionRuleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_an_existing_subscription_should_apply_its_configured_rule(bool isAsync)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        client.Rules.Add("sqlFilter", ServiceBusModelFactory.RuleProperties("sqlFilter", new SqlRuleFilter("region = 'old'")));
        var configuration = new AzureServiceBusSubscriptionConfiguration { SqlFilter = "region = 'new'" };

        //Act
        await ReceiveAsync(client, configuration, isAsync);

        //Assert
        Assert.Equal("region = 'new'", Assert.IsType<SqlRuleFilter>(client.Rules["sqlFilter"].Filter).SqlExpression);
        Assert.Equal(1, client.UpdatedRules);
    }

    [Theory]
    [InlineData("sql", false)]
    [InlineData("sql", true)]
    [InlineData("correlation", false)]
    [InlineData("correlation", true)]
    [InlineData("true", false)]
    [InlineData("false", true)]
    public async Task When_creating_a_subscription_should_apply_its_full_rule(string filterType, bool isAsync)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient { SubscriptionExists = false };
        var rule = CreateRule(filterType);

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = rule }, isAsync);

        //Assert
        var stored = Assert.Single(client.Rules).Value;
        Assert.Equal(rule.Name, stored.Name);
        Assert.Equal(rule.Filter, stored.Filter);
        Assert.Equal(rule.Action, stored.Action);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_named_rule_is_missing_should_create_it_before_removing_the_default(bool isAsync)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        client.Rules.Add("$Default", ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter()));
        client.Rules.Add("external", ServiceBusModelFactory.RuleProperties("external", new FalseRuleFilter()));
        var rule = CreateRule("correlation");

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = rule }, isAsync);

        //Assert
        Assert.Equal(rule.Filter, client.Rules[rule.Name].Filter);
        Assert.Equal(rule.Action, client.Rules[rule.Name].Action);
        Assert.False(client.Rules.ContainsKey("$Default"));
        Assert.True(client.Rules.ContainsKey("external"));
        Assert.Equal(1, client.CreatedRules);
        Assert.Equal(1, client.DeletedRules);
    }

    [Theory]
    [InlineData("sql")]
    [InlineData("correlation")]
    public async Task When_a_rule_changes_should_update_its_filter_and_action(string filterType)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        var oldRule = CreateRule(filterType);
        client.Rules.Add(oldRule.Name, ServiceBusModelFactory.RuleProperties(oldRule.Name, oldRule.Filter, oldRule.Action));
        var desired = filterType == "correlation"
            ? new CreateRuleOptions("orders", new CorrelationRuleFilter { Subject = "order-placed", ApplicationProperties = { ["region"] = "us" } })
            : new CreateRuleOptions("orders", new SqlRuleFilter("region = 'us'"));

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = desired });

        //Assert
        Assert.Equal(desired.Filter, client.Rules[desired.Name].Filter);
        Assert.Equal(desired.Action, client.Rules[desired.Name].Action);
        Assert.Equal(1, client.UpdatedRules);
    }

    [Fact]
    public async Task When_only_the_action_changes_should_update_the_rule()
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        var desired = CreateRule("sql");
        client.Rules.Add(desired.Name, ServiceBusModelFactory.RuleProperties(desired.Name, desired.Filter, new SqlRuleAction("SET matched = 'old'")));

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = desired });

        //Assert
        Assert.Equal(desired.Action, client.Rules[desired.Name].Action);
        Assert.Equal(1, client.UpdatedRules);
    }

    [Theory]
    [InlineData("sql")]
    [InlineData("correlation")]
    public async Task When_the_rule_already_matches_should_not_update_it(string filterType)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        var existing = CreateRule(filterType);
        client.Rules.Add(existing.Name, ServiceBusModelFactory.RuleProperties(existing.Name, existing.Filter, existing.Action));
        var desired = new AzureServiceBusSubscriptionConfiguration { Rule = CreateRule(filterType) };

        //Act
        await ReceiveAsync(client, desired);
        await ReceiveAsync(client, desired);

        //Assert
        Assert.Single(client.Rules);
        Assert.Equal(0, client.CreatedRules);
        Assert.Equal(0, client.UpdatedRules);
    }

    [Theory]
    [InlineData(OnMissingChannel.Assume)]
    [InlineData(OnMissingChannel.Validate)]
    public async Task When_channel_creation_is_disabled_should_not_modify_rules(OnMissingChannel mode)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        client.Rules.Add("$Default", ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter()));

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = CreateRule("sql") }, makeChannels: mode);

        //Assert
        Assert.Equal("$Default", Assert.Single(client.Rules).Key);
        Assert.Equal(0, client.ReadRules);
        Assert.Equal(0, client.CreatedRules);
        Assert.Equal(0, client.UpdatedRules);
        Assert.Equal(0, client.DeletedRules);
    }

    [Fact]
    public async Task When_no_rule_is_configured_should_preserve_existing_rules()
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        client.Rules.Add("external", ServiceBusModelFactory.RuleProperties("external", new FalseRuleFilter()));

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration());

        //Assert
        Assert.Equal("external", Assert.Single(client.Rules).Key);
        Assert.Equal(0, client.ReadRules);
    }

    [Fact]
    public async Task When_no_rule_is_configured_should_create_the_default_on_a_new_subscription()
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient { SubscriptionExists = false };

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration());

        //Assert
        var rule = Assert.Single(client.Rules).Value;
        Assert.Equal("$Default", rule.Name);
        Assert.IsType<TrueRuleFilter>(rule.Filter);
    }

    [Fact]
    public async Task When_the_default_rule_is_explicitly_configured_should_update_without_deleting_it()
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient();
        client.Rules.Add("$Default", ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter()));
        var rule = new CreateRuleOptions("$Default", new SqlRuleFilter("region = 'eu'"));

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = rule });

        //Assert
        Assert.Equal(rule.Filter, Assert.Single(client.Rules).Value.Filter);
        Assert.Equal(0, client.DeletedRules);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task When_reconciling_a_rule_fails_should_preserve_the_default_and_allow_retry(string operation)
    {
        //Arrange
        var failure = new ServiceBusException("Cannot reconcile rule.", ServiceBusFailureReason.ServiceCommunicationProblem);
        await using var client = new InMemoryServiceBusRuleClient
        {
            CreateRuleException = operation == "create" ? failure : null,
            UpdateRuleException = operation == "update" ? failure : null,
            DeleteRuleException = operation == "delete" ? failure : null
        };
        client.Rules.Add("$Default", ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter()));
        if (operation == "update")
        {
            client.Rules.Add("orders", ServiceBusModelFactory.RuleProperties("orders", new FalseRuleFilter()));
        }

        var configuration = new AzureServiceBusSubscriptionConfiguration { Rule = CreateRule("sql") };
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("rules"), channelName: new ChannelName("rules"),
            routingKey: new RoutingKey("orders"), subscriptionConfiguration: configuration);
        await using var consumer = new AzureServiceBusConsumerFactory(client, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateAsync(subscription);

        //Act / Assert
        var error = await Assert.ThrowsAsync<ChannelFailureException>(() => consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1)));
        Assert.Same(failure, error.InnerException);
        Assert.True(client.Rules.ContainsKey("$Default"));
        client.CreateRuleException = null;
        client.UpdateRuleException = null;
        client.DeleteRuleException = null;
        await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1));
        var rule = Assert.Single(client.Rules).Value;
        Assert.Equal("orders", rule.Name);
        Assert.Equal(configuration.Rule.Filter, rule.Filter);
        Assert.Equal(configuration.Rule.Action, rule.Action);
    }

    [Fact]
    public void When_both_rule_options_are_set_should_fail_during_subscription_construction()
    {
        //Arrange
        var configuration = new AzureServiceBusSubscriptionConfiguration { Rule = CreateRule("sql"), SqlFilter = "1=1" };

        //Act / Assert
        Assert.Throws<ConfigurationException>(() => new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("rules"), channelName: new ChannelName("rules"),
            routingKey: new RoutingKey("orders"), subscriptionConfiguration: configuration));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_another_consumer_creates_the_entity_should_still_apply_the_configured_rule(bool subscriptionRace)
    {
        //Arrange
        await using var client = new InMemoryServiceBusRuleClient
        {
            SubscriptionExists = !subscriptionRace,
            SubscriptionCreatedByAnotherClient = subscriptionRace,
            RuleCreatedByAnotherClient = !subscriptionRace
        };
        client.Rules.Add("$Default", ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter()));
        var desired = CreateRule("sql");

        //Act
        await ReceiveAsync(client, new AzureServiceBusSubscriptionConfiguration { Rule = desired });

        //Assert
        var rule = Assert.Single(client.Rules).Value;
        Assert.Equal(desired.Name, rule.Name);
        Assert.Equal(desired.Filter, rule.Filter);
        Assert.Equal(desired.Action, rule.Action);
    }

    private static CreateRuleOptions CreateRule(string filterType)
    {
        RuleFilter filter = filterType switch
        {
            "sql" => new SqlRuleFilter("region = 'eu'"),
            "correlation" => new CorrelationRuleFilter { Subject = "order-placed", ApplicationProperties = { ["region"] = "eu" } },
            "true" => new TrueRuleFilter(),
            "false" => new FalseRuleFilter(),
            _ => throw new ArgumentOutOfRangeException(nameof(filterType))
        };
        return new CreateRuleOptions("orders", filter) { Action = new SqlRuleAction("SET matched = 'yes'") };
    }

    private static async Task ReceiveAsync(InMemoryServiceBusRuleClient client,
        AzureServiceBusSubscriptionConfiguration configuration, bool isAsync = true,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("rules"), channelName: new ChannelName("rules"),
            routingKey: new RoutingKey("orders"), makeChannels: makeChannels, subscriptionConfiguration: configuration);
        var factory = new AzureServiceBusConsumerFactory(client, loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        if (isAsync)
        {
            await using var consumer = factory.CreateAsync(subscription);
            await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1));
        }
        else
        {
            using var consumer = factory.Create(subscription);
            consumer.Receive(TimeSpan.FromMilliseconds(1));
        }
    }
}
