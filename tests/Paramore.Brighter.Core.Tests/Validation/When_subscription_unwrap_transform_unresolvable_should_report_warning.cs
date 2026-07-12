#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Transforms.Transformers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class UnwrapTransformResolvableTests
{
    private static MessageMapperRegistry RegistryWith<TMapper>() where TMapper : class, IAmAMessageMapper<MyDescribableCommand>
    {
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        registry.Register<MyDescribableCommand, TMapper>();
        TransformPipelineBuilder.ClearPipelineCache();
        return registry;
    }

    private static Subscription SubscriptionFor<TRequest>(string name) =>
        new(
            subscriptionName: new SubscriptionName(name),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test.routing.key"),
            requestType: typeof(TRequest),
            messagePumpType: MessagePumpType.Reactor);

    [Fact]
    public void When_subscription_unwrap_transform_unresolvable_should_report_warning()
    {
        // Arrange — mapper declares an unwrap transform (MyDescribableTransform) the probe cannot resolve
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert — a single Warning naming the request type, transformer type, subscription Name, and AutoFromAssemblies
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("greeting-subscription", results[0].Error!.Source);
        Assert.Contains(nameof(MyDescribableCommand), results[0].Error!.Message);
        Assert.Contains(nameof(MyDescribableTransform), results[0].Error!.Message);
        Assert.Contains("greeting-subscription", results[0].Error!.Message);
        Assert.Contains("AutoFromAssemblies", results[0].Error!.Message);
    }

    [Fact]
    public void When_subscription_unwrap_transforms_all_resolvable_should_report_no_warning()
    {
        // Arrange — same mapper, but the probe resolves every transformer
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesEverything);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_subscription_mapper_declares_no_transforms_should_report_no_warning()
    {
        // Arrange — vanilla mapper declares no unwrap transforms
        var registry = RegistryWith<MyVanillaDescribableCommandMessageMapper>();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_subscription_request_type_is_null_should_be_skipped()
    {
        // Arrange — a datatype-channel subscription has a null RequestType and cannot be inspected, so it is skipped
        var registry = RegistryWith<MyDescribableCommandMessageMapper>();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("datatype-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test.routing.key"),
            getRequestType: _ => typeof(MyDescribableCommand),
            messagePumpType: MessagePumpType.Reactor);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_subscription_request_type_has_no_mapper_should_report_no_warning()
    {
        // Arrange — no mapper (and no default) resolves for the request type, so there is nothing to inspect
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!));
        TransformPipelineBuilder.ClearPipelineCache();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_subscription_has_resolvable_and_unresolvable_unwrap_transforms_should_report_one_warning()
    {
        // Arrange — mapper declares two unwrap transforms; the probe resolves one (MyDescribableTransform)
        // but not the other (CompressPayloadTransformer)
        var registry = RegistryWith<MyTwoUnwrapDescribableCommandMessageMapper>();
        var probe = new StubTransformerResolvabilityProbe(t => t != typeof(CompressPayloadTransformer));
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, probe);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert — exactly one Warning, for the unresolvable transform only (resolvable one is not reported)
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Contains(nameof(CompressPayloadTransformer), results[0].Error!.Message);
        Assert.DoesNotContain(nameof(MyDescribableTransform), results[0].Error!.Message);
    }

    [Fact]
    public void When_subscription_resolves_to_default_mapper_should_report_no_warning()
    {
        // Arrange — no custom mapper, so the request type resolves to the default JsonMessageMapper, which
        // declares no unwrap transform — so a subscription on the default mapper produces no warning.
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            typeof(JsonMessageMapper<>),
            typeof(JsonMessageMapper<>));
        TransformPipelineBuilder.ClearPipelineCache();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_subscription_has_only_an_async_custom_mapper_with_a_default_present_should_still_report_warning()
    {
        // Arrange — a default mapper IS configured (as AddBrighter does), but the request type has only a
        // custom async mapper declaring an unresolvable unwrap transform. The sync side falls back to the
        // default; the custom async mapper's transform must still be evaluated — the default-mapper guard must
        // not mask a real custom mapper's transforms (FR-5).
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => null!),
            new SimpleMessageMapperFactoryAsync(_ => null!),
            typeof(JsonMessageMapper<>),
            typeof(JsonMessageMapper<>));
        registry.RegisterAsync<MyDescribableCommand, MyDescribableCommandMessageMapperAsync>();
        TransformPipelineBuilder.ClearPipelineCache();
        var spec = ConsumerValidationRules.UnwrapTransformResolvable(registry, StubTransformerResolvabilityProbe.ResolvesNothing);
        var subscription = SubscriptionFor<MyDescribableCommand>("greeting-subscription");

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var results = spec.Accept(new ValidationResultCollector<Subscription>()).ToList();

        // Assert — the custom async mapper's unwrap transform is still reported
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Contains(nameof(MyDescribableTransform), results[0].Error!.Message);
    }
}
