# Tutorial samples

The code behind the *Get Started* tutorial ladder in the
[Brighter documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation).
Each rung is one readable delta from the one below it, so a reader — and a reviewer — can
diff two of them and see exactly what a feature costs.

| Rung | Teaches | Sample |
|---|---|---|
| 1 | A command, a handler, no broker | [`samples/CommandProcessor/HelloWorld`](../CommandProcessor/HelloWorld) — reused as-is, which is why there is no `01-` directory here |
| 2 | One event over a RabbitMQ exchange, two processes | [`02-FirstMessage`](02-FirstMessage) |
| 3 | A durable Postgres Outbox, one transaction, the Sweeper | [`03-DurableOutbox`](03-DurableOutbox) |
| 4 | Partitions, a consumer group, per-key ordering, offsets | [`04-Kafka`](04-Kafka) |

Rungs 3 and 4 are each a delta from rung 2 rather than from each other: rung 3 keeps the
transport and adds durability, rung 4 keeps the shape and swaps the transport.

These samples are deliberately smaller than the reference samples they derive from. Where a
sample under `samples/TaskQueue/` shows a feature properly, the tutorial version shows the
least that still works — see each rung's README for what it drops and why. **If you change a
file here, the matching tutorial page needs the same edit**: CI proves this code compiles, but
nothing proves the page still matches it.
