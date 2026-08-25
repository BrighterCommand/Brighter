# 02 — Your First Message Over a Broker

The sample behind rung 2 of the *Get Started* tutorial ladder in the
[Brighter documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation).
Every code block on the page *Your First Message Over a Broker* is this code, so please keep
the two in step: if you change a file here, the tutorial page needs the same edit. CI can
prove this compiles; nothing can prove the page still matches it.

One event goes from a sender process, over a RabbitMQ exchange, to a receiver process that
handles it.

## Running it

From the repository root:

```bash
docker compose -f docker-compose-rmq.yaml up -d
```

That command returns before RabbitMQ is accepting connections — the compose file has no
healthcheck — so give the broker a few seconds. It is ready when
<http://localhost:15672> answers. Starting the apps too early prints connection failures
until the broker comes up; that is the broker, not the sample.

Then, in two terminals — **the receiver first**, because it is the process that declares the
queue and the binding:

```bash
dotnet run --project samples/Tutorials/02-FirstMessage/GreetingsReceiver
```

```bash
dotnet run --project samples/Tutorials/02-FirstMessage/GreetingsSender
```

The receiver prints `Received: Hello from the sender` and keeps running; Ctrl+C stops it.

**If you ran the sender first on a fresh broker, nothing arrives.** The sender declares the
exchange, but no queue is bound to it yet, so RabbitMQ drops the message — and the publish
still succeeds, so the sender reports no error. Start the receiver, then run the sender
again.

Order only matters that first time. The queue is declared `autoDelete: false`, so once the
receiver has run, the queue and its binding outlive the receiver process and survive until
the broker restarts. After that you can run the sender with nothing listening and the
message waits in the queue for the receiver to come back.

Both halves are visible at <http://localhost:15672> (guest/guest): the
`paramore.brighter.exchange` exchange, the `greeting.event` queue, and the binding between
them on routing key `greeting.event`. When you are done:

```bash
docker compose -f docker-compose-rmq.yaml down
```

## Why the topic is a bare string in three places

`"greeting.event"` appears in the sender's publication and twice in the receiver's
subscription, as the channel name and the routing key. A shared constant would be tidier and
is deliberately not used: the lesson of this rung is that two independently deployed
processes agree on a name over the wire, and the sender and receiver are the same two
processes precisely because they *cannot* share code in the general case. Seeing the string
on both sides is the point.

## Why this is not `samples/TaskQueue/RMQTaskQueue`

`RMQTaskQueue` is the reference sample for RabbitMQ and earns its extras — Serilog, a
`CustomPublicationFinder`, a second event type, an explicit scheduler factory. Each of
those is a concept a reader meeting a broker for the first time has to park, so this sample
drops all four and adds nothing. It also has no message mapper: `JsonMessageMapper<T>` is
Brighter's default, and a tutorial should not write code the framework already supplies.

Rung 3 adds a durable Outbox to this sample; rung 4 swaps the transport for Kafka. Each is
one readable delta from this one, which is why this is deliberately the smallest thing that
still crosses a broker.
