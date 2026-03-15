# Paramore.Brighter.AsyncAPI

`Paramore.Brighter.AsyncAPI` generates an AsyncAPI 3.0 document from your Brighter configuration using the [AsyncAPI .NET SDK](https://github.com/asyncapi/net-sdk).

It can discover messaging contracts from:
- `AddConsumers(...)` subscriptions (receive operations)
- `AddProducers(...)` producer registry publications (send operations)
- Assembly scanning for message types decorated with `PublicationTopicAttribute`

## Install

Add these packages to your application:

```bash
dotnet add package Paramore.Brighter.AsyncAPI
dotnet add package Paramore.Brighter.AsyncAPI.NJsonSchema
```

`Paramore.Brighter.AsyncAPI.NJsonSchema` provides the default `IAmASchemaGenerator` used by `UseAsyncApi()`.

## Quick Start

Register AsyncAPI support when you configure Brighter:

```csharp
using Neuroglia.AsyncApi.v3;
using Paramore.Brighter.AsyncAPI;

services
    .AddConsumers(options =>
    {
        options.Subscriptions = new Subscription[]
        {
            // your subscriptions
        };
    })
    .AddProducers(configure =>
    {
        configure.ProducerRegistry = producerRegistry;
    })
    .UseAsyncApi(opts =>
    {
        opts.Title = "My Service API";
        opts.Version = "1.0.0";
        opts.Description = "AsyncAPI generated from Brighter subscriptions and publications";
        opts.Servers = new Dictionary<string, V3ServerDefinition>
        {
            ["rabbitmq"] = new V3ServerDefinition
            {
                Host = "localhost:5672",
                Protocol = "amqp",
                Description = "Local RabbitMQ broker"
            }
        };
    })
    .AutoFromAssemblies();
```

Generate the document from your host:

```csharp
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.AsyncAPI;

var document = await host.GenerateAsyncApiDocumentAsync("asyncapi.json");
```

This writes both `asyncapi.json` and `asyncapi.yaml` and returns the generated `V3AsyncApiDocument`.

## Configuration Options

`AsyncApiOptions` supports:
- `Title`: document title (default: `Brighter Application`)
- `Version`: document version (default: `1.0.0`)
- `Description`: optional description text
- `Servers`: optional server definitions (`Dictionary<string, V3ServerDefinition>`)
- `AssembliesToScan`: assemblies to scan for `[PublicationTopic]` message types
- `DisableAssemblyScanning`: turn off scanning when you only want DI-driven discovery
- `SupplementalPublications`: add extra `Publication` values not present in `IAmAProducerRegistry`

## Notes

- If `IAmConsumerOptions` is not registered, receive operations are omitted.
- If `IAmAProducerRegistry` is not registered, send operations come only from `SupplementalPublications` or assembly scanning.
- If neither `Paramore.Brighter.AsyncAPI.NJsonSchema` nor a custom `IAmASchemaGenerator` is registered, `UseAsyncApi()` throws an `InvalidOperationException`.
- Document serialization uses the SDK's `IAsyncApiDocumentWriter`, registered automatically by `UseAsyncApi()` via `AddAsyncApiIO()`.

## Sample

See the end-to-end examples at:
- `samples/AsyncAPI/RMQAsyncAPI/Program.cs`
- `samples/AsyncAPI/KafkaAsyncAPI/Program.cs`
