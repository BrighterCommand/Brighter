# 10. Brighter OpenTelemetry Semantic Conventions 

Date: 2024-04-29

## Status

Accepted

## Context

Up to this point our approach to OTel has tended to use custom information over the standards described here. We need to review our approach to OTel and ensure that we are working with these standards.

### Semantic Conventions

There are some public semantic conventions of interest for attributes to set on a span when working with OTel and messaging systems:

* [Semantic Convetions for Messaging Systems](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/README.md)
* [Semantic Conventions for Cloud Events](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/cloudevents/README.md)
* [Trace Context](https://w3c.github.io/trace-context/)

Conventions provide a standard way to describe a trace and to propogate context between services: the Semantic Conventions for Messaging Systems provide a common way to describe spans and attributes in messaging systems; the W3C  Trace Context standard provides a common way to propogate context information between services; the Cloud Events Semantic Conventions uses the W3C Trace Context standard to propogate their context and add additional attributes to the Messaging approach.

The [worked example](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/MicroserviceExample) for RabbitMQ is a starting point for understanding OTel in a messaging context within .NET. 
                     
### Semantic Conventions for Brighter

We would want to define semantic conventions for Brighter itself. 

Brighter can operate both as a Command Processor and Dispatcher. 

When Brighter operates operates as a Command Processor, the span would naturally be the point at which the command processor is invoked:

* We would assume that the span would be named for the type of request.
  * Attributes for the span would include request level metadata.
  * Entering a handler, either middleware or sink, would be an event on the span. 
  * A send is a span which covers the entire handler pipeline for a command.
  * A publish is span which has links to child spans for each handler pipeline subscribed to the event.
  * A deposit is a span which covers the entire outgoing transform and message mapper pipeline for a request. 
    * It would create a child span to cover the Outbox database operation.
    * With a transform that calls out-of-process, such as to a schema registry or object storage, it would create a child span to cover the external call.
  * A clear is a span which covers producing a message to a messaging system. 
      * It would create a child span to cover the Outbox database operation.
      * Invoking the messaging transport would be a child span. 

When Brighter operates as a Dispatcher, each individual Performer would tend to create a span which would begin where we receive a message. 

* Because processing a message triggers the Command Processor this would create a child span of the Performer's receive span, as described above.
* The Performer also triggers message translation of an incoming request via a transform pipeline and a message mapper. Because the Command Processor is a child span, message translation would also likely be a child of the Performer's receive span; as well and a sibling of the Command Processor span.
  * With a transform that calls out-of-process, such as to a schema registry or object storage, it would create a child span to cover the external call.
  * *For example, a call to check an Inbox would create a child span for the database operation*

### Controlling the Number of Attributes

We should make it possible to set the types of attributes that users of the framework want from the library and provide options for them to control this. See [this blog](https://www.jimmybogard.com/building-end-to-end-diagnostics-activitysource-and-open/) for an example.

## Decision

### Tracer

Brighter should define its own Tracer, via the Activity Source class in .NET (see [this](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) note). The name of the source should be be:

source name: `paramore.brighter` 

We would also add the `<version number>` to the Activity Source.

We do not want to initialize this for both usage as a Command Processor and a Dispatcher implying that the source needs to be created by a stand-alone static class. 

The approach outlined here forms a useful design for the usage of [Activity Source](https://www.jimmybogard.com/building-end-to-end-diagnostics-activitysource-and-open/) in .NET:

* We should create a static class that initializes the Activity Source.
* We should create a settings class that allows the user to control the attributes that we add to the span.
* We should create an Enricher that adds the attributes to the span, observing the options set by the user.
* We should provide a helper class to register the source and ensure no typos in source name cause us trivial issues.

### Command Processor Spans

We will create a span for each command processed. The span will be named as follows:

span name: `<request type> <operation>`

* The `<request type>` is the full type name of the request.
* The `<operation>` the command processor operation being performed.

The span will usually have a parent. This is because the command processor is usually invoked by an ASP.NET Controller or by Brighter's Service Activator Performer (or Message Pump). The parent span will be the span for the ASP.NET Controller or the Service Activator Performer.  

In addition, a common pattern is for a Send operation to be followed by a Publish, Deposit or Clear operation invoked from within the Sink handler. In this case the Publish, Deposit or Clear operation will be a child span of the Send operation span.

The span kind will always be Internal. This is because the command processor is an internal component of the application. It does not need to be set.

#### Command Processor Operations

| Operation | Description                                                                         |
|-----------|-------------------------------------------------------------------------------------|
| `send`    | A command is routed to a single handler.                                            |
| `publish` | An event is routed to multiple handlers.                                            |
| `deposit` | A request is transformed into a message and stored in an Outbox                      |
| `clear`     | Requests in the Outbox are dispatched to a messaging broker via a messaging gateway |

Note that we Publish, Deposit and Clear may be batch operations which result in multiple invocations of our pipeline. In a batch we will create a parent `create` span (itself probably a child of another span that triggered it) and add each item within the batch as an activity via an activity link on the parent span. 

#### Deposit Operation Spans, External Call Spans

During a Deposit the Command Processor transforms a request into a message via a transform and mapper pipeline. It then stores the message in an Outbox.

The Command Processor span for a Deposit covers the entire transform and mapper pipeline; however child spans are created for any external calls in that pipeline. *For example, if the transform uses object storage or s achema registry, a child span is created for the external call.*

Storing the message in an Outbox should create a span, with low-cardinality (i.e. name of Outbox or Inbox operation) as per the [Otel specification](https://opentelemetry.io/docs/specs/semconv/database/database-spans/).

A transformer is middleware used in the message mapping pipeline that turns a request into a message. A transformer may call externally, for example to object storage or a schema registry. These external calls should create a new span that has the Deposit Operation Span as a parent.

In some cases semantic conventions will exist for these external calls. *For example see object storage: See the [OTel documentation for S3]*

#### Clear Operation Spans, Publish and Create Spans

During a Clear we retrieve a message from the Db, and then produce a message. There are existing conventions around producing and clearing.

Because the CommandProcessor performs both these operations, and both involve a span that has it's own semantics, they need to be the child of a CommandProcessor span which spawns them. This span is named

* `clear`

We don't know the `channel` so we cannot provide more detail in the name

When we Clear we always use a batch, so we may well be looping over a number of clear operations, each of which forms part of a batch, so this implies that each `clear` span is the child of another span

* `create`

Both the `create` span and the `clear` span our internal.

During a Clear the Command Processor acts as a Producer. There are existing [Messaging](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/) semantic conventions for a Producer.

We should create a span for producing a message, that is a child of the Command Processor `clear` span. The span is named:

* `<destination name>` `<operation name>`

where the destination name is the name of the channel and the operation name is the operation being performed. The span kind is producer.

* Create Message => span name: `<channel> create` span kind: producer
* Publish Message => span name: `<channel> publish` span kind: producer

Producing a message is a `publish` operation, unless the operation is within a Batch in which case the batch is a `publish` with each message in the batch a `create` span.

Kind of the span is `producer` for the creator of the message. So a `publish` span is a `producer` span for a single message but a `client` for a batch, with the `create` being the `producer` in that case. 

[Cloud Events](https://opentelemetry.io/docs/specs/semconv/cloudevents/cloudevents-spans/#attributes) offers alternative names the producer and consumer spans:

* Create a message => span name: `Cloud Events Create <event type>` span kind: producer

In our case we will use the messaging semantic conventions; the Cloud Events standard identifier is irrelevant in a span name.

The span kind will be Producer instead of Internal at this point.

When we Clear we read a message from the Outbox. Once we have dispatched the message, we update the Outbox to mark it as sent. Reading and writing the message to and from the Outbox should create a span, with low-cardinality (i.e. name of Outbox or Inbox operation) as per the [Otel specification](https://opentelemetry.io/docs/specs/semconv/database/database-spans/).

### Command Processor Attributes

We record the following attributes on a Command Processor span:

| Attribute                       | Type | Description                                                        | Example                                     |
|---------------------------------| --- |--------------------------------------------------------------------|---------------------------------------------|
| `paramore.brighter.requestid`     | string | In a non-batch operation this is the request id                    | "1234-5678-9012-3456"                       |
| `paramore.brighter.requestids`    | string | In a batch operation this is a comma separated list of request ids | "1234-5678-9012-3456, 2345-6789-0123-4567"  |
| `paramore.brighter.requesttype`   | string | The full type name of the command                                  | "MyNamespace.MyCommand"                     |
| `paramore.brighter.request_body`  | string | The contents of the request as JSON                                | "{"greeting": "Hello World"}"                |
| `paramore.brighter.operation`     | string | The operation being performed                                      | "send"                                      |
| `paramore.brighter.spancontext.*` | varies | User supplied attributes for the span via the request context bag  | paramore.brighter.spancontext.userid "1234" |

Because we allow you to inject RequestContext on a call to the Command Processor you can use this to add additional attributes to the span. Any RequestContext.Bag entries that start with "paramore.brighter.spancontext." will be added to the span as attributes. Baggage is an alternative here, but we won't automatically add baggage as attributes to your span. 

We should check Activity.IsAllDataRequested and only add the attributes if it is. We should enable granular control of which attributes if all data is requested. This is because adding attributes to a span can be expensive, see [this doc](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/README.md). Likely options we would need:

* `RequestInformation` (`.requestid`, `.requestids`, `.requesttype`, `.operation`) => what is the request?
* `RequestBody` (`.request_body`) => what is the request body?
* `RequestContext` (`.spancontext.*`) => what is the context of the request?

##### External Call Span Attributes

In many cases semantic conventions will define attributes for these spans. For example: 

* Object Storage: See the [OTel documentation for S3](https://opentelemetry.io/docs/specs/semconv/object-stores/s3/)
* Database Calls: See the [OTel documentation for Database](https://opentelemetry.io/docs/specs/semconv/database/)
* HTTP Calls: See the [OTel documentation for HTTP](https://opentelemetry.io/docs/specs/semconv/http/)

##### Publish/Create Span Attributes

A Clear operation results in Publish or Create span for a message being sent which will have additional attributes. The Semantic Convention for Messaging Systems provides a common set of attributes for messaging. There are both Required and Recommended attributes. We should always set the Required attributes and offer the Recommended attributes. We should make it possible to control the amount of attributes we set by only setting the Recommended attributes if the user has requested them.

The Command Processor will need changes to the code to support asking the Producer for some of the attributes to set on the span. For example:

* `messaging.system`: what broker are we using?

Other attributes are available in Brighter today:

* `messaging.destination`: what is the name of the channel?
* `messaging.message_id`: what is the message id?
* `messaging.destination.partition.id`: what is the partition id?
* `messaging.message.body.size`: what is the size of the message payload?

We may also wish to make the payload available (although it is not part of the Semantic Conventions).

* `messaging.messagebody`: what is the message payload?
* `messaging.messageheaders`: what are the message headers?

We should check Activity.IsAllDataRequested and only add the attributes if it is. Likely options we would need:

* `MessageInformation` (`message.*`) => what is the message?
* `MessageBody` => (`message.body`)what is the message body?
* `MessageHeaders` => (`message.headers`) what is the metadata of the message?

[Cloud Events](https://opentelemetry.io/docs/specs/semconv/cloudevents/cloudevents-spans/#attributes) also provides attributes for a producer span. This gives us multiple attributes for describing some properties of a message. The id of a message is given by both *messaging.message_id* and *cloudevents.event_id*. As a generic framework, we opt to support both messaging and cloud events conventions for attribute names.

This means that we will need to provide options to allow users to choose the attributes they wish to propogate:

* `UseMessagingSemanticConventionsAttributes`
* `UseCloudEventsConventionsAttributes`

### Command Processor Events

We record an event for every handler we enter. The event is named after the handler. The event has the following attributes:

| Attribute                     | Type | Description | Example|
|-------------------------------| --- | --- | --- |
| `paramore.brighter.handlername` | string | The full type name of the handler | "MyNamespace.MyHandler" |
| `paramore.brighter.handlertype` | string | Is the handler sync or async | "async" |
| `paramore.brighter.is_sink`     | bool | Is this the final operation in the chain | true |

We should record exceptions as events on the span. See the [OTel documentation on Exceptions](https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-spans/)

Standards may exist for the attributes used by events, and we should follow those. *For example we should instrument our Feature Flag handler in compliance with the [OTel documentation on feature flags](https://opentelemetry.io/docs/specs/semconv/feature-flags/feature-flags-spans/)*

### Message Context

Because we may be participating in a distributed trace, we will need to work with `traceparent` and `tracecontext` headers when initializing the span. To do this, we need to use the OpenTelemetry Propogators API to extract and inject across process boundaries, see the [OTel documentation](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md). 

There is an example of this for DotNet and RabbtitMQ in the [DotNet OpenTelemetry Repo](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/MicroserviceExample/Utils/Messaging).

There is an example of this for DotNet and Kafka in the [LightStep Repo](https://github.com/lightstep/kafka-otel-dotnet-example). 

### Performer Spans

The Performer (message pump) acts as a Consumer. There are existing [Messaging](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/) Semantic Conventions for a Consumer.

A Performer should create a span for each message that it processes.

* Receive Message via a pull => span name: `<channel> receive` span kind: `consumer`
* Process Message via a push  => span name: `<channel> process` span kind: `consumer`

Cloud Events offers alternative names the consumer span:

* Receive a message => span name: `Cloud Events Process <event type>` span kind: `consumer`

In our case we will use the messaging semantic conventions; the `Cloud Events` standard identifier is irrelevant in a span name.

We don't create the span until we begin to process the message i.e. not when we read into Brighter's local buffer, but when we retrieve a message from that buffer. This means that the span is created outside of the transport and within the message pump.

We will have to ask the transport for the operation the span is performing:

* `recieve` or `process`: was the message obtained by push or pull?

This is because this will vary by the capabilities of the transport.As this information is static, we can enhance the channel with this information.
  
### Message Attributes

The Semantic Conventions for Messaging Systems provides a common set of attributes for messaging systems. There are both Required and Recommended attributes. We should always set the Required attributes and offer the Recommended attributes. We should make it possible to control the amount of attributes we set by only setting the Recommended attributes if the user has requested them.

A number of attributes will need to be retrieved from the transport, as they are specific to the transport and not available on the message itself:

* `messaging.operation`: an attribute that describes the above (also used in the name)
* `messaging.system`: what broker did we obtain the message from?
* `server.address`: what is the address of that broker

As this information is dynamic the other we should put it into the header bag when reading from the broker and retrieve it from there when creating the span.

We should check Activity.IsAllDataRequested and only add the attributes if it is. Likely options we would need:

* `MessageInformation` (`message.*`) => what is the message?
* `MessageBody` => (`message.body`)what is the message body?
* `MessageHeaders` => (`message.headers`) what is the metadata of the message?
* `ServerInformation` => (`server.*`) what is the server information?

## Consequences

### Standards

Following standards helps us become a participant of wider ecosystem. In particular, adopting standards for how we provide telemetry information from Brighter will make it easier for users to integrate Brighter into user's own observability tools.

### Path Tracing

We have an existing "Trace" operation on a Handler that uses the Visitor pattern to describe the middleware pipeline for a handler.

* This approach has proved useful for diagnostics; we just append the handler name to a string.
* Under OTel, an event should to be recorded on the span in the RequestContext that flows through the pipeline; we would just name the event after the handler.
* Thus it might be possible to retire the Trace operation in favor of the span. Tests that rely on the Trace operation would need to be updated to use the span instead.
