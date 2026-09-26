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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.Validation;

[Trait("Category", "Kafka")]
public class KafkaMissingTopicWarningTests
{
    [Theory]
    [InlineData(GroupProtocol.Consumer, OnMissingChannel.Assume, true)]
    [InlineData(GroupProtocol.Consumer, OnMissingChannel.Validate, false)]
    [InlineData(GroupProtocol.Consumer, OnMissingChannel.Create, false)]
    [InlineData(GroupProtocol.Classic, OnMissingChannel.Assume, false)]
    [InlineData(GroupProtocol.Classic, OnMissingChannel.Validate, false)]
    [InlineData(GroupProtocol.Classic, OnMissingChannel.Create, false)]
    [InlineData(null, OnMissingChannel.Assume, false)]
    [InlineData(null, OnMissingChannel.Validate, false)]
    [InlineData(null, OnMissingChannel.Create, false)]
    public async Task When_consumer_protocol_assumes_a_topic_should_warn_at_startup(
        GroupProtocol? protocol, OnMissingChannel policy, bool expectWarning)
    {
        //Arrange
        using var logContext = TestCorrelator.CreateContext();
        var subscription = new KafkaSubscription(
            new SubscriptionName("orders-subscription"),
            new ChannelName("orders-channel"),
            new RoutingKey("orders-topic"),
            requestType: typeof(Event),
            groupId: "orders-group",
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: policy,
            configHook: _ => throw new InvalidOperationException("Validation must not execute client configuration hooks."))
        {
            GroupProtocol = protocol switch
            {
                GroupProtocol.Consumer => new ConsumerGroupProtocol(),
                GroupProtocol.Classic => new ClassicGroupProtocol(),
                _ => null
            }
        };
        using var provider = CreateProvider(subscription);

        //Act
        var result = provider.GetRequiredService<IAmAPipelineValidator>().Validate();
        foreach (var service in provider.GetServices<IHostedService>())
            await service.StartAsync(CancellationToken.None);

        //Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(policy, subscription.MakeChannels);
        var loggedWarnings = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(entry => entry.Level == LogEventLevel.Warning && entry.RenderMessage().Contains("KIP-848"))
            .ToList();
        if (!expectWarning)
        {
            Assert.Empty(result.Warnings);
            Assert.Empty(loggedWarnings);
            return;
        }

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains("orders-subscription", warning.Source);
        Assert.Contains("orders-topic", warning.Message);
        Assert.Contains("KIP-848", warning.Message);
        Assert.Contains("Assume", warning.Message);
        Assert.Contains("missing topic", warning.Message);
        Assert.Contains("Validate", warning.Message);
        Assert.Contains(warning.Message, Assert.Single(loggedWarnings).RenderMessage());
    }

    [Fact]
    public void When_subscription_is_not_kafka_should_not_warn_about_missing_kafka_topics()
    {
        //Arrange
        var subscription = new Subscription<Event>(makeChannels: OnMissingChannel.Assume);
        using var provider = CreateProvider(subscription);

        //Act
        var result = provider.GetRequiredService<IAmAPipelineValidator>().Validate();

        //Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    private static ServiceProvider CreateProvider(Subscription subscription)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddSerilog(
            new LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger(), dispose: true));
        var registry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(registry);
        var mappers = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, registry, mappers);
        services.AddSingleton<IAmConsumerOptions>(new ConsumersOptions { Subscriptions = [subscription] });
        services.AddSingleton<ISpecification<Subscription>>(
            KafkaConsumerValidationRules.MissingTopicDetection());
        builder.ValidatePipelines(throwOnError: true);
        return services.BuildServiceProvider();
    }
}
