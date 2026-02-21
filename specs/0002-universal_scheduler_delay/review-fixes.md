# Review Fixes for 0002-universal_scheduler_delay

## Critical Issues

- [x] **1. RMQ Sync: `SendWithDelayAsync` drops delay argument** — `RMQ.Sync/RmqMessageProducer.cs:195-198` — Pass `delay` through to `SendWithDelay(message, delay)`
- [x] **2. RMQ Sync: Duplicate `x-overflow` key** — `RMQ.Sync/RmqMessageConsumer.cs:509-518` — Use ternary like async version: `_hasDlq ? "reject-publish-dlx" : "reject-publish"`
- [x] **3. Redis: `PurgeAsync` client not disposed** — `RedisMessageConsumer.cs:173` — Add `await using`
- [x] **4. Redis: `RequeueAsync` (non-delayed) client not disposed** — `RedisMessageConsumer.cs:389` — Add `await using`
- [~] **5. Redis: `EnsureRequeueProducer` clobbers static `s_pool`** — Pre-existing architectural issue with static pool design; both pools connect to same Redis and are disposed correctly on cleanup. Proper fix requires redesigning `RedisMessageGateway` static state — out of scope for this PR.
- [x] **6. Core: `InMemoryScheduler` missing `IDisposable`** — `InMemoryScheduler.cs:48` — Implement `IDisposable`/`IAsyncDisposable`, dispose all timers
- [x] **7. MsSql: `CancellationTokenSource` leak in `ReceiveAsync`** — `MsSqlMessageConsumer.cs:99-101` — Wrap both CTS in `using`/`await using`
- [x] **8. MQTT: `MqttMessagePublisher._mqttClient` never disposed** — `MQTTMessageProducer.cs:68,74` — Implement disposal chain through producer and publisher

## Important Issues

- [x] **9. Kafka: Exception message references "SQSConnection"** — `KafkaMessageConsumerFactory.cs:69` — Fix to "KafkaSubscription"
- [x] **10. MsSql: OTel typo `"microsft_sql_server"`** — `MsSqlMessageProducer.cs:139,176` — Fix to `"microsoft_sql_server"`
- [x] **11. Kafka: `DateTimeOffset.UtcNow` bypasses `TimeProvider`** — `KafkaMessageConsumer.cs:981` — Use `_timeProvider.GetUtcNow()`
- [x] **12. Kafka: `SendWithDelay` null check only on zero-delay path** — `KafkaMessageProducer.cs:247,321` — Move null guard before delay branching
- [x] **13. Core: `InMemoryScheduler.Schedule(Message)` missing negative delay guard** — `InMemoryScheduler.cs:64` — Add same guard as other overloads
- [x] **14. Redis: Stale RMQ XML doc in `ChannelFactory`** — `Redis/ChannelFactory.cs:31-33` — Fix doc comment
- [x] **15. MQTT: Dead null guard on `TopicPrefix`** — `MQTTMessageConsumer.cs:61` — Move null check before interpolation
- [x] **16. MQTT: Wrong log method on connection failure** — `MQTTMessagePublisher.cs:121` — Call `UnableToConnectToHost` instead
