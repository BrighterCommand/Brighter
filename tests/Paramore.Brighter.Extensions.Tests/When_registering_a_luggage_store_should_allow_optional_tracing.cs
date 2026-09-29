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

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class OptionalClaimCheckTracingTests
{
    [Theory]
    [InlineData("type", false, false)]
    [InlineData("type", true, false)]
    [InlineData("instance", false, false)]
    [InlineData("instance", true, false)]
    [InlineData("factory", false, false)]
    [InlineData("factory", true, false)]
    [InlineData("type", false, true)]
    [InlineData("type", true, true)]
    [InlineData("instance", false, true)]
    [InlineData("instance", true, true)]
    [InlineData("factory", false, true)]
    [InlineData("factory", true, true)]
    public void When_registering_a_luggage_store_should_allow_optional_tracing(
        string registration, bool resolveAsyncFirst, bool registerTracer)
    {
        //Arrange
        var services = new ServiceCollection();
        using var tracer = registerTracer ? new BrighterTracer() : null;

        if (tracer is not null)
            services.AddSingleton<IAmABrighterTracer>(tracer);

        RegisterStore(services.AddBrighter(), registration);
        using var provider = services.BuildServiceProvider();

        //Act
        object first = resolveAsyncFirst
            ? provider.GetRequiredService<IAmAStorageProviderAsync>()
            : provider.GetRequiredService<IAmAStorageProvider>();

        //Assert
        var concrete = provider.GetRequiredService<InMemoryStorageProvider>();
        Assert.Same(concrete, first);
        Assert.Same(tracer, concrete.Tracer);
        Assert.Same(concrete, provider.GetRequiredService<IAmAStorageProvider>());
        Assert.Same(concrete, provider.GetRequiredService<IAmAStorageProviderAsync>());
        Assert.Same(tracer, concrete.Tracer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_posting_a_claim_checked_message_without_tracing_should_store_the_payload(bool postAsync)
    {
        //Arrange
        var bus = new InternalBus();
        var topic = new RoutingKey("optional-tracing.claim-check");
        var services = new ServiceCollection();
        services.AddLogging();
        // Brighter adopts this factory globally; its lifetime must outlast the test container.
        services.AddSingleton<ILoggerFactory>(Initializer.Factory);
        services.AddBrighter()
            .AddProducers(options => options.ProducerRegistry = new InMemoryProducerRegistryFactory(bus,
                [new Publication { Topic = topic, RequestType = typeof(OptionalTracingClaimCheckEvent) }],
Initializer.Factory,                InstrumentationOptions.None).Create())
            .UseExternalLuggageStore<InMemoryStorageProvider>();
        await using var provider = services.BuildServiceProvider();

        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var request = new OptionalTracingClaimCheckEvent { Text = new string('x', 4096) };

        //Act
        if (postAsync)
            await processor.PostAsync(request);
        else
            processor.Post(request);

        //Assert
        var message = Assert.Single(bus.Stream(topic));
        Assert.False(string.IsNullOrEmpty(message.Header.DataRef));
        Assert.Equal($"Claim Check {message.Header.DataRef}", message.Body.Value);
        Assert.Null(provider.GetService<IAmABrighterTracer>());

        var store = provider.GetRequiredService<InMemoryStorageProvider>();
        Assert.Null(store.Tracer);

        using var storedPayload = postAsync
            ? await store.RetrieveAsync(message.Header.DataRef!)
            : store.Retrieve(message.Header.DataRef!);
        using var reader = new StreamReader(storedPayload);

        Assert.Equal(request.Text, await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_no_luggage_store_is_configured_without_tracing_should_report_the_missing_store(bool resolveAsync)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();

        //Act
        var exception = Assert.Throws<NotImplementedException>(() =>
        {
            if (resolveAsync)
                provider.GetRequiredService<IAmAStorageProviderAsync>();
            else
                provider.GetRequiredService<IAmAStorageProvider>();
        });

        //Assert
        Assert.Contains("register a real store", exception.Message);
    }

    private static IBrighterBuilder RegisterStore(IBrighterBuilder builder, string registration) =>
        registration switch
        {
            "type" => builder.UseExternalLuggageStore<InMemoryStorageProvider>(),
            "instance" => builder.UseExternalLuggageStore(new InMemoryStorageProvider()),
            "factory" => builder.UseExternalLuggageStore<InMemoryStorageProvider>(_ => new InMemoryStorageProvider()),
            _ => throw new ArgumentOutOfRangeException(nameof(registration))
        };
}
