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

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesProducerTransformTests
{
    [Fact]
    public void When_publication_wrap_transform_unresolvable_through_di_should_surface_warning()
    {
        // Arrange — a publication whose resolved mapper (MyDescribableCommandMessageMapper) declares a wrap
        // transform (MyDescribableTransform) that is NOT registered as a transformer, so the probe built from
        // the service collection reports it unresolvable. ValidatePipelines must thread the mapper registry and
        // the probe into the validator for the (A) producer rule to run.
        var routingKey = new RoutingKey("greeting");
        var producer = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = routingKey, RequestType = typeof(MyDescribableCommand) });
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        services.AddSingleton(mapperRegistry);
        // MyDescribableTransform is intentionally NOT registered as a transformer.
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.ValidatePipelines();

        var provider = services.BuildServiceProvider();

        // Act — resolve the validator and run validation through the full DI path
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — an (A) Warning surfaces naming the request, transformer, topic, and AutoFromAssemblies;
        // warnings never block (IsValid stays true)
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w =>
            w.Message.Contains(nameof(MyDescribableCommand))
            && w.Message.Contains(nameof(MyDescribableTransform))
            && w.Message.Contains("greeting")
            && w.Message.Contains("AutoFromAssemblies"));
    }
}
