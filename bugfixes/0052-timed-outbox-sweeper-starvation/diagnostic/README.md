# Diagnostics for #4560

These scratch console apps produced the evidence in `../bugfix.md`. Use them as the starting point for the
`samples/` harness. Both target net9.0 and reference the repo's projects by relative path.

## Builds against a fixed commit

Both apps were written against the code at `2890f8610`, before the fix.

- **Both still build against the fixed code.** The fix left the `TimedOutboxSweeper` constructor
  source compatible.
- **The results change.** Against the fixed code, `blocked` mode with `DOTNET_PROCESSOR_COUNT=1`
  gives the real sweeper 10 ticks with a 1.01 s maximum gap. Before the fix it gave 7 ticks and a
  3.03 s gap. The `PeriodicTimer` async loop still slips (2.01 s). That makes the apps useful as
  before/after demonstrations.
- **`TimerInterval` must be at least 1.** Values below 1 now throw `ConfigurationException`.

## `starve/`: when does the sweeper's tick slip?

`starve/` runs the real `TimedOutboxSweeper` with `TimerInterval = 1`. A recording `IDistributedLock`
times each tick. Three loads are compared:
- none;
- 200 blocked pool items;
- a flood of 20k `Task.Run` per second from a non-pool thread.

Each load also runs a dedicated-thread loop and a `PeriodicTimer` async loop for comparison.

```bash
DOTNET_PROCESSOR_COUNT=1 dotnet run -c Release -- blocked 10   # modes: none | blocked | flood
```

## `pileup/`: the full pipeline under load

`pileup/` runs the real `CommandProcessor`, `OutboxProducerMediator`, `InMemoryOutbox` and
`TimedOutboxSweeper`. A fake Kafka-like producer delivers on a non-pool thread and calls `Task.Run` for
each confirm.

```bash
# args: sync|async  workers  seconds
DOTNET_PROCESSOR_COUNT=2 dotnet run -c Release -- async 64 12
```

| Variable | Effect |
| --- | --- |
| `DI_DEFAULT=1` | Outstanding-check interval of zero, as DI sets it |
| `NO_CHECK=1` | Effectively turns the outstanding check off (1 h interval) |

- **Posters yield.** The posting loops `await Task.Yield()`, which gives fair load. Remove the yield
  to saturate the pool.
