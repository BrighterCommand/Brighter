#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesWithProducersTests
{
    [Fact]
    public void When_validate_pipelines_with_producers_should_detect_missing_request_type()
    {
        // Arrange — set up a producer whose publication has no RequestType
        var routingKey = new RoutingKey("test.validation.topic");
        var producer = new InMemoryMessageProducer(
            new InternalBus(), new Publication { Topic = routingKey });
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        services.AddSingleton(mapperRegistry);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.ValidatePipelines();

        var provider = services.BuildServiceProvider();

        // Act — resolve validator and run validation
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — validation should detect the missing RequestType on the publication
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("RequestType is null"));
    }

    [Fact]
    public void When_validate_pipelines_with_valid_producers_should_pass_producer_checks()
    {
        // Arrange — set up a producer with a valid publication
        var routingKey = new RoutingKey("test.validation.topic");
        var producer = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = routingKey, RequestType = typeof(MyValidationCommand) });
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        services.AddSingleton(mapperRegistry);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.ValidatePipelines();

        var provider = services.BuildServiceProvider();

        // Act
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — no producer validation errors
        Assert.DoesNotContain(result.Errors, e => e.Source.Contains("Publication"));
    }

    private class MyValidationCommand : Command
    {
        public MyValidationCommand() : base(System.Guid.NewGuid()) { }
    }
}
