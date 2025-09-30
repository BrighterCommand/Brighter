# Producers

A producer sends a message via middleware. It may be a [point-to-point](https://www.enterpriseintegrationpatterns.com/patterns/messaging/PointToPointChannel.html) channel, in which case the producer writes directly to the channel, or it may be via a [broker](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessageBroker.html) in which case the message is sent to the broker, which routes it to the correct channel.

## Implementation

You MUST implement the `IAmAMessageProducer` interface and its derived sync interface `IAmAMessageProducerSync` and its derived async interface `IAmAMessageProducerAsync`. You SHOULD name the publisher `[XXXXX]MessageProducer` where `[XXXX]` is the name of the middleware or an abbreviation for it. For example we name the RabbitMQ publisher `RMQMessageProducer` and we name the Kafka publisher `KafkaMessageProducer`.

- `IAmAMessageProducer` has the following properties and methods:
  - `Publication` a property to store the publication (see [Publication](./publication.md)).
  - `Span` a property that enables us to set the `Activity` on an `IAmAMessageProducer`to participate in Open Telemetry.
  - `Scheduler` the scheduler that we will use for delayed publication.
- `IAmAMessageProducerSync` and `IAmAMessageProducerAsync` derive from `IAmAMessageProducer` and are used to send messages to middleware. The separated interfaces allows clients to depend on either the sync or async operations. We use the `Async` suffix for async interfaces or methods.
  - `Send` and `SendAsync` are used to send a message to middleware.
  - `SendWithDelay` and `SendWithDelayAsync` are used to send a message to middleware with a delay. Either the middleware natively supports a delayed message send, or the consumer should use the scheduler to delay sending.

  ### Publisher

  You SHOULD use a publisher to create the message to be sent over middleware. You SHOULD name the publisher `[XXXXX]MessagePublisher` where `[XXXX]` is the name of the middleware or an abbreviation for it. For example we name the RabbitMQ publisher `RMQMessagePublisher` and the Kafka publisher `KafkaMessagePublisher`.
  
  The publisher has a `PublishMessage` or `PublishMessageAsync` method, which is implemented as follows:

  ```pseudo

   create the middleware message
   populate the middleware's message headers from the Brighter `Message`'s `Header` property, which is of type `MessageHeader`
   populate the middleware's message headers from the Brighter `Message`'s `Header` property's `Bag` property, which is of type `Dictionary<string, object>` and contains user-defined values
   populate the middleware message's body from the `Brighter` `Message`'s `Body' property. //Depending on the format of the middleware's message you may need to access this as `Bytes()` or just as the `Value` property for a string.
   publish the message to the middleware

  ```

  You SHOULD break these steps up into separate methods to reduce the cyclomatic complexity.

### Producer

The producer's implementation of the `Send` or `SendAsync` methods uses the publisher. Typically we implement `SendWithDelay` or `SendWithDelayAsync` and then call those from `Send` and `SendAsync` with a `TimeSpan.Zero` to indicate no delay, for example:

```csharp
public void Send(Message message)
{
  SendWithDelay(message, TimeSpan.Zero);
}
```        

We implement `SendWithDelayAsync` as follows:

```pseudo
ensure that we have a connection to the broker
create an instance of the publisher - passing any connection information needed to send the message
if the delay is `TimeSpan.Zero` or the broker natively supports a delayed publish, call the publisher's `PublishMessage` or `PublishMessageAsync`
else
  use the `Scheduler` set on the producer to schedule the message with the delay.
endif

```

#### Marking Messages as Dispatched in the Outbox

When the message has been sent, e need to mark it as dispatched in the Outbox. Some middleware will asynchronously confirm delivery of the message via a callback. For example, RabbitMQ has [Publisher Confirms](https://www.rabbitmq.com/docs/confirms) and Kafka. Other middleware, for example SQS, returns a value indicating whether we successfully published. In the latter case, the `OutboxProducerMediator` handles marking the message as dispatched in the `Outbox` and you MUST NOT handle this in the producer. In the former case, you should hook up the callback to 
