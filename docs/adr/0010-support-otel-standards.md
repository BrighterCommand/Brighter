# 10. Support OpenTelemetry standards 

Date: 2024-04-28

## Status

Accepted

## Context

Up to this point our approach to OTel has tended to use custom information over the standards described here. We need to review our approach to OTel and ensure that we are working with these standards.

### Semantic Conventions

There are some public semantic conventions of interest for attributes to set on a span when working with OTel and messaging systems:

* [Semantic Convetions for Messaging Systems](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/README.md)
* [Semantic Conventions for Cloud Events](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/cloudevents/README.md)
* [Trace Context](https://w3c.github.io/trace-context/)

The Semantic Conventions for Messaging Systems provides a common way to pass context information between services. The Semantic Conventions for Cloud Events provides a common way to pass context information between services. The W3C  Trace Context standard provides a common way to propogate context information between services. Cloud Events uses the W3C Trace Context standard to propogate it's context.

All of these standards are designed to work together to provide a common way to pass context information between services. For Brighter to participate well in an OTel ecosystem we need to support these standards.  

The [worked example](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/MicroserviceExample) for RabbitMQ is a good starting point. 
                     
#### Semantic Conventions for Brighter

In addition, we would need to think about semantic conventions for Brighter itself. 

Brighter can operate both as a Command Processor and Dispatcher. 

When Brighter operates operates as a Command Processor, the span would naturally be the point at which the command processor is invoked:

* We would assume that the span would be named for the type of request.
* Attributes for the span would include request level metadata, which is well known.
* Entering a handler, either middleware or sink, would be an event on the span. 
* A publish is likely a single span, see the semantic conventions for messaging as an example, with an event for each new pipeline we enter.

When Brighter operates as a Dispatcher, each individual Performer would tend to create a span which would begin where we receive a message. Because processing a message triggers the Command Processor this would likely create a further child span of the Performer's receive span. The translation span would thus likely be a child of the Performer's receive span as well and a sibling of the Command Processor span.
    
#### Controlling the Number of Attributes

We should determine the depth of attributes that users of the framework want from the library and provide options for them to control this. See [this blog](https://www.jimmybogard.com/building-end-to-end-diagnostics-activitysource-and-open/) for an example.

#### Tracing

We have an existing Trace operation on a Handler that uses the Visitor pattern. This proves useful for diagnostics, and just uses the handler name. When the event is likely to be recorded on the span in the RequestContext that flows through the pipeline, it might be possible to retire the Trace operation in favour of the span. Tests that rely on the Trace operation would need to be updated to use the span instead.

## Decision

### Command Processor

#### Spans

We will create a span for each command processed. The span will be named as follows:

*request type operation*

* The <request type> is the full type name of the request.
* The <operation> the command processor operation being performed.

The span will usually have a parent. This is because the command processor is usually invoked by an ASP.NET Controller or by Brighter's Service Activator Performer (or Message Pump). The parent span will be the span for the ASP.NET Controller or the Service Activator Performer.  

In addition, a common pattern is for a Send operation to be followed by a Publish, Deposit or Clear operation invoked from within the Sink handler. In this case the Publish, Deposit or Clear operation will be a child span of the Send operation span.

The span kind will always be Internal. This is because the command processor is an internal component of the application. It does not need to be set.

#### Command Processor Operations

| Operation | Description |
| --- | --- |
| Send | A command is being routed to a single handler. |
| Publish | A command is being routed to multiple handlers. |
| Deposit | A request is being written to the Outbox |
| Clear | Requests in the Outbox are being dispatched via a messaging gateway |

Note that we Publish, Deposit and Clear may be batch operations which result in multiple invocations of our pipeline. In a batch we will create a parent span (itself probably a child of another span that triggered it) and add each item within the batch as an activity via an activity link on the parent span. 

#### Command Processor Attributes

We record the following attributes on a Command Processor span:

| Attribute                         | Type | Description                                                        | Example|
|-----------------------------------| --- |--------------------------------------------------------------------| --- |
| paramore.brighter.requestid       | string | In a non-batch operation this is the request id                    | "1234-5678-9012-3456" |
| paramore.brighter.requestids      | string | In a batch operation this is a comma separated list of request ids | "1234-5678-9012-3456, 2345-6789-0123-4567" |
| paramore.brighter.requesttype     | string | The full type name of the command                                  | "MyNamespace.MyCommand" |
| paramore.brighter.operation       | string | The operation being performed                                      | "Send" |
| paramore.brighter.spancontext.*   | varies | User supplied attributes for the span via the request context bag  | paramore.brighter.spancontext.userid "1234" |
                     
Because we allow you to inject RequestContext on a call to the Command Processor you can use this to add additional attributes to the span. Any RequestContext.Bag entries that start with "paramore.brighter.spancontext." will be added to the span as attributes. Baggage is an alternative here, but we won't automatically add baggage as attributes to your span. 

We should check Activity.IsAllDataRequested and only add the attributes if it is. This is because adding attributes to a span can be expensive. See [this doc](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/README.md).

#### Command Processor Events

We record an event for every handler we enter. The event is named after the handler. The event has the following attributes:

| Attribute | Type | Description | Example|
| --- | --- | --- | --- |
| paramore.brighter.handlername | string | The full type name of the handler | "MyNamespace.MyHandler" |
| paramore.brighter.handlertype | string | Is the handler sync or async | "Async" |
| paramore.brighter.issink | bool | Is this the final operation in the chain | True |

We should record exceptions as events on the span. See the [OTel documentation on Exceptions](https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-spans/)

We should instrument our Feature Flag handler as an event in the span, as per the [OTel documentation on feature flags](https://opentelemetry.io/docs/specs/semconv/feature-flags/feature-flags-spans/)

#### Command Processor Producer

There are existing [Messaging](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/) semantic conventions for a Producer.

The span is named:

* destination name operation name

where the destination name is the name of the channel and the operation name is the operation being performed. The span kind is producer.

* Create Message => span name: "<channel> create" span kind: producer
* Publish Message => span name: "<channel> publish" span kind: producer

The operation is Publish, unless the operation is within a Batch in which case each item is a Create.

The span kind will be Producer instead of Internal at this point.



### Service Activator Performer

The Service Activator Performer creates a span for each message that it processes. We must follow the existing [Messaging](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/) Semantic Conventions for a Consumer.

### Consumer

* Receive Message via a pull => span name: "<channel> receive" span kind: consumer
* Process Message via a push  => span name: "<channel> process" span kind: consumer

The span is not created when read into any cache but only when made available to the consumer.

### Third-Party Infrastructure Calls Dependencies Spans

Where we access a database i.e. Inbox and Outbox, we should instrument as a span, with low-cardinality (i.e. name of Outbox or Inbox operation) as per the [Otel specification](https://opentelemetry.io/docs/specs/semconv/database/database-spans/).

Where our Claim Check accesses the S3 bucket we should record an event as per the [OTel documentation for S3](https://opentelemetry.io/docs/specs/semconv/object-stores/s3/)


## Consequences


