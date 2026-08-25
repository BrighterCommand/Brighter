# 02 — Your First Message Over a Broker

The sample behind rung 2 of the *Get Started* tutorial ladder in the Brighter
documentation. Every code block on that page is this code, so please keep the two in step:
if you change a file here, the tutorial page needs the same edit.

One event goes from a sender process, over a RabbitMQ exchange, to a receiver process that
handles it.

## Running it

From the repository root:

```bash
docker compose -f docker-compose-rmq.yaml up -d
```

Then, in two terminals — the receiver first, because it is the one that declares the queue:

```bash
dotnet run --project samples/Tutorials/02-FirstMessage/GreetingsReceiver
```

```bash
dotnet run --project samples/Tutorials/02-FirstMessage/GreetingsSender
```

The receiver prints `Received: Hello from the sender` and keeps running; Ctrl+C stops it.

## Why this is not `samples/TaskQueue/RMQTaskQueue`

`RMQTaskQueue` is the reference sample for RabbitMQ and earns its extras — Serilog, a
`CustomPublicationFinder`, a second event type, an explicit scheduler factory. Each of
those is a concept a reader meeting a broker for the first time has to park, so this sample
drops all four and adds nothing. It also has no message mapper: `JsonMessageMapper<T>` is
Brighter's default, and a tutorial should not write code the framework already supplies.

Rung 3 adds a durable Outbox to this sample; rung 4 swaps the transport for Kafka. Each is
one readable delta from this one, which is why this is deliberately the smallest thing that
still crosses a broker.
