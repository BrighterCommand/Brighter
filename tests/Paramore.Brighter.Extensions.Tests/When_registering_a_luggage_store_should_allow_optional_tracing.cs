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


namespace Paramore.Brighter.Extensions.Tests;

public class OptionalClaimCheckTracingTests
{
    [Test]
    [Arguments("type", false, false)]
    [Arguments("type", true, false)]
    [Arguments("instance", false, false)]
    [Arguments("instance", true, false)]
    [Arguments("factory", false, false)]
    [Arguments("factory", true, false)]
    [Arguments("type", false, true)]
    [Arguments("type", true, true)]
    [Arguments("instance", false, true)]
    [Arguments("instance", true, true)]
    [Arguments("factory", false, true)]
    [Arguments("factory", true, true)]
    public async System.Threading.Tasks.Task When_registering_a_luggage_store_should_allow_optional_tracing(
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
        await Assert.That(first).IsSameReferenceAs(concrete);
        await Assert.That(concrete.Tracer).IsSameReferenceAs(tracer);
        await Assert.That(provider.GetRequiredService<IAmAStorageProvider>()).IsSameReferenceAs(concrete);
        await Assert.That(provider.GetRequiredService<IAmAStorageProviderAsync>()).IsSameReferenceAs(concrete);
        await Assert.That(concrete.Tracer).IsSameReferenceAs(tracer);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
                InstrumentationOptions.None).Create())
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
        var message = await Assert.That(bus.Stream(topic)).HasSingleItem();
        await Assert.That(string.IsNullOrEmpty(message.Header.DataRef)).IsFalse();
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {message.Header.DataRef}");
        await Assert.That(provider.GetService<IAmABrighterTracer>()).IsNull();

        var store = provider.GetRequiredService<InMemoryStorageProvider>();
        await Assert.That(store.Tracer).IsNull();

        using var storedPayload = postAsync
            ? await store.RetrieveAsync(message.Header.DataRef!)
            : store.Retrieve(message.Header.DataRef!);
        using var reader = new StreamReader(storedPayload);

        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo(request.Text);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_no_luggage_store_is_configured_without_tracing_should_report_the_missing_store(bool resolveAsync)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();

        //Act
        var exception = await Assert.That(() =>
        {
            if (resolveAsync)
                provider.GetRequiredService<IAmAStorageProviderAsync>();
            else
                provider.GetRequiredService<IAmAStorageProvider>();
        }).ThrowsExactly<NotImplementedException>();

        //Assert
        await Assert.That(exception.Message).Contains("register a real store");
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
