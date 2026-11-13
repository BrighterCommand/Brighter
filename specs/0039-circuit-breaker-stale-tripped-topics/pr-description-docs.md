## BrighterCommand/Docs changes

Writing these pages is tracked separately. This PR changes behaviour that the pages below describe.

### Circuit-breaker page

- Register `DistributedOutboxCircuitBreaker` (and `InMemoryOutboxCircuitBreaker`) as a **singleton** shared with the mediator. Brighter adds no `AddDistributedOutboxCircuitBreaker` extension, and registers no breaker by default.
- The clock assumption: hosts run NTP, and `ClockSkewLeeway` covers residual skew. The distributed trip length is `CooldownDuration` plus `ClockSkewLeeway`.
- The table name passed to the store must match the one passed to `Add{Backend}TrippedTopics`. Both default to `BrighterTrippedTopics`.
- Set the store's `commandTimeout` equal to `StoreTimeout`, and keep `StoreTimeout` at or under half of `TimerInterval`.
- The purge script (`GetPurgeSql`, with the `@EndedBefore` cutoff the operator binds) runs under a separate account with `DELETE` permission. The application account needs only `SELECT`, `INSERT` and `UPDATE`.

### Telemetry page

- With the distributed breaker every instance reports the deployment-wide count, so alert on `max` across instances, not `sum`. Counts agree within `TimerInterval` plus `StoreTimeout`.
- The new span attribute, `circuit_breaker.cooldown_duration`.
- The difference in announcement rate between the breakers: the in-memory breaker announces every failed message, and the distributed breaker announces once per coalesced write.

### Health-check page

- The new `tripped_topics_store_unavailable` condition and its `exception_type` (the exception's short type name, or `timeout`).
- The changed applies-when rule for `tripped_topics`: the sweeper is active, or the sweeper is running and the breaker shares trips.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01GcetE2hjJheqtQvHoVF1at
