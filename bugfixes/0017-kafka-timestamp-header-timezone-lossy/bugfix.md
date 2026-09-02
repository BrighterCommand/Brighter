# Bugfix: Kafka legacy `TimeStamp` header is timezone-lossy, drifting `Header.TimeStamp` by the host UTC offset per hop

**Linked Issue**: #4253
**Status**: Verified

## Symptom
The Kafka gateway's legacy `TimeStamp` header is written without its UTC offset and read back as
host-local time, so `Message.Header.TimeStamp` drifts by the host's UTC offset on each round-trip
through Kafka.

- Observed (host at UTC+O): hop 1 (original send) preserves the instant but re-represents it at
  `+O`, so the wall clock is off by `O`; hop 2+ (any re-publish, e.g. a requeue) writes that local
  wall clock offset-less and the reader re-assumes it as UTC, so the **instant** advances by `O`
  per subsequent hop.
- Expected: `Header.TimeStamp` names the same instant, with the same UTC wall clock, after any
  number of hops, on any host.

Invisible on a UTC host (including CI), which is why it shipped. First noticed as the 9 unrelated
failures recorded in the Verification section of `bugfixes/0004-kafka-hasfatalerror-not-latched`.

## Suspected Location
Both sides of the round-trip, in `src/Paramore.Brighter.MessagingGateway.Kafka`:

- Write — `KafkaDefaultMessageHeaderBuilder.cs:59-63`: serialises
  `message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture)`. `DateTimeOffset.DateTime`
  yields the clock-time component with `Kind = Unspecified`; the invariant format carries no offset
  marker, so the offset never reaches the wire.
- Read — `KafkaMessageCreator.cs:252`: `DateTime.TryParse(..., DateTimeStyles.AssumeUniversal, out DateTime)`.
  `AssumeUniversal` *without* `AdjustToUniversal` treats the offset-less string as UTC and then converts
  it to host-local (`Kind = Local`); the implicit `DateTime` to `DateTimeOffset` conversion stamps the
  host offset onto it.

Reference (correct, unaffected): the CloudEvents `ce_time` path writes `TimeStamp.ToRfc3339()`
(`KafkaDefaultMessageHeaderBuilder.cs:87`) and reads with offset-aware `DateTimeOffset.TryParse`
(`KafkaMessageCreator.cs:267-270`).

## Root-Cause Hypothesis
One defect with two facets that must be fixed as a pair: the writer drops the offset, and the reader,
having no offset to honour, re-anchors the value to host-local time. Issue's suggested direction —
serialise offset-bearing (RFC 3339) and parse offset-aware / `AssumeUniversal | AdjustToUniversal`.
**UNVERIFIED at triage — proven below.**

## Confirmed Root Cause
**CONFIRMED, by red repro rather than code-trace alone.** A round-trip of a timestamp carrying an
explicit non-UTC offset (`2024-06-15T13:45:30+05:00`, instant `08:45:30Z`) came back, on this host
(IST, +05:30), as `2024-06-15T19:15:30+05:30` — instant `13:45:30Z`. The written wall clock
`13:45:30` was re-read as UTC and re-anchored to local, exactly as hypothesised.

Note this repro is **host-independent**: because the input offset is explicitly non-UTC, the writer
loses information that no reader can recover, so it fails on a UTC host too. That is a stronger
statement than the issue's BST-only reproduction, and it makes the regression test CI-safe.

## Evidence
- [x] **Red repro** — `When_round_tripping_a_timestamp_across_hops`, failing pre-fix with
  `Expected: 2024-06-15T13:45:30.0000000+05:00 / Actual: 2024-06-15T19:15:30.0000000+05:30`.
- [x] **Red repro (legacy-format facet)** — `When_reading_a_timestamp_written_by_an_older_producer`,
  failing pre-fix on the offset assertion with `Expected: 00:00:00 / Actual: 05:30:00` (the host offset).
- [x] **Code-trace** of both write and read sites as described above.

## Scope Notes
- **Suggested-fix assessment: CONFIRMED and adopted.** RFC 3339 on the write side plus offset-aware
  parsing on the read side. Using `ToRfc3339()` also aligns the legacy header with the `ce_time`
  header beside it and with the legacy timestamp header of the Redis, GCP PubSub, RocketMQ and AWS
  gateways, all of which already write `TimeStamp.ToRfc3339()`.
- **Wire-compatibility, both directions.** `AssumeUniversal | AdjustToUniversal` is retained (rather
  than replaced by plain offset-aware parsing) precisely so a *new* consumer still reads the legacy
  offset-less values in flight from *old* producers — taking them as UTC and leaving them there.
  In the other direction an old consumer's `DateTime.TryParse(..., InvariantInfo)` parses an RFC 3339
  `...Z` string, so an old consumer keeps working against a new producer.
- **Also fixed, same defect class, same method:** the binary Unix-ms fallback read in
  `KafkaMessageCreator.ReadTimeStamp`, `DateTimeOffset.FromUnixTimeMilliseconds(...).DateTime`, whose
  trailing `.DateTime` discarded the (zero) offset and let the implicit conversion re-stamp the host
  offset — corrupting the instant for any producer writing a binary timestamp. The `.DateTime` is dropped.
- **Deliberately NOT changed (scope discipline):**
  - The CloudEvents fallback read (`KafkaMessageCreator.cs:267-270`) is instant-correct today and is
    the issue's stated reference; it is left alone.
  - `When_converting_kafkaheader_to_brighterheader.cs:69`'s 5-second-tolerant assertion is left as is.
    It still passes and remains a meaningful single-hop check; the new tests cover the drift it cannot see.
- **Separate pre-existing defect found, NOT fixed (out of scope, worth its own issue):** a consumed
  Kafka message cannot be re-published and re-consumed in-process. `KafkaMessageCreator` puts
  `TopicPartitionOffset` into `Header.Bag`; `KafkaDefaultMessageHeaderBuilder.AddUserDefinedBagHeaders`
  writes the bag back out as a header (it is in neither `BrighterDefinedHeaders.HeadersToReset` nor
  `MessageHeader.IsLocalHeader`); the next read then hits
  `ArgumentException: An item with the same key has already been added. Key: TopicPartitionOffset`
  in `ReadBagEntry`, which `CreateMessage` swallows into a `Message.FailureMessage`. The `ce_*` headers
  are duplicated by the same route. This is what blocked an end-to-end two-hop *read* assertion; the
  regression test asserts the second hop at the wire level instead.

## Regression Test
`tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Reactor/When_round_tripping_a_timestamp_across_hops.cs`
— two `[Fact]`s, broker-free, and green under any host timezone:

- `When_round_tripping_a_timestamp_across_hops` — round-trips a timestamp at `+05:00` and asserts the
  instant and its UTC wall clock survive, that the value read back is anchored to UTC
  (`Offset == TimeSpan.Zero`, which is what makes re-publishing idempotent), and that re-publishing
  what was read produces byte-identical `TimeStamp` header bytes — so drift cannot accumulate over hops.
- `When_reading_a_timestamp_written_by_an_older_producer` — an offset-less invariant-format value is
  read as UTC and left there, pinning the backward-compatibility contract above.

**RED confirmed** with the source change stashed: `Failed: 2, Passed: 0`.

## Fix
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaDefaultMessageHeaderBuilder.cs` — the legacy
  `TimeStamp` header is written as `timeStamp.ToRfc3339()` (UTC, offset-bearing). The
  `DateTime != default` guard that substitutes `UtcNow` for an unset timestamp is preserved verbatim.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageCreator.cs` — `ReadTimeStamp` parses with
  `DateTimeOffset.TryParse(..., DateTimeFormatInfo.InvariantInfo, AssumeUniversal | AdjustToUniversal)`,
  keeping the result anchored to UTC instead of re-stamping it with the host offset; and the Unix-ms
  fallback no longer strips to `.DateTime`.
- `tests/.../Reactor/When_converting_brighterheader_to_kafkaheader.cs:60` — the pinned write-format
  assertion is updated to `message.Header.TimeStamp.ToRfc3339().ToByteArray()`, with a comment giving
  the rationale. That test exists to pin the on-wire format, so changing the format changes it by design.

## Verification
- Regression tests: **2/2 pass**; **RED (2/2 fail) with the source change stashed**, each for the right
  reason (the exact drift values quoted under Evidence).
- Broker-free header tests in the touched area — `KafkaTimeStampRoundTripTests`,
  `KafkaDefaultMessageHeaderBuilderTests`, `KafkaHeaderToBrighterTests`: **3/3 pass**.
- **Not run: `net9.0`.** The `net9.0` runtime is not installed on this machine (`dotnet --list-runtimes`
  shows no `Microsoft.NETCore.App 9.x`), so only `net10.0` of the `net9.0;net10.0` matrix was exercised.
  The change introduces no API not present in both.
- **Not run: the live-broker suite.** No Kafka broker was available; the broker-dependent tests in
  `Paramore.Brighter.Kafka.Tests` fail on this machine with `Local: Broker transport failure` /
  `1/1 brokers are down` **both with and without this change** (verified by stashing). The conformance
  requeue tests named in the issue (FR-22/15/16) therefore remain unverified end to end and should be
  run against a broker before merge.
