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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class DescribePipelinesWithProducersTests
{
    [Fact]
    public void When_describe_pipelines_with_producers_should_log_publication_summary()
    {
        // Arrange — set up a producer with a publication that has a RequestType and Topic
        var routingKey = new RoutingKey("greeting.created");
        var producer = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = routingKey, RequestType = typeof(MyDescribableEvent) });
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        services.AddSingleton(mapperRegistry);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.DescribePipelines();

        // Replace the logger factory so we can capture output
        var spyLogger = new SpyLogger();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(
            new TestLoggerFactory(spyLogger));

        var provider = services.BuildServiceProvider();

        // Act — resolve the diagnostic writer and call Describe
        var writer = provider.GetRequiredService<IAmAPipelineDiagnosticWriter>();
        writer.Describe();

        // Assert — the summary should include publication count
        var infoMessages = spyLogger.InformationEntries.Select(e => e.Message).ToList();
        Assert.Contains(infoMessages, m => m.Contains("1 publication"));
    }

    private class MyDescribableEvent : Event
    {
        public MyDescribableEvent() : base(System.Guid.NewGuid()) { }
    }

    private class TestLoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory
    {
        private readonly SpyLogger _logger;
        public TestLoggerFactory(SpyLogger logger) => _logger = logger;
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => _logger;
        public void AddProvider(Microsoft.Extensions.Logging.ILoggerProvider provider) { }
        public void Dispose() { }
    }
}
