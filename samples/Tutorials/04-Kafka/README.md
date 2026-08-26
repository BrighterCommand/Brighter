# 04 — Streaming with Kafka

The sample behind rung 4 of the *Get Started* tutorial ladder in the
[Brighter documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation).
Every code block on the page *Streaming with Kafka* is this code, so please keep the two in
step: if you change a file here, the tutorial page needs the same edit. CI can prove this
compiles; nothing can prove the page still matches it.

The same one event as rung 2, over a three-partition Kafka topic, to a consumer group you can
scale by starting a second copy of the receiver.

## Running it

From the repository root:

```bash
docker compose -f docker-compose-kafka.yaml up -d kafka
```

The `kafka` argument matters: the compose file also defines a schema registry and a control
centre, and this sample needs neither. As with rung 2's broker, the command returns before
Kafka is accepting connections. It is ready when this stops failing:

```bash
docker exec kafka /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server localhost:9092
```

Then, in two terminals — the receiver first, so that you can watch it take the partitions:

```bash
dotnet run --project samples/Tutorials/04-Kafka/GreetingsReceiver
```

```bash
dotnet run --project samples/Tutorials/04-Kafka/GreetingsSender
```

The receiver logs its assignment, and on an empty broker that is all three partitions:

```text
Partition Added greeting.event : 0,greeting.event : 1,greeting.event : 2
```

The sender publishes nine greetings and prints `Published 9 greetings to greeting.event`.
When you are done:

```bash
docker compose -f docker-compose-kafka.yaml down -v
```

Either process will create the topic if it is missing, so unlike rung 2 the order does not
matter: Kafka retains what it accepts, and the subscription's `offsetDefault` is
`AutoOffsetReset.Earliest`, so a receiver started afterwards reads the greetings that are
already there.

If you start the sender within a second or two of the broker, it may log
`Failed to acquire idempotence PID from broker … Coordinator load in progress: retrying`.
That is the broker still starting up and the producer doing what it says — retrying. It
resolves itself.

## The order they arrive in is not the order they were sent

The sender interleaves three recipients — `alice`, `grace`, `mia`, three greetings each,
round-robin. The receiver prints them grouped:

```text
Received: Hello alice #1
Received: Hello alice #2
Received: Hello alice #3
Received: Hello grace #1
Received: Hello grace #2
Received: Hello grace #3
Received: Hello mia #1
Received: Hello mia #2
Received: Hello mia #3
```

Both halves of that are the point. Kafka orders messages **within a partition** and makes no
promise across partitions, so the global send order is gone. Each recipient's three greetings
are still in sequence, because the partition key sends all of one recipient's greetings to
one partition, and the Reactor pump is a single thread draining one partition at a time.

## Why those three names

Kafka chooses the partition by hashing the key — `crc32(key) % 3` for the default
partitioner — so you do not choose it, you only choose the key. With three keys over three
partitions there is a 2-in-9 chance they land one apiece.

The first three names this sample used, `alice`, `bob` and `carol`, all hashed to partition
**2**. Nothing looked wrong: one consumer holds every partition anyway, so all nine greetings
arrived in the order they were sent and the sample appeared to work perfectly. It took asking
the broker to see it:

```bash
docker exec kafka /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server localhost:9092 --topic greeting.event --from-beginning \
  --max-messages 9 --property print.partition=true --property print.key=true \
  --property print.value=false --timeout-ms 15000
```

`alice`, `grace` and `mia` were checked with that command and land on partitions 2, 0 and 1.
Real keys are business identifiers and you take the partition the hash gives you; these are
chosen so the sample has something to show.

## Scaling the group

Start a second copy of the receiver, with no arguments and no configuration change:

```bash
dotnet run --project samples/Tutorials/04-Kafka/GreetingsReceiver
```

Both share the `greeting.readers` group id, so Kafka treats them as two members of one group
and rebalances. The first instance logs the revocation and what it kept, the second logs what
it took:

```text
Partitions for consumer revoked greeting.event : [0],greeting.event : [1],greeting.event : [2]
Partition Added greeting.event : 0,greeting.event : 2
```

```text
Partition Added greeting.event : 1
```

The two sets are disjoint and cover all three partitions. Which instance gets which is Kafka's
call, not yours — across two runs of this sample the split came out `[1]` / `[0,2]` and then
`[0,2]` / `[1]`.

## A rebalance redelivers, and that is at-least-once

After the rebalance, some greetings are handled a **second** time. **How many depends on when
you start the second instance**, and that is worth understanding rather than memorising,
because the mechanism is the whole of Kafka's at-least-once guarantee.

Watch the committed offsets while the first instance runs on its own:

```bash
docker exec kafka /opt/kafka/bin/kafka-consumer-groups.sh \
  --bootstrap-server localhost:9092 --describe --group greeting.readers
```

**Immediately after all nine greetings are handled, nothing is committed at all:**

```text
TOPIC           PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG
greeting.event  0          -               3               -
greeting.event  1          -               3               -
greeting.event  2          -               3               -
```

Handling a message *stores* an offset; it does not commit one. Brighter commits on two paths,
and neither has run yet. The first is the batch: `commitBatchSize` defaults to **10** and each
partition holds **3** messages, so it is never reached. The second is a timer —
`sweepUncommittedOffsetsInterval`, default **30 seconds**.

**Once that sweeper has fired, each partition sits one message short:**

```text
greeting.event  0          2               3               1
greeting.event  1          2               3               1
greeting.event  2          2               3               1
```

That `LAG 1` is stable — it was still 1 a minute later. The sweeper drains its stored offsets
in no particular order and commits them in one call, so the offset that sticks is not
necessarily the highest.

So the rebalance redelivers whatever was uncommitted when it happened:

| Second instance started | Redelivered (of 9) |
|---|---|
| Before the first sweep, within a few seconds | **7** |
| After the sweep | **3** — one per partition, on two separate runs |

Nothing is ever *lost*: every one of the nine distinct greetings is handled. But some are
handled twice, and **no timing makes that number zero**. That is what at-least-once means, and
it is why a handler doing real work — charging a card, sending an email — has to be idempotent.

## Why this is not `samples/TaskQueue/KafkaTaskQueue`

`KafkaTaskQueue` is the reference sample for Kafka and earns its extras — a Polly
`PolicyRegistry`, an explicit scheduler factory, a hosted service generating messages on a
timer, and a hand-written message mapper. Each is a concept a reader meeting partitions for
the first time has to park, so this sample drops all four.

Dropping the mapper is the one worth explaining, because `KafkaTaskQueue` uses its mapper to
set the partition key. It does not have to. `JsonMessageMapper<T>` is Brighter's registered
default for **both** the sync and the async path, and it already reads the key from the
request context onto the message header, so this sample sets it there:

```csharp
var context = new RequestContext();
context.Bag[RequestContextBagNames.PartitionKey] = recipient;

commandProcessor.Post(new GreetingEvent($"Hello {recipient} #{i}"), context);
```

The pump also runs as a **Reactor** rather than a Proactor, which is `KafkaSubscription`'s own
default. That is what makes the single-threaded ordering argument above visible in the code
instead of asserted about it, and it means the handler is a plain synchronous
`RequestHandler<GreetingEvent>` — the same file as rung 2's, byte for byte.

## What changed from rung 2

This sample is rung 2 with the transport swapped, and `diff -r` says so — four files differ
and three are untouched:

```text
samples/Tutorials/02-FirstMessage        samples/Tutorials/04-Kafka
  Greetings/GreetingEvent.cs             identical
  Greetings/Greetings.csproj             identical
  GreetingsReceiver/GreetingEventHandler.cs   identical
  GreetingsSender/Program.cs             Kafka publication, NumPartitions, partition key
  GreetingsSender/GreetingsSender.csproj      Kafka gateway instead of RMQ
  GreetingsReceiver/Program.cs           KafkaSubscription, groupId
  GreetingsReceiver/GreetingsReceiver.csproj  Kafka gateway instead of RMQ
```

Rung 3 adds a durable Outbox to rung 2 and changes only the sender; this rung changes both
ends, because a transport is something two processes have to agree on.
