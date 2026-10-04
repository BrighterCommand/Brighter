# Bugfix: Reinstate rocketmq-ci so the nine RocketMQ `Fixed (#4240)` conformance cells are protected in CI

**Linked Issue**: #4389
**Status**: Confirmed — **UNBLOCKED, re-verified against merged master** (see note below)

> **#4506 merged** (2026-10-04, `ab83db635`→master via PR #4506). `master` has been pulled into
> this branch cleanly. Re-verification against the now-current
> `RocketMqMessageGatewayProvider.cs:s_topicMap` (done statically — read-only diff, no broker/CI run,
> since RocketMQ is in use by another agent right now) finds **cause C is resolved and two of the
> three Scope Notes defects are also already fixed on master**, independently of this bugfix:
>
> - **Cause C (stale topic list) — resolved.** Every topic `s_topicMap` requires (all `gen_r_*`/
>   `gen_p_*` canonical/legacy topics and their derived `_DLQ`/`_Invalid` names) is present in
>   `docker-compose-rocketmq.yaml`'s `create-topic` block. Compose now also carries a few orphaned
>   leftovers nothing references (`rmq_dlq_source`, `rmq_dlq_target`, `rmq_dlq_invalid`, `gen_r_requeue`,
>   `gen_r_dlq`, `gen_r_dlq_target`, `gen_r_requeue_delay` and the `gen_p_` siblings) — harmless
>   (`mqadmin` just creates topics nothing reads) but worth a cleanup pass separately from this fix.
> - **Scope Note "3 hand-written Reactor tests pollute each other" — resolved.** The three tests that
>   hard-coded `rmq_dlq_source`/`rmq_dlq_target`/`rmq_dlq_invalid`
>   (`When_rejecting_message_with_delivery_error_should_send_to_dlq.cs` and its
>   `..._fallback_to_dlq`/`..._send_to_invalid_channel` siblings, plus `Proactor` async versions) were
>   **deleted** by #4506, superseded by the generated conformance tests in `s_topicMap`, which use
>   per-test topic names. Those three topic names are now unreferenced by any test.
> - **Scope Note "`RocketConsumerFactoryDlqTests` fails in any environment" — resolved.** Commit
>   `ab83db635` ("test: pin RocketMQ compose to 5.5.0 and give the DLQ consumer-factory test a
>   consumer group", 2026-10-01, already on master pre-dating #4506's merge but carried through it)
>   gave that test an explicit `consumerGroup: Guid.NewGuid().ToString()` instead of relying on the
>   `string.Empty` default, and added the `orders`/`orders-dlq`/`orders-invalid` topics it needs. The
>   same commit also pinned all four `apache/rocketmq` images in the compose file to `5.5.0`, which
>   independently resolves the "image is unpinned" sub-point under cause B's write-up.
>
> **What's left is narrower than originally diagnosed: causes A and B only.**
> - **Cause A** (the commented `ci.yml` job: no role/proxy, no topic creation, no live broker) —
>   unchanged, still fully present at `.github/workflows/ci.yml:744-806` (job still commented out).
> - **Cause B** (compose sequencing) — unchanged except for the image pin just noted. The
>   `create-topic` wait loop at `docker-compose-rocketmq.yaml:75` (`curl -s http://broker:10911/
>   &>/dev/null` under `dash`) is still the same no-op construct; `depends_on` entries are still
>   short-form with no health-based ordering; the proxy still has no `restart:` policy.
>
> These two causes do not depend on #4506 and were already known to be independently fixable. No
> live broker/CI run was performed for this re-verification (RocketMQ is currently in use by another
> agent) — the comparison was done by reading `s_topicMap`, the compose file, and test source only.

## Symptom

The RocketMQ transport suite has never run against a live broker in CI. The `rocketmq-ci` job in
`.github/workflows/ci.yml` is still entirely commented out, as the issue says. The issue's line
number has drifted, though: the job now starts at **`.github/workflows/ci.yml:744-806`**, not
`:714`. It still begins with `#  TODO: Rafael Andrade is working on how to run RocketMQ on GHA`
(line 744), followed by `#  rocketmq-ci:` (line 745).

The only RocketMQ coverage in CI is in the `build` job, and none of it needs a broker:
- `ci.yml:92`: the `GatewayChannelFactoryDeclarationTests` sweep for `Paramore.Brighter.RocketMQ.Tests`.
- `ci.yml:94-99`: the `RocketMQ Broker-Free Tests` step, which runs `--filter "Category=RocketMQBrokerFree"`. Its comment says "the other 47 tests in that project need a live broker". The issue quoted this as `ci.yml:70`, which is also stale. The step was added by commit `69a05e123` (2026-09-19).

So every broker-dependent RocketMQ test runs nowhere. That includes the generated Reactor and
Proactor conformance tests under `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/`.
The conformance ledger still records nine `Fixed (#4240)` cells for RocketMQ (FR-4/5/6/7/8/9/16/17/22):
see `specs/0036-universal-transport-conformance-tests/conformance-status.md:187-228`. Its local-run
section (`conformance-status.md:554`) admits that "`rocketmq-ci` has been commented out since #3696,
so local is the only place this ever runs."

**Correction to the issue (stale claim):** the issue says "Revert that one guard [Baggage] and CI
stays green." That is no longer true. The guard now lives in
**`src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqMessagePublisher.cs:111-115`**, not in
`RocketMqMessageProducer`:
```csharp
var baggage = header.Baggage.ToString();
if (!string.IsNullOrEmpty(baggage))
{
    builder.AddProperty(HeaderNames.Baggage, baggage);
}
```
A broker-free test already covers it:
`tests/Paramore.Brighter.RocketMQ.Tests/When_a_header_value_is_empty_should_not_be_written_as_a_property.cs:54-55`
(`[Trait("Category","RocketMQBrokerFree")]`), test `When_baggage_is_empty_should_not_write_the_property`
at line 76, running in CI at `ci.yml:98-99`. The ledger text at `conformance-status.md:189-192` still
names `RocketMqMessageProducer` and is stale on this point too.

What is still genuinely unguarded is the behaviour that needs a broker:
- the reject-time re-publish to the DLQ and invalid topics
- preservation of the `originalTopic` bag entry
- the real `RejectionMetadataKeys` asserted by FR-8
- native delayed delivery for FR-9 (`Delay`-typed topics)
- redelivery after the invisibility lease (FR-7/16/22)

## Suspected Location

**1. The commented job, `.github/workflows/ci.yml:744-806`.** Compared with what RocketMQ 5.x and
the test project actually need, it has these gaps:
- **No proxy container.** It only defines `rocketmq-namesrv` (`:750-760`) and `rocketmq-broker`
  (`:762-781`). The tests connect only through the 5.x gRPC proxy, at `localhost:8081`:
  - `tests/Paramore.Brighter.RocketMQ.Tests/Utils/GatewayFactory.cs:11`: `.SetEndpoints("localhost:8081")`
  - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/When_subscription_does_not_match_should_reject_sync_channel.cs:39`

  The reference compose runs the proxy as a separate container (`docker-compose-rocketmq.yaml:48-66`,
  `command: sh mqproxy`, port 8081).
- **No `command`.** Both services use the bare `apache/rocketmq` image. The compose file starts each
  role with an explicit command: `sh mqnamesrv` (`docker-compose-rocketmq.yaml:11`), `sh mqbroker`
  (`:39`) and `sh mqproxy` (`:61`). GHA `services:` has no `command:` key; only `image`, `env`,
  `ports`, `volumes`, `options` and `credentials` are allowed. So the role command cannot be
  expressed the way the commented job is written. The best `options:` can do is `--entrypoint`,
  which does not supply arguments.
- **No topic creation.** The job relies on `AUTO_CREATE_TOPIC_ENABLE: "true"` (`:781`). The ledger
  records that "RocketMQ 5.x does not auto-create topics through the gRPC proxy"
  (`conformance-status.md:570-572`). About 30 topics have to be created up front, several of them
  `DELAY`- or `FIFO`-typed (`docker-compose-rocketmq.yaml:82-122`).
- **No proxy restart after topic creation.** The proxy caches topic routes at startup, so it needs a
  restart once the topics exist. It can also fail if it starts before the broker has registered with
  the nameserver (`conformance-status.md:573-577`).
- **Readiness checks that don't prove readiness.** The job waits with a blind `sleep 15` (`:790-793`).
  Its health checks (`netstat -an | grep 9876` / `grep 10911`, `:757`, `:772`) only show a socket
  exists, not that the broker is registered or the proxy is serving.
- **Heap not set.** No `JAVA_OPT_EXT` is set, so the image's default broker heap applies. The ledger
  notes the broker "degrad[es] under load" without the raised heap (`conformance-status.md:227-228`).
- **Too-short timeout.** `timeout-minutes: 5` (`:747`) is the shortest of any transport job.
  Comparable jobs use 15-20 (kafka-ci `:324`, aws-mock-ci `:518`, gcp-ci `:710`), and the ledger says
  the requeue tests rely on ~10 s invisibility leases with 30 s retry ceilings.
- **Out-of-date toolchain and test settings.**
  - dotnet: `8.0.x` / `9.0.x` (`:799-800`), while every active job uses `9.0.x` / `10.0.x`, e.g. `:391-393`.
  - The test step is missing `--logger GitHubActions` (`:806`).
  - The `--filter "Fragile!=CI"` (`:806`) does not exclude the FR-2/FR-15 deferred/skipped cells.
    That may be fine because they are Skip-marked, but it is unverified.

**2. Reference infra, `docker-compose-rocketmq.yaml`.**
- Heap raise: confirmed. Broker is `-Xms2g -Xmx2g …` (`:38`) and proxy is `-Xms1g -Xmx1g …` (`:60`),
  introduced in `4458a8417` (#4240). The nameserver is still `-Xmx64m` (`:18`). The comment at
  `:35-37` says "Local-only: rocketmq-ci is commented out … nothing in CI runs this file."
- **mqadmin 5.4.0 vs 5.5.0 mismatch: already fixed, so the issue is stale here.**
  `docker-compose-rocketmq.yaml:79` now resolves the path at run time,
  `MQADMIN=$$(ls -d /home/rocketmq/rocketmq-*/bin/mqadmin | head -1)`, via commit `be2ebd8bf`. The
  ledger paragraph at `conformance-status.md:196-198` still describes the hard-coded path in the
  present tense and is stale. A CI job must use the same glob, not a literal version path.
- Image is unpinned (`apache/rocketmq`, i.e. `:latest`, at `:6/:22/:49/:70`). An unpinned image is
  how the 5.4.0→5.5.0 drift happened. The commented CI job is unpinned too (`ci.yml:751,763`).
- The `create-topic` service's wait loop (`:75`, `curl … http://broker:10911/` with 60 s retries)
  never checks its exit code. Its `depends_on` (`:126-128`) only orders startup and does not wait on
  health, so in CI it would need a real readiness gate.

**3. Patterns already in `ci.yml` that a fix can copy.**
- Every currently working transport job uses native `services:`: redis `:217`, mqtt `:246`,
  rabbitmq `:271/:299`, kafka `:331`, postgres `:404`, sqlserver `:435`, mysql `:461`, dynamo `:496`,
  aws-mock `:522`, mongodb `:681`.
- No job uses `docker compose` in a step (grep finds no `docker compose` / `docker-compose`
  invocation in `ci.yml`).
- The closest model is `kafka-ci` (`ci.yml:322-397`): two interlinked service containers (`kafka` +
  `schema-registry`) that reach each other by service label (`kafka:29092`, `:345/:361`), plus a
  polling readiness loop with a hard failure (`:368-385`, `max_attempts=30`, `exit 1`). It does not
  need a custom `command`, because the Kafka image's default entrypoint starts the role from env
  vars. RocketMQ's image does not.
- Port 8081 clashes with kafka-ci's `schema-registry` locally (`conformance-status.md:578-579`), but
  this does not matter on GHA: each job gets its own runner.

## Root-Cause Hypothesis

**Nothing in the repo records why the job was "disabled", and the issue's framing is inaccurate.**
`git log -S "rocketmq-ci" -- .github/workflows/ci.yml` returns only `af881b599` (fix: RocketMQ #3696,
2025-08-05, Rafael Lillo) and `69a05e123`. The diff of `af881b599` shows the job was **added already
commented out**, with the TODO line. It was never live and then switched off. The squashed
sub-commits are just "fix: GHA CI" ×2, with no stated reason. So "broker startup on GHA runners" is
a paraphrase without evidence, and the original failure is not visible from the repo.

**Hypothesis.** The commented job cannot work as written, whatever the heap or timeout. It is
structurally incompatible with how RocketMQ 5.x and the test suite run:
1. GHA `services:` cannot pass a role command (`sh mqnamesrv` / `sh mqbroker` / `sh mqproxy`), so the
   containers do not start the intended processes.
2. There is no proxy, yet every test connects to `localhost:8081`.
3. Topics are never created, and the proxy does not auto-create them.
4. The proxy's startup route cache would hide topics created after it started, even if 1-3 were fixed.

Raising the heap (already done in compose) and raising `timeout-minutes` are necessary but not
sufficient. On their own they would not turn the job green.

The likely viable shape follows the kafka-ci readiness pattern, but brings the containers up in an
ordered sequence: either `docker compose -f docker-compose-rocketmq.yaml up` or explicit `docker run`
steps inside the job, rather than GHA `services:`. The sequence would be: nameserver → broker → wait
for broker registration → run `create-topic` (globbed `mqadmin`, exit code checked) → start or
restart the proxy → poll the proxy until it serves → tests. The broker and proxy would keep the
2g/1g heap. That totals about 3.1 GB of JVM heap plus the nameserver, which is within the roughly
16 GB on standard `ubuntu-latest` runners. The image should be pinned (e.g. `apache/rocketmq:5.5.0`)
and the timeout set to about 20 minutes. Nothing found suggests option 1 (reinstate per-PR) is
infeasible. It just can't be done by uncommenting — **UNVERIFIED, to be proven or refuted in
/bugfix:confirm**.

**How to falsify it:**
- (a) Uncomment the existing job as is, only bumping dotnet to 9/10 and the timeout to 20. The
  hypothesis predicts failure: wrong or absent role processes, or `Connection refused
  localhost:8081`, or `No topic route info in name server`. It does **not** predict a JVM heap or
  OOM failure. If that job goes green, the hypothesis is wrong.
- (b) On a GHA runner, bring the stack up with `docker compose -f docker-compose-rocketmq.yaml up
  -d`, then restart `rmqproxy` after `create-topic` reports `done`, then run the suite with no
  filter beyond `Fragile!=CI`. The hypothesis predicts the ledger's local result: 21 pass / 2 skip /
  0 fail per variant. If that fails on the runner for resource or startup reasons despite the 2g/1g
  heap, then "runner capacity" is the real blocker and option 1's feasibility has to be reassessed.

## Confirmed Root Cause

**PARTIAL verdict.** The top-level claim holds — you cannot get a working `rocketmq-ci` job by
uncommenting it and bumping the heap/timeout — but two of the four reasons in the triage hypothesis
were overstated, and a **bigger, independent cause** was found that the hypothesis missed entirely.
There are three genuinely separate problems, each sufficient on its own to stop a green job:

**A. The commented job can't start RocketMQ at all (confirmed live).** The bare `apache/rocketmq:5.5.0`
image with no role configured exits immediately (`exec: dummy: not found`). The commented job
(`ci.yml:750-781`) sets no role, defines no proxy, and creates no topics — every test talks to
`localhost:8081`, which nothing is listening on. This produces fast (11-193ms) `Grpc.Core.RpcException
Unavailable … Connection refused` failures, matching the fast failures from my own earlier exploratory
run.

**Correction to triage reason 1**: GHA `services:` having no `command:` key is true, but overstated as
"roles cannot be expressed." The image's own entrypoint picks nameserver/broker roles from the
`NODE_ROLE` **environment variable**, not just CLI args — confirmed live (a container with
`NODE_ROLE=nameserver` booted via `env:` alone, no command needed). The proxy has no `NODE_ROLE`
branch, but `options: --entrypoint ./mqproxy` (no arguments) reached "startup successfully" in a live
probe. GitHub's own docs/community threads confirm `--entrypoint` alone is accepted in `services:`
`options`; only passing *arguments* to it isn't. **Native `services:` is not impossible — it's just
worse**, because it still can't sequence startup, restart a container, or run topic-creation `exec`
steps, which is why the docker-compose/script-driven shape is still the right call, but for a
different reason than originally stated.

**B. `docker-compose-rocketmq.yaml` on master doesn't sequence the stack, so `up -d` is unreliable
(reproduced live, twice).** `depends_on` entries are short-form (`:41,52-53`), so everything starts
within the same second. The `create-topic` wait loop (`:72-76`) is a no-op: `curl … &>/dev/null` under
`dash` means "background the curl, then an empty redirect" — the loop body never runs. The first
`updateTopic` then races broker registration and fails — but **mqadmin exits 0 on failure**, so the
container's own exit code can't be trusted as a readiness signal. `rmqproxy` crashes ~20s after start
(`ProxyException: create system broadcast topic … failed`) because it started before the broker
registered, and has no restart policy, so it stays down.

**Correction to triage reason 4**: "the proxy needs a restart because it caches routes forever" is
wrong. Live test: deleted a topic, created it fresh, retried without restarting the proxy — failed at
t+2s (`No topic route info`), passed at t+34s. The cache heals in ~30s on its own. The proxy needs
restarting because it **crashes** when started too early, not because its cache is permanently stale.
The ledger's claim at `conformance-status.md:573-576` repeats this same overstatement.

**C. Master's topic list is stale relative to the test provider — the main cause of failures on an
otherwise-correctly-sequenced stack. Not in the original hypothesis at all.** Every generated test
looks up its topic in `s_topicMap`
(`tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/RocketMqMessageGatewayProvider.cs:47-96`).
Commit `edf2b4f52` (2026-07-30) renamed those topics (`gen_{r,p}_rej_*`, `_rq_*`, `_nack*`,
`_send_delayed_msg`); `docker-compose-rocketmq.yaml`'s topic-creation block (`:85-122`, last touched
by `be2ebd8bf`) was never updated to match. 26 mapped topics are missing from the compose file, plus
14 derived `_DLQ`/`_Invalid` topics; compose also still creates 12 stale topics nothing uses.

**A fix for this already exists, but not on master.** The sibling worktree
`/Users/ian.cooper/CSharpProjects/github/BrighterCommand/generator-transport-tests` on branch
`feature/4341-delivery-count-and-rejection-routing` has a compose file that pins the image, waits
correctly (`mqadmin clusterList … | grep DefaultCluster`), and creates the full, current topic set
(~100 topics). **This is a real coordination question, not just a code fix** — see the Confirm gate
discussion.

## Evidence
- [x] Code-trace: `ci.yml:744-806` (commented job: unpinned image, no `NODE_ROLE`, no proxy,
  `timeout-minutes: 5`, dotnet 8/9); `git show af881b599` confirms the job was **added already
  commented out** (2025-08-05, "fix: RocketMQ #3696"), with no disable reason recorded anywhere in
  history; `69a05e123` (2026-09-19) only adds the broker-free step and says coverage "still waits on
  rocketmq-ci". No file under `.github/` uses `--entrypoint`/`docker compose`/`docker run` today.
  `docker-compose-rocketmq.yaml:72-76` (no-op dash wait), `:85-122` (stale topic list), `:41,52-53`
  (short-form `depends_on`), no `restart:` on the proxy. `RocketMqMessageGatewayProvider.cs:166-167`
  (DLQ/Invalid key derivation). `RocketMqSubscription.cs:103` (`ConsumerGroup ?? string.Empty`).
- [x] Live repro on a fresh, correctly-sequenced stack (not the dirty 57-minute-old broker from my
  own exploratory run): removed old containers, brought up master's `docker-compose-rocketmq.yaml`
  fresh (`docker-compose -p rmqconfirm … up -d`), waited for `create-topic` to exit, verified via
  `mqadmin topicList` (32/34 expected-by-compose topics existed), started the proxy by hand after
  topics existed (since it had crashed), then ran the full suite once:
  **75 total, 42 passed, 27 failed, 6 skipped.** Failure breakdown: 22 `No topic route info` (20 from
  cause C's missing topics, 2 from cause B's registration race), 3 cross-test-pollution failures among
  hand-written Reactor rejection tests sharing fixed topic names (see Scope Notes), 1 leftover-message
  pollution from the probe itself, 1 pre-existing unrelated test defect (`RocketConsumerFactoryDlqTests`,
  empty consumer group). **Zero JVM heap/OOM failures** — that part of the original hypothesis holds.

## Scope Notes

**This is now a multi-file fix, not a single-workflow-file change**, and it overlaps with in-flight
work on another branch:

- **`docker-compose-rocketmq.yaml` itself needs fixing independent of CI** — the stale topic list
  (cause C) and the no-op wait loop (cause B) are real, pre-existing bugs in this repo's reference
  local-dev infra, not just a CI-authoring problem. A fix already exists on
  `feature/4341-delivery-count-and-rejection-routing` (a branch that hasn't merged).
- **Three hand-written Reactor tests pollute each other even on a perfectly fresh broker, within a
  single run**: `When_rejecting_message_with_delivery_error_should_send_to_dlq.cs:47-48`,
  `..._unacceptable_and_no_invalid_channel_should_fallback_to_dlq`, and
  `..._unacceptable_reason_should_send_to_invalid_channel` (plus their `Proactor` async siblings) all
  hard-code the same three topic names (`rmq_dlq_source`, `rmq_dlq_target`, `rmq_dlq_invalid`) with a
  fresh consumer group each time — a new group reads whatever's already sitting on the topic from a
  sibling test. These need per-test topics (matching the generated tests' own convention) or
  serialization, or they'll keep failing intermittently in a reinstated CI job regardless of the
  workflow fix.
- **`RocketConsumerFactoryDlqTests` fails in any environment**, unrelated to broker/CI state —
  `RocketMqSubscription.cs:103` defaults `ConsumerGroup` to `""`, which RocketMQ's client-side
  validation rejects. Pre-existing defect, not caused by this work, but it will block a green
  `rocketmq-ci` run if not separately fixed.
- **Re-running the suite against the same broker always pollutes** (every generated test uses a fixed
  topic name; only a fresh consumer group changes). Safe for GHA (new runner per job), unsafe for any
  in-job retry loop or local re-run without tearing the stack down — worth stating explicitly in
  whatever CI job lands, so nobody "fixes" an intermittent failure by adding a retry.
- **Ledger corrections needed independent of this fix**: `conformance-status.md:573-576` ("proxy
  stays invisible until restart") should say ~30s self-heal, not permanent staleness; `:188`'s claimed
  "21 pass / 2 skip / 0 fail" per variant isn't reproducible from master's compose as it stands.
- **Topic-creation timing**: ~8s per `updateTopic` call under this machine's emulation (~13 min for
  ~100 topics) — unverified on a native x64 GHA runner, but worth budgeting for in the timeout and/or
  batching topic creation into fewer `docker exec` calls.

## Regression Test

**Scope: cause B only.** Causes A and C are not covered by an xUnit regression test, by deliberate
decision:
- **Cause C** is already fixed on master (see the re-verification note at the top of this file) —
  no test needed.
- **Cause A** (the commented-out `ci.yml` job) cannot be exercised locally at all — a GHA `services:`
  job only runs on an actual GitHub Actions runner. Its verification is a real CI run on the pushed
  branch/PR, done at `/bugfix:verify`, not a local test.
- **Cause B** gets one static characterization test now (RocketMQ is free to use again — the other
  agent's branch is GCP-focused), plus a live `docker-compose up` run as real-world proof once the
  fix lands (decided with the user before writing the test).

**Test**: `tests/Paramore.Brighter.RocketMQ.Tests/When_the_create_topic_service_waits_for_broker_readiness_should_check_curls_real_exit_status.cs`
(class `RocketMqComposeBrokerReadinessWaitTests`, `[Trait("Category","RocketMQ")]` +
`[Trait("Category","RocketMQBrokerFree")]`, so it runs in `ci.yml`'s existing broker-free step with
no broker needed).

Reads `docker-compose-rocketmq.yaml`'s own text (no production C# class exists for this defect — it
is a shell-syntax bug in reference infrastructure, not application code) and asserts the
`create-topic` service's `until curl …` broker-readiness wait condition does not contain the bare
`&>/dev/null` dash-ism. Confirmed RED on the first run, for the right reason:

```
Assert.DoesNotMatch() Failure: Match found
String: ···" curl -s http://broker:10911/ &>/dev/null"
RegEx:  "&>\s*/dev/null"
```

The other 5 tests under `Category=RocketMQBrokerFree` still pass — no collisions introduced.
User-approved to proceed to the fix. Not yet committed (this branch lands everything in one final
commit via `/bugfix:fix`/`/bugfix:verify`, per the standing convention noted in `PROMPT.md`).

**Status**: Tested (cause B). Causes A and C have no regression test by design (see above) — `/bugfix:fix` covers all three; `/bugfix:verify` supplies the live-broker and real-CI proof for B and A respectively.

## Fix

**Scope: causes A and B.** Cause C needed no change (already fixed on master by #4506's merge).

### Cause B — `docker-compose-rocketmq.yaml`

- **The confirmed defect**: fixed the broker-readiness wait's redirect from the dash-broken
  `curl -s http://broker:10911/ &>/dev/null` to a real check.
- **A second, independent defect found live while fixing the first**: a plain `curl` against port
  10911 never completes, because that port speaks RocketMQ's own binary protocol, not HTTP — fixing
  only the redirect turned the no-op into a *genuine infinite retry* (confirmed live: 20+ minutes of
  `Broker not fully ready yet, retrying in 60 seconds...` with the broker already healthy). Replaced
  the probe with a direct TCP check (`until echo > /dev/tcp/broker/10911 2>/dev/null; do … done`),
  which requires `bash` rather than `sh` for this service's `command:` (confirmed `bash` is present
  in the image; `/dev/tcp` is a bash-ism dash does not support). Dropped the retry sleep from 60s to
  5s to match the second wait loop, now that a retry is cheap and should rarely be needed.
- **`depends_on` short-form → health-gated**: `broker`, `proxy` and `create-topic` now
  `depends_on: { <service>: { condition: service_healthy } }` instead of the short (list) form, so
  they wait for the dependency's healthcheck rather than just its container start.
- **A third, independent defect found live while exercising the above**: the existing
  `netstat`-based healthchecks (`nameserver`, `broker`, `proxy`) have always been silently broken —
  `netstat` does not exist in `apache/rocketmq:5.5.0` (`sh: 1: netstat: not found`). This was inert
  under the old short-form `depends_on` (nothing read health status), but became a hard blocker the
  moment `depends_on` started gating on it: every service came up "unhealthy" forever. Replaced all
  three with the same `bash -c "echo > /dev/tcp/localhost/<port>"` TCP-connect check used above.
- **Proxy restart policy**: added `restart: on-failure:5` to `proxy`, so it recovers if it still
  starts before the broker has registered with the nameserver (the healthcheck only proves the
  broker's port is open, not registration, which can lag slightly behind).
- Removed the now-stale "nothing in CI runs this file" comment, since cause A reinstates `ci.yml`'s
  use of it.

**Live-verified** (RocketMQ freed up mid-fix — the other agent's branch was GCP-focused; had to tear
down a stale stack left running under the sibling `generator-transport-tests` worktree first, with
the user's explicit go-ahead): fresh `docker-compose up -d nameserver broker proxy` — nameserver and
broker came up healthy and gated correctly; proxy started, stayed up through the entire ~13-minute,
100-topic `create-topic` run with **0 restarts** (previously it crashed ~20s in and stayed down).
`create-topic` exited 0. Restarted the proxy per the planned sequence; confirmed it was serving.
Ran the full suite: **93 total, 85 passed, 2 failed, 6 skipped** — a large improvement on Confirm's
baseline (75/42/27/6), and the 2 remaining failures are an unrelated, newly-discovered defect (see
below), not causes A/B/C.

### Cause A — `.github/workflows/ci.yml`

Replaced the fully-commented `rocketmq-ci` job (`:839-901`, GHA `services:` with no role, no proxy,
no topics, `sleep 15`, `timeout-minutes: 5`, dotnet 8/9) with a job that brings the stack up with
`docker compose` in steps rather than GHA `services:` — matching the pattern `gcp-emulator-ci`
already uses for a job GHA's declarative services block can't express (startup sequencing,
restarting a container, running a one-shot `create-topic` step). Steps: checkout → start
nameserver/broker/proxy → run `create-topic` to completion (`--exit-code-from`) → restart the proxy
→ poll it until it serves → setup dotnet 9.0.x/10.0.x → restore → run the suite (`Fragile!=CI`,
`--logger GitHubActions`) → tear down (`if: always()`). `timeout-minutes: 20`, matching `kafka-ci`.

**Not verified by a local test or run** — a GHA `services:`/job only runs on a real Actions runner.
Per the decision at `/bugfix:test`, this is proven by an actual CI run on the pushed branch/PR at
`/bugfix:verify`, not locally. Given the live-verification result above, **that real run is expected
to show 2 failures** (the new finding below), not a clean pass — this is expected and not a
regression introduced by this fix.

**First real CI run (PR #4509) caught a genuine defect in this fix itself, not the predicted
finding.** `rocketmq-ci` failed at the "Wait for the proxy to serve" step — all 30 attempts of
`curl -s -o /dev/null http://localhost:8081/` failed, and the proxy's own logs showed repeated
`INFO: Transport failed … Http2Exception.connectionError`, not "connection refused". Port 8081 is
the proxy's gRPC/HTTP2 endpoint, not plain HTTP, so a bare `curl` GET gets a real (rejected)
response rather than ever reporting ready — the same class of mistake as `curl` against the
broker's binary-protocol port 10911, caught locally earlier in this same Fix step, but not carried
over to this wait step in `ci.yml` (it was written, and validated only for YAML syntax, before that
lesson was re-applied here). Fixed by replacing the `curl` check with the same bare TCP-connect
idiom already used for the compose healthchecks: `until echo > /dev/tcp/localhost/8081 2>/dev/null;
do … done`. Pushed as a follow-up commit on the same PR.

**Second real CI run caught a second, also genuine, also self-inflicted defect.** The proxy-wait
fix worked - the job reached the `RocketMQ Tests` step - which then failed broadly: **105 total,
first TFM pass 88 passed/15 failed/2 skipped, second TFM pass 81 passed/22 failed/2 skipped**, with
message-ID mismatches (`Assert.Equal() Failure: Values differ`, completely unrelated expected/actual
GUIDs) and `Sequence contains no matching element` on ordinary tests that pass trivially and fast
locally (e.g. `When_posting_a_message_via_the_messaging_gateway_should_be_received`, 50ms locally).
Cause: the `RocketMQ Tests` step's `dotnet test` had no `--framework`, so it ran **both net9.0 and
net10.0 sequentially against the same live broker and the same fixed per-behaviour topic names** -
exactly the "re-run against the same broker without tearing the stack down" pollution mode this
bugfix's own Confirm phase documented as unsafe (Scope Notes, above). The second TFM pass showing
more failures than the first (22 vs. 15) is consistent with pollution compounding on the second
pass. `gcp-emulator-ci` and the GCP Pull jobs already pin `--framework net10.0` for the same
reason, but that convention was omitted by oversight when the `rocketmq-ci` step was written.
Fixed by adding `--framework net10.0` to match that established convention. Pushed as a second
follow-up commit; awaiting the next CI run.

**Both of these were authoring gaps in this PR's own `ci.yml` job, not design flaws in causes A or
B's actual fix** - the compose file and its healthchecks/sequencing were never at fault in either
case. Mentioned here in full because the pattern is worth naming: a lesson learned once locally
(curl vs. a non-HTTP RocketMQ port; one TFM only against live infra) has to be applied everywhere
the same risk recurs, not just where it was first found.

### New finding, out of scope — native delayed-delivery defect

Both the Reactor and Proactor variants of `When_sending_a_delayed_message_should_deliver_after_delay`
failed on the live-verified stack above (`Assert.NotEqual() Failure: Expected: Not MT_NONE, Actual:
MT_NONE` — a message sent with a native RocketMQ delay never arrived). This is **not** cause A, B or
C, and was never visible before: the full suite has never run against a correctly-sequenced live
broker until this fix. **Decision (discussed with the user): land causes A and B as scoped; do not
fold this into the current fix.** Flagged here for a possible separate `/bugfix:triage` — it covers
FR-9 (native delayed delivery), one of the ledger's nine `Fixed (#4240)` RocketMQ cells.

**Update: did not reproduce on the real `rocketmq-ci` run (third CI attempt, after the two `ci.yml`
fixes above) — both variants passed, 5s each, in a fully green 105/103/0/2 run.** This defect was
reproducible twice, consistently, on this machine's local Docker stack (`platform: linux/amd64`
under ARM emulation - the same emulation the Confirm phase's evidence already flagged as ~8s/topic
vs. an unverified native-runner speed). The most likely explanation is that it is a timing artifact
of that local emulation rather than a genuine cross-environment RocketMQ defect - native delayed
delivery may simply need longer than the emulated environment allows to land within the test's
receive window. **Not escalating to a new bugfix on current evidence** - there is no live CI failure
to drive one, and a fix chased from an unreproduced-on-the-target-environment symptom risks fixing
the wrong thing. Worth a note if it resurfaces on a real run, local Apple Silicon, or under load.

### Regression test

`RocketMqComposeBrokerReadinessWaitTests.When_the_create_topic_service_waits_for_broker_readiness_should_not_be_a_no_op`
renamed/rewritten mid-fix (from `…should_check_curls_real_exit_status`) once the curl-against-a-
binary-port defect was found — it no longer references `curl` at all, so it was re-anchored on the
`Waiting for broker to be healthy…` marker and now asserts the wait condition (a) contains no bare
`&>` and (b) actually probes `broker`/`10911`, not just avoids the broken operator. Green on both
`net9.0`/`net10.0`; the other 5 `Category=RocketMQBrokerFree` tests still pass.

**Status**: **Verified — PR #4509's real `rocketmq-ci` run is green** (105 total, 103 passed, 0
failed, 2 skipped). Causes A and B fixed and confirmed on a real GHA runner, in addition to local
live verification. Cause C required no change. Two further defects, both self-inflicted in this
PR's own `ci.yml` authoring (not in causes A/B's actual design), were found and fixed across two
follow-up commits during the real CI cycle — see the Fix section. The native-delayed-delivery defect
found during local live verification did not reproduce on the real runner (see Fix section) and
is not escalated to a new bugfix absent further evidence.

## Verify

- Regression test re-run in isolation: `RocketMqComposeBrokerReadinessWaitTests.When_the_create_topic_service_waits_for_broker_readiness_should_not_be_a_no_op`
  — **passed** on both `net9.0` and `net10.0`.
- Broader suite for the touched project, `Category=RocketMQBrokerFree` (6 tests, no broker needed):
  **6/6 passed** on both TFMs — no regressions from the new test or the compose/ci.yml changes.
- The full broker-dependent suite was exercised live locally as part of the Fix step, against the
  state of every compose/test change in this PR: **93 total, 85 passed, 2 failed, 6 skipped** — a
  strict improvement over Confirm's pre-fix baseline (75/42/27/6). The 2 failures were the
  native-delayed-delivery finding.
- `.github/workflows/ci.yml` parsed as valid YAML throughout (26 jobs).
- **Real CI, three attempts on PR #4509** (GHA `services:`/jobs can't be exercised any other way):
  1. First run: failed at the proxy-readiness wait (`curl` against the proxy's gRPC/HTTP2 port
     8081 got a real rejection, not "not ready yet") — fixed with a `/dev/tcp` TCP check.
  2. Second run: reached the test step, failed broadly (105 total, 15 then 22 failed across the two
     TFM passes) with cross-run message-ID pollution — `dotnet test` had no `--framework`, so
     net9.0 and net10.0 ran sequentially against the same broker and fixed topic names. Fixed by
     pinning `--framework net10.0`, matching `gcp-emulator-ci`'s existing convention.
  3. Third run: **green** — 105 total, 103 passed, 0 failed, 2 skipped, including both previously-
     failing delayed-delivery tests (5s each). Every other PR check passed except `dynamo-ci`,
     unrelated to this work (no DynamoDB files touched) and already failing independent of this
     branch's changes.

### Critical Files for Implementation
- .github/workflows/ci.yml
- docker-compose-rocketmq.yaml
- tests/Paramore.Brighter.RocketMQ.Tests/Utils/GatewayFactory.cs
- specs/0036-universal-transport-conformance-tests/conformance-status.md
- src/Paramore.Brighter.MessagingGateway.RocketMQ/RocketMqMessagePublisher.cs
