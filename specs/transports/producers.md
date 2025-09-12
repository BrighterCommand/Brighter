# Producers

A producer sends a message via middleware. It may be a [point-to-point](https://www.enterpriseintegrationpatterns.com/patterns/messaging/PointToPointChannel.html) channel, in which case the producer writes directly to the channel, or it may be via a [broker](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessageBroker.html) in which case the message is sent to the broker, which routes it to the correct channel.

## Implementation

You MUST implement the `IAmAMessageProducer` interface and its derived sync interface `IAmAMessageProducerSync` and its derived async interface `IAmAMessageProducerAsync`.

- `IAmAMessageProducer` has the following properties and methods:
  - `Publication` a property to store the publication (see [Publication](./publication.md)).
  - `Span` a property that enables us to set the `Activity` on an `IAmAMessageProducer`to participate in Open Telemetry.
  - `Scheduler` the scheduler that we will use for delayed publication.
- `IAmAMessageProducerSync` and `IAmAMessageProducerAsync` derive from `IAmAMessageProducer` and are used to send messages to middleware.
  - `Send` and `SendAsync` are used to send a message to middleware.
  - `SendWithDelay` and `SendWithDelayAsync` are used to send a message to middleware with a delay. Either the middleware natively supports a delayed message send, or the consumer should use the scheduler to delay sending.

  ### Publisher

  You SHOULD use a publisher to create the message to be sent over middleware. The publisher typically works as follows (pseudocode):

  ```

   create the middleware message
   populate the 


  ```
