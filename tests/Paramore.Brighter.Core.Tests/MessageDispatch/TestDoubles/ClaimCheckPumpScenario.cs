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
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public sealed class ClaimCheckPumpScenario : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IDisposable _transformerFactory;
    private readonly IAmAMessagePump _pump;

    public ClaimCheckPumpScenario(bool useAsync, bool retain = false, bool dataRefOnly = false)
    {
        using var payload = new MemoryStream(Encoding.UTF8.GetBytes(Payload));
        ClaimId = Storage.Store(payload);
        ClaimBody = $"Claim Check {ClaimId}";
        Message = new Message(
            new MessageHeader(Id.Random(), Channel.RoutingKey, MessageType.MT_COMMAND,
                source: new Uri("https://example.test/claim-source"),
                type: new CloudEventsType("claim.delivery"),
                timeStamp: new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
                correlationId: new Id("claim-correlation"),
                contentType: new ContentType("text/plain; charset=utf-8"),
                replyTo: new RoutingKey("claim-reply"),
                handledCount: 3,
                delayed: TimeSpan.FromSeconds(7),
                partitionKey: new PartitionKey("claim-partition"),
                dataSchema: new Uri("https://example.test/claim-schema"),
                subject: "claim-subject",
                workflowId: new Id("claim-workflow"),
                jobId: new Id("claim-job"),
                traceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
                traceState: "vendor=value",
                baggage: Baggage.FromString("tenant=sample")),
            new MessageBody(ClaimBody))
        {
            Persist = true
        };
        Message.Header.DataRef = ClaimId;
        if (!dataRefOnly)
            Message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = ClaimId;
        Message.Header.Bag["transport-lock"] = "lock-to-preserve";

        Channel.OnRequeue = message =>
        {
            RequeuedBody = message.Body.Value;
            RequeuedMessage = message;
            LuggageAtRequeue = Storage.HasClaim(ClaimId);
        };
        Channel.OnAcknowledge = _ => LuggageAtAcknowledge.Add(Storage.HasClaim(ClaimId));

        var services = new ServiceCollection();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { TransformerLifetime = ServiceLifetime.Scoped });
        services.AddScoped(_ =>
        {
            var storage = new InMemoryScopedClaimCheckStorage(Storage);
            ScopedStores.Add(storage);
            return storage;
        });
        services.AddScoped<IAmAStorageProvider>(provider => provider.GetRequiredService<InMemoryScopedClaimCheckStorage>());
        services.AddScoped<IAmAStorageProviderAsync>(provider => provider.GetRequiredService<InMemoryScopedClaimCheckStorage>());
        services.AddScoped<ClaimCheckTransformer>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        if (retain)
        {
            (_pump, _transformerFactory) =
                Build<RetainedClaimCheckDeliveryCommand, RetainedClaimCheckDeliveryMapper>(
                    useAsync, new RetainedClaimCheckDeliveryMapper(this));
        }
        else
        {
            (_pump, _transformerFactory) =
                Build<ClaimCheckDeliveryCommand, ClaimCheckDeliveryMapper>(useAsync, new ClaimCheckDeliveryMapper(this));
        }

        Channel.Enqueue(Message);
    }

    public string Payload { get; } = new('x', 300 * 1024);
    public InMemoryStorageProvider Storage { get; } = new();
    public InMemoryClaimCheckChannel Channel { get; } = new();
    public List<InMemoryScopedClaimCheckStorage> ScopedStores { get; } = [];
    public List<string> HandledPayloads { get; } = [];
    public List<bool> LuggageAtAcknowledge { get; } = [];
    public Func<Exception?>? HandlerFailure { get; set; }
    public bool MappingFailure { get; set; }
    public string ClaimId { get; }
    public string ClaimBody { get; }
    public Message Message { get; }
    public Message? MappedMessage { get; set; }
    public Message? RequeuedMessage { get; private set; }
    public string? RequeuedBody { get; private set; }
    public bool? LuggageAtRequeue { get; private set; }

    public void Run() => _pump.Run();

    public void Handle(ClaimCheckDeliveryCommand request)
    {
        HandledPayloads.Add(request.Payload);
        var failure = HandlerFailure?.Invoke();
        if (failure is not null)
            throw failure;
    }

    private (IAmAMessagePump, IDisposable) Build<TRequest, TMapper>(bool useAsync, TMapper mapper)
        where TRequest : ClaimCheckDeliveryCommand
        where TMapper : class, IAmAMessageMapper<TRequest>, IAmAMessageMapperAsync<TRequest>
    {
        var subscribers = new SubscriberRegistry();
        var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => mapper),
            new SimpleMessageMapperFactoryAsync(_ => mapper));
        mappers.Register<TRequest, TMapper>();
        mappers.RegisterAsync<TRequest, TMapper>();

        IAmAHandlerFactory handlers;
        if (useAsync)
        {
            subscribers.RegisterAsync<TRequest, ClaimCheckDeliveryHandlerAsync<TRequest>>();
            handlers = new SimpleHandlerFactoryAsync(_ => new ClaimCheckDeliveryHandlerAsync<TRequest>(this));
        }
        else
        {
            subscribers.Register<TRequest, ClaimCheckDeliveryHandler<TRequest>>();
            handlers = new SimpleHandlerFactorySync(_ => new ClaimCheckDeliveryHandler<TRequest>(this));
        }

        var processor = new CommandProcessor(subscribers, handlers, new InMemoryRequestContextFactory(),
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        if (useAsync)
        {
            var transforms = new ServiceProviderTransformerFactoryAsync(_services);
            var pump = new ServiceActivator.Proactor(processor, _ => typeof(TRequest), mappers, transforms,
                new InMemoryRequestContextFactory(), Channel) { DontAckDelay = TimeSpan.Zero, RequeueCount = 10 };
            return (pump, transforms);
        }
        else
        {
            var transforms = new ServiceProviderTransformerFactory(_services);
            var pump = new ServiceActivator.Reactor(processor, _ => typeof(TRequest), mappers, transforms,
                new InMemoryRequestContextFactory(), Channel) { DontAckDelay = TimeSpan.Zero, RequeueCount = 10 };
            return (pump, transforms);
        }
    }

    public void Dispose()
    {
        _transformerFactory.Dispose();
        _services.Dispose();
    }
}
