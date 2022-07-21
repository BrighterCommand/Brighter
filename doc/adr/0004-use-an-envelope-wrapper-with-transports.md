# 2. Use A Claim Check For Large or Sensitive Messages 

Date: 2022-07-18

## Status

Proposed

## Context

Brighter models a Message with two parts: a Message Header and a Message Body. 

The body is a byte array and the headers include both Brighter defined headers and a "bag" that can be used for user-defined headers.

This is an internal format to Brighter and not native to any transport.

To publish we must convert the Brighter Message into the native transport's message format. By convention we call this component {BrokerName}MessagePublisher where {BrokerName} is the name of the transport.

To read a message from the queue we must convert the transport's message into a Brighter Message. By convention we call this component {BrokerName}MessageCreator where {BrokerName} is the name of the transport.

This is by convention only as there is no interface required to be implemented by Brighter.

The interfaces that we ask you to implement are the IAmAMessageConsumer and IAmAMessageProducer (along with interfaces related to a producer registry, subscriptions and publications and gateway configuration).

Without a defined pipeline, we cannot effectively provide middleware that allows for the transformation of a message between producer and gateway or transformation to a Brighter Message between gateway and consumer.

With a defined pipeline we could add middleware that would allow us to intercept message transformation.

Througout Brighter we build a pipeline by using an interface to define the interface that a filter must support and chain them together.

## Decision

The [Envelope Wrapper](https://www.enterpriseintegrationpatterns.com/patterns/messaging/EnvelopeWrapper.html) pattern provides for middleware that transforms the message between source and messaging system or messaging system and recipient. Although the diagram below shows one wrapper, in principle the pattern allows for a pipeline of transformations where, in effect each middleware step provides translation required to pass it over the platform.

![Claim Check](images/Wrapper.gif)

For this reason we suggest as follows:

1: We define an interface IAmAWrapper<T> where T is the message type in the SDK of the transport to be used.

2: We add a method to this interface Wrap that takes a Brighter Message and returns a Message of type T.

3: We provide an abstract base class Wrapper that implements this interface by simply calling the next Wrapper in the chain's Wrap method. It holds as state the next wrapper in the chain.

3: We define an interface IAmAmUnWrapper<T> where T is message type in the SDK of the transport to be used. 

4: We add a method to this interface Unwrap that takes a message of type T and returns a Brighter Message.

5: We define an abstract base class Unwrapper that implements this interface by simply calling the next Wrapper in the chain's UnWrap method. it holds as state the next unwrapper in the chain.

6: We define WrapperBuilder and UnwrapperBuilder that respectively build a chain of wrappers or unwrappers as requested in configuration. \

7: A default wrapper should exist that marshals between Brighter and the target transport - the implementation of our existing {BrokerName}MessagePublisher. 

8: A default wrapper should exist that marshals from the target transport to Brighter.

9: The producer is supplied with the Wrapper pipeline

10: The consumer is supplied with the Unwrapper pipeline.


## Consequences

* It will be possible to insert new steps into the process of marshalling or unmarshalling a message.
* Likely use cases include large message transports and support for CloudEvents.
* But it could also be used for observability around message transformation, encryption etc.

We have built a platform that is extensible, even if most users don't choose to extend it. But we don't provide much extensibility once you choose a transport. This would redress the balance of that by allowing us to use the pipeline to support user modification of the marshalling or unmarshalling of messages.

