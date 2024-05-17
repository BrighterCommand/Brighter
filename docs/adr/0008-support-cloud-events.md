# 8. Support Cloud Events 

Date: 2019-08-01

## Status

Accepted

## Context

Without a standard for metadata, each messaging framework has its own set of metadata fields. This makes it difficult to write code that can work with multiple messaging frameworks. [CloudEvents](https://github.com/cloudevents/spec?tab=readme-ov-file) is a specification for describing event data in a common way. The specification is designed to make it easier to write code that can work with any messaging framework that supports CloudEvents.

We see increasing adoption of CloudEvents as the header standard. We know that the .NET 9 Eventing Framework will use CloudEvents and has listed it as a requirement for the framework. We also know that the Azure Event Grid service supports CloudEvents. Finally, CloudEvents is a CNCF project.

CloudEvents has two options for how metadata is added to a message: structured and binary. The structured mode is a JSON object that is added to the message body. The binary mode is a set of headers that are added to the message. 

## Decision

Given that we want to be interoperable, and often operate in mixed-stack environments, we should support CloudEvents. It is up to the transport to choose between binary and structured, initially though we will support binary adding structured if we see demand or a transport does not have headers.

Because some Cloud Events values are user-defined we will need to allow user-defined values to be passed to the message mapper. Because these user defined values can be determined at design time we will use the Publication to provide the values. This means that the Message Mapper must take the Publication as a parameter. We have two options here: the first is to use DI and register the message mapper with the DI container. The second is to pass the publication directly. As we do not want to have a dependency on a DI framework we will pass the publication directly.

To pass the publication to the mapper, we have to know the publication for the topic that we are sending to. We already have a Producer Registry and it makes sense to add the Publication to that, so that we can look up the Publication used to build the Producer. The Producer Registry is part of the External Bus. The Message Mapper is called by the Command Processor today, not by the External Bus. As we only need a message mapper with an external bus, this dependency can be moved into the external bus, and out of the CommandProcessor.

This will result in the following breaking changes:

* Message Id will change from a GUID to a string
* Correlation Id will change from a GUID to a string
* Our Bag should support our usage of OTel via [CloudEvents attributes](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/distributed-tracing.md) over an explicit property; we should revise how we handle OTel
* Claim Check will start to use DataRef instead of a value in the Header Bag

We will need to add the following CloudEvents properties which have no equivalent in MessageHeader:

* Source: a URI giving the source for the event
* Type: a string giving the type of the event
* DataSchema: a URI giving the schema for the data
* Subject: a string giving the subject of the event
* Time: a string giving the time of the event
* SpecVersion: a string giving the version of the CloudEvents spec
* DataRef: Used with a claim check
* TraceParent: Used for OTel
* TraceState: Used for OTel

Our transports will need to be able to interpret metadata where it comes from cloud events, and where it comes from native headers (see the [AMQP specification for CloudEvents](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/amqp-protocol-binding.md) as an example). In all cases we should try to read native headers first, then CloudEvents headers and overwrite the native headers with the CloudEvents headers if they exist. Our existing option types for reading headers should support this approach. When writing, we should write both native and CloudEvents headers.

In addition we have a number of Brighter metadata fields that are not part of CloudEvents. We will need to decide how to handle these. We could add them as extensions to CloudEvents, or we could add them as custom header values. We will need to decide on a case by case basis, depending on the transport's native headers.

## Consequences
                  
Passing the Publication to the Message Mapper has advantages. Our ability to provide a generic message mapper has previously been constrained by the Message Mapper not having access to key fields on the Publication, such as the topic/routing key that the message is to be sent over. Whilst the objective of this change is not to provide a generic message mapper, it will allow us to provide a more generic message mapper in the future.

Moving the Message Mapper to the External Bus will simplify the constructors of the Command Processor. We have a number of Command Processor constructors and that is mainly caused by whether or not you need an external bus.
