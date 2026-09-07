# Bugfix: ASB gateway cannot receive a message with a relative CloudEvents source

**Linked Issue**: #4310
**Status**: Fixed

## Symptom

**Observed.** Receiving a message over Azure Service Bus whose CloudEvents `source` attribute is a
*relative* URI throws `System.UriFormatException: Invalid URI: The format of the URI could not be
determined.` The message is never delivered to the pipeline.

**Expected.** The message is received and mapped, with `Header.Source` carrying the relative URI (or,
failing that, the same documented fallback the missing/empty cases already use).

**Reproduction (no broker required).** `AzureServiceBusMessageCreator.MapToBrighterMessage` is
directly unit-testable with the `BrokeredMessage` test double — see
`tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_A_Deferred_Message_Is_Redelivered.cs`
for the established pattern. Put a relative URI string in `ApplicationProperties["cloudEvents:source"]`
and map it.

**Observed in CI.** Run `34127954869`, job `101763183832`, `azure-ci`. Both variants (Reactor and
Proactor) of `When_sending_a_delayed_message_should_deliver_after_delay` failed this way:

```
System.UriFormatException : Invalid URI: The format of the URI could not be determined.
   at System.Uri..ctor(String uriString)
   at …AzureServiceBusMessageCreator.GetSource(IBrokeredMessageWrapper)
      in .../AzureServiceBusMessageCreator.cs:329
   at …AzureServiceBusMessageCreator.MapToBrighterMessage(IBrokeredMessageWrapper)
      in .../AzureServiceBusMessageCreator.cs:85
   at …AzureServiceBusConsumer.ReceiveAsync(Nullable`1, CancellationToken)
      in .../AzureServiceBusConsumer.cs:220
   at Paramore.Brighter.ChannelAsync.ReceiveAsync(…) in .../ChannelAsync.cs:153
```

Those two are the **only** generated ASB tests that ever received a real message — the other 26 are
blocked upstream by #4309. See `bugfixes/0018-asb-subscription-created-after-send/bugfix.md`.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageCreator.cs:309`
  — `GetSource`, ending `return new Uri(source!);` at **line 331** in the current tree (the CI stack
  frame says 329; the branch has drifted by two lines).
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessageCreator.cs:151`
  — `GetCloudEventsDataSchema`, ending `return new Uri(dataSchema);` at **line 173**. Same bug shape,
  not yet observed failing only because no test sets a relative `DataSchema`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusMessagePublisher.cs:87`
  — the write side, `message.Header.Source?.ToString() ?? string.Empty`. Believed correct: it
  round-trips a relative URI to a bare string faithfully. It is only the read side that cannot parse
  it back.

## Root-Cause Hypothesis

**UNVERIFIED — to be proven or refuted in /bugfix:confirm.**

`GetSource` and `GetCloudEventsDataSchema` construct a `System.Uri` with the single-argument
`new Uri(string)` overload, which defaults to `UriKind.Absolute` and throws on anything relative.
CloudEvents defines `source` as a **URI-reference**, which MAY be relative, so a relative value is
legal and must be accepted. Both methods already handle the *missing* and *empty* cases by falling
back to `new Uri("http://goparamore.io")`; they simply do not handle *present but relative*.

**Why this is a gateway defect and not a test artefact.** The generated conformance suites build
messages from one shared template,
`tools/Paramore.Brighter.Test.Generator/Templates/DefaultMessageBuilder.cs.liquid`, which defaults the
source to a relative URI:

```csharp
private Uri? _source = new Uri(Uuid.NewAsString(), UriKind.Relative);
```

All **17** generated `DefaultMessageBuilder.cs` copies carry that identical default (verified:
`grep -h '_source = new Uri' $(find tests -name DefaultMessageBuilder.cs)` yields 17 identical lines).
Every other transport receives such a message without complaint. **Azure Service Bus is the only one
that throws** — and that asymmetry is what identifies the gateway, not the builder, as the defective
side.

**Refutation test.** Map a `BrokeredMessage` carrying a relative `cloudEvents:source` through
`AzureServiceBusMessageCreator.MapToBrighterMessage`. If it throws `UriFormatException`, the
hypothesis holds. If it maps cleanly, it is refuted.

## Confirmed Root Cause

`AzureServiceBusMessageCreator.cs:331` — `return new Uri(source!);` — binds the single-argument
`System.Uri(string)` constructor, which defaults to `UriKind.Absolute` and throws `UriFormatException`
for any value that is not an absolute URI. The same defect exists verbatim at
`AzureServiceBusMessageCreator.cs:173` (`return new Uri(dataSchema);`). CloudEvents defines both
`source` and `dataschema` as URI-*references*, which may be relative; the guard clauses above each
`new Uri` cover only *absent* and *empty*, never *present-but-relative*.

**Azure Service Bus is the only gateway in `src/` that does this.** Every other transport already uses
`Uri.TryCreate(..., UriKind.RelativeOrAbsolute, ...)`.

## Evidence

- [x] **Code-trace**
- [x] **Red repro specified** (pure logic, no broker needed)

1. **Unconditional reach.** `MapToBrighterMessage` calls `GetSource` at `AzureServiceBusMessageCreator.cs:85`
   and `GetCloudEventsDataSchema` at `:88`, before the `MessageHeader` is built at `:99`. The only early
   return is the null-message guard at `:52-56` (a null *body* only logs, `:58-61`). Every non-null
   received message hits both.
2. **Consumer path.** `AzureServiceBusConsumer.cs:219` calls `MapToBrighterMessage` inside the `foreach`
   over received messages; the exception escapes `ReceiveAsync`, matching the CI stack frame.
3. **`GetSource` (309-332).** `:313-317` missing key -> default; `:323-327` `property is not string ||
   IsNullOrEmpty` -> default; `:329-331` otherwise `new Uri(source!)` — absolute-only, no try, no catch.
   Key: `ASBConstants.cs:17` `cloudEvents:source`.
4. **`GetCloudEventsDataSchema` (151-174).** Identical shape; `:173` `new Uri(dataSchema)`. Key:
   `ASBConstants.cs:16` `cloudEvents:schema`. Note there is **no** `property is not string` guard here,
   unlike `GetSource`.
5. **The wire value really is relative.** `DefaultMessageBuilder.cs.liquid:64` —
   `new Uri(Uuid.NewAsString(), UriKind.Relative)`. `src/Paramore.Brighter/Uuid.cs:65-72` returns a bare
   GUID string with no scheme, so it can *never* parse as absolute — the throw is deterministic, not
   probabilistic.
6. **"17 identical builders" — VERIFIED.** 17 files, 17 byte-identical lines (`uniq -c` = 17 x 1 distinct).
7. **Write side is faithful for `source`.** `AzureServiceBusMessagePublisher.cs:87` writes
   `Header.Source?.ToString()`; `Uri.ToString()` is legal on a relative Uri and yields the bare string,
   which then passes `GetSource`'s `is not string` guard and reaches the throwing line. Write-OK /
   read-broken, exactly as hypothesised.

**Red repro (to be written by /bugfix:test).** Follow `When_A_Deferred_Message_Is_Redelivered.cs`
(same `[Trait("Category", "ASB")]`, same `AzureServiceBusSubscription<ASBTestCommand>` construction).
`ASBConstants` is `internal` with **no `InternalsVisibleTo`**, so the test must use string literals.
Arrange a `BrokeredMessage` (test double confirmed to expose everything needed) with
`ApplicationProperties`: `{"MessageType","MT_COMMAND"}`, `{"cloudEvents:source", relativeSource.ToString()}`
where `relativeSource = new Uri(Uuid.NewAsString(), UriKind.Relative)`, and `{"cloudEvents:schema","/schemas/v1"}`.
Act: `_creator.MapToBrighterMessage(received)`.

**Fails TODAY at the Act line**, not at an assertion: `UriFormatException` from `System.Uri..ctor(String)`
at `:331` via `:85`. (Remove the source entry and it fails identically at `:173` via `:88`.)
Green-after-fix assertions: `Header.Source` equals the relative URI, `IsAbsoluteUri` is false, and
`Header.DataSchema` equals `new Uri("/schemas/v1", UriKind.Relative)`.

## Scope Notes

### ⚠️ FOURTH DEFECT — CONFIRMED on the WRITE side: `AzureServiceBusMessagePublisher.cs:98`

```csharp
if (message.Header.DataSchema is not null)
    azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsSchema] = message.Header.DataSchema;
```

This assigns the `Uri` **object**, not `.ToString()` — asymmetric with line 87 for `source`. A `Uri` is a
supported ApplicationProperties type, but the SDK serialises it via `Uri.AbsoluteUri`. Verified by
disassembling the pinned SDK (`Directory.Packages.props:37` -> `Azure.Messaging.ServiceBus 7.20.2`):
`AmqpMessageConverter::TryGetAmqpObjectFromNetObject`, IL_0096-IL_00b0 — `ldstr "com.microsoft:uri"` …
`castclass System.Uri` … `callvirt instance string System.Uri::get_AbsoluteUri()`. And
`Uri.AbsoluteUri` throws `InvalidOperationException: This operation is not supported for a relative URI`.

⇒ **Publishing any message whose `Header.DataSchema` is relative throws at send time**, before it ever
reaches a broker. It has not fired yet only because the generated builders default `_dataSchema` to an
*absolute* URI (`DefaultMessageBuilder.cs.liquid:54`). **Any regression test that round-trips a relative
dataschema through the real publisher will hit this.** Fix: `.ToString()` at line 98, mirroring line 87.

*Corollary:* because the SDK reads `"com.microsoft:uri"` back with `UriKind.RelativeOrAbsolute`,
`property` arrives as a `Uri`, so `property.ToString()` at `:165` yields the *unescaped* form of what was
written as the *escaped* `AbsoluteUri` — a silent escaping round-trip loss for any dataschema containing
reserved characters. Also `AzureServiceBusMessageCreator.cs:124` copies the raw `Uri` object (not a
string) into `Header.Bag["cloudEvents:schema"]`, unlike every other bag value.

### Dead null-guard
`AzureServiceBusMessagePublisher.cs:97`'s `is not null` can never be false for a message that came from
`MapToBrighterMessage`, because `GetCloudEventsDataSchema` never returns null (`:162,170,173`). ASB
therefore fabricates and re-publishes a `http://goparamore.io` dataschema on every requeue.

### Cross-backend parity — NO other transport has this bug
Exhaustive scan of `src/` for single-argument `new Uri(...)` applied to wire data found exactly two
hazardous sites, both ASB (`:331`, `:173`). Every other `new Uri(` in `src/` takes a compile-time
constant or a config value. The house pattern is `Uri.TryCreate(..., UriKind.RelativeOrAbsolute, ...)`:
`RmqMessageCreator.cs:326/:360` (Async) and `:324/:358` (Sync), `SqsMessageCreator.cs:142/:174`
(+`.V4` `:139/:172`), `SqsInlineMessageCreator.cs:282,288,299,306`, `GcpPubSub/Parser.cs:242/:253`,
`KafkaMessageCreator.cs:364/:370`, `RedisMessageCreator.cs:237/:327`, `RocketMessageConsumer.cs:447/:475`,
`RelationDatabaseOutbox.cs:1917`, `Outbox.MongoDb/OutboxMessage.cs:274,290`,
`Outbox.DynamoDB/MessageItem.cs:276-277`, `Outbox.Firestore/FirestoreOutbox.cs:1418,1441`.
MQTT is safe by a different route: it round-trips the whole `Message` through `JsonSerializer`
(`MQTTMessageConsumer.cs:110`), and `System.Text.Json`'s `UriConverter` parses `RelativeOrAbsolute`.

### `RelativeOrAbsolute` can still fail — the fallback is NOT dead code
It fails on null input, inputs over 65519 chars, and malformed-but-scheme-bearing strings such as
`"http://"` (a scheme forces absolute parsing). So the `out` value must not be trusted unconditionally.

### Downstream impact of a relative `Header.Source` — SAFE
Every consumer goes through `Uri.ToString()`, which is legal on a relative Uri
(`AzureServiceBusMessagePublisher.cs:87`, `RmqMessagePublisher.cs:156/:168`, `SnsMessagePublisher.cs:127`,
`SqsMessageSender.cs:147`, `GcpPubSub/Parser.cs:310`, `RedisMessagePublisher.cs:155`,
`KafkaDefaultMessageHeaderBuilder.cs:91`, `RocketMqMessageProducer.cs:84`, `RelationDatabaseOutbox.cs:1456`,
`Outbox.MongoDb/OutboxMessage.cs:45`, `Outbox.DynamoDB/MessageItem.cs:254`, `FirestoreOutbox.cs:1283`).
`BrighterTracer.cs:180,290,1059` passes the `Uri` as an activity tag object. `CloudEventsTransformer.cs:225,287`
only does reference/equality comparison. **No `.AbsoluteUri`, `.Host` or `.Scheme` access on
`Header.Source` exists anywhere in `src/`.**

### Two decisions the fix needs (see Suggested-Fix Assessment)
1. **`dataSchema` fallback.** Every other backend yields **`null`** for an unparseable/absent dataschema
   (`MessageHeader.DataSchema` is `Uri?`, `MessageHeader.cs:200`). ASB instead fabricates
   `http://goparamore.io` (`:153,162,170`), which is not the documented semantic —
   `MessageHeader.cs:90` `DefaultSource` is the default for **`Source`** (`:303`), not `DataSchema`.
   Changing it to `null` is the correct target but is a **behaviour change** beyond the minimal fix.
2. **The hard-coded literal.** `:153` and `:311` write `new Uri("http://goparamore.io")` rather than
   `new Uri(MessageHeader.DefaultSource)`. Every other site uses the constant. This matters
   behaviourally for `JustSayingTransform.cs:256`, which does `message.Header.Source != s_defaultSource`
   — an identity that holds only because the literals happen to match today.

### Out of scope, worth its own issue
`src/Paramore.Brighter/RelationDatabaseOutbox.cs:1814` reads `DataSchema` with `UriKind.Absolute`, so a
relative dataschema stored in a relational Outbox is silently dropped to `null` on read — inconsistent
with `:1917` in the same file and with every other Outbox. It loses data rather than throwing, so it is
a separate, lower-severity bug.

### Relationship to #4309
Genuinely disjoint. #4309 blocks 26 of the 30 generated ASB failures upstream of receive, which is why
only the two delayed-message tests ever reached `MapToBrighterMessage` and surfaced this defect.
**Do not expect the generated ASB suite to go green on this fix alone.**

## ✅ APPROVED FIX SCOPE (user decision, 2026-09-07 — Confirm gate)

All four parts approved, plus the adjacent Outbox bug. **This is the agreed scope; do not narrow it.**

1. **Read side (core).** `AzureServiceBusMessageCreator.GetSource` (`:331`) and
   `GetCloudEventsDataSchema` (`:173`) — use `Uri.TryCreate(..., UriKind.RelativeOrAbsolute, out ...)`,
   the house pattern, with a fallback on failure (the fallback is reachable: null, >65519 chars, and
   malformed-but-scheme-bearing strings like `"http://"`).
2. **Write side.** `AzureServiceBusMessagePublisher.cs:98` — `.ToString()`, mirroring line 87. Without
   this, publishing a relative `DataSchema` throws `InvalidOperationException` inside the SDK.
3. **Use the constant.** Replace `new Uri("http://goparamore.io")` at `:153` and `:311` with
   `MessageHeader.DefaultSource`. Matters for `JustSayingTransform.cs:256`, which compares against it.
4. **`dataSchema` fallback -> `null`** (⚠️ **deliberate behaviour change, explicitly approved**). Brings
   ASB into line with every other backend. `GetCloudEventsDataSchema`'s return type must become `Uri?`;
   `MessageHeader.DataSchema` is already `Uri?` (`MessageHeader.cs:200`). **Side effect to verify:** the
   "dead null-guard" at `AzureServiceBusMessagePublisher.cs:97` becomes live and correct — ASB will stop
   fabricating and re-publishing a `http://goparamore.io` dataschema on every requeue.
5. **Also fix `src/Paramore.Brighter/RelationDatabaseOutbox.cs:1814`** — read `DataSchema` with
   `UriKind.RelativeOrAbsolute`, matching `:1917` in the same file and every other Outbox. Approved as
   part of this change rather than a separate issue.

## Suggested-Fix Assessment

**PARTIAL** — `Uri.TryCreate(..., UriKind.RelativeOrAbsolute, ...)` with a fallback is the right house
pattern and the fallback is genuinely reachable, but the proposal is incomplete: it misses the write-side
defect at `AzureServiceBusMessagePublisher.cs:98`, and it leaves the two decisions above unmade.

## Regression Test

Four tests, each red for a distinct reason before the fix and green after. All are broker-free.

| test | failed before with |
| --- | --- |
| `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_receiving_a_message_with_relative_cloud_events_uris_should_preserve_them.cs` | `UriFormatException` at `AzureServiceBusMessageCreator.cs:331` — thrown at the **Act** line, reproducing the CI stack locally |
| `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_publishing_a_message_with_a_relative_dataschema_should_write_it_as_a_string.cs` | `Assert.IsType()` — `Expected: typeof(string)`, `Actual: typeof(System.Uri)` |
| `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_receiving_a_message_with_no_dataschema_should_leave_it_null.cs` | `Assert.Null()` — `Expected: null`, `Actual: http://goparamore.io/` |
| `tests/Paramore.Brighter.Sqlite.Tests/Outbox/When_storing_a_message_with_a_relative_dataschema_should_read_it_back.cs` | `Assert.Equal() Failure: Values differ` |

⚠️ **The write-side test asserts the observable contract, not the throw.** The SDK's `Uri.AbsoluteUri`
failure happens deep inside AMQP conversion at *send* time, which no test can reach without a broker.
Asserting that the property goes on the wire as a `string` pins exactly what the fix changes, and it
failed today on a real type mismatch.

📌 The Sqlite test is deliberately **not** under `Outbox/Text/Generated/` — a hand-written test inside
the generator's output tree is the trap #4306 exists to fix.

## Fix

Five changes, all in the approved scope.

1. `AzureServiceBusMessageCreator.GetSource` — `Uri.TryCreate(sourceString, UriKind.RelativeOrAbsolute,
   out var uri) ? uri : defaultSourceUri`. Also now reads `sourceString` (already narrowed to `string`
   by the guard above) rather than re-calling `property.ToString()`.
2. `AzureServiceBusMessageCreator.GetCloudEventsDataSchema` — return type widened to `Uri?`; all three
   paths (missing, empty, unparseable) now yield **`null`** instead of a fabricated
   `http://goparamore.io`. ⚠️ Deliberate behaviour change, explicitly approved at the Confirm gate.
3. `AzureServiceBusMessagePublisher` (the `CloudEventsSchema` assignment) — `.ToString()`, mirroring
   how `source` is written two lines above.
4. `RelationDatabaseOutbox` `DataSchema` reader — `UriKind.Absolute` -> `UriKind.RelativeOrAbsolute`,
   matching the `Source` reader in the same file and every other Outbox.
5. `GetSource`'s default is now `new Uri(MessageHeader.DefaultSource)` rather than a hard-coded
   `"http://goparamore.io"` literal. Structural only — folded into this commit by user decision.
   The literal in `GetCloudEventsDataSchema` disappeared entirely with change 2.

**Side effect confirmed:** the previously-dead null-guard on the publisher's dataschema assignment is
now live, so ASB no longer fabricates and re-publishes a `http://goparamore.io` dataschema on requeue.

### Verification

| suite | result |
| --- | --- |
| the 4 new regression tests | **green** |
| `Paramore.Brighter.Core.Tests` | Passed 978, Failed 0, Skipped 7 |
| `Paramore.Brighter.Sqlite.Tests` | Passed 153, Failed 0 |
| `Paramore.Brighter.InMemory.Tests` | Passed 153, Failed 0 |
| `Paramore.Brighter.AzureServiceBus.Tests` (`Category=ASB`) | Passed 11, Failed 10 |

⚠️ Those 10 ASB failures are **pre-existing and environmental**, not caused by this change: every one
fails in ~1 ms in its constructor with
`System.Exception : ASB ConnectionString or Namespace not set not set` (`ASBCreds.cs:19`) because no
local ASB credentials are set. The 11 that pass are the broker-free mapping tests, which include all
three new ASB tests.

⚠️ **Not yet verified against a real broker.** #4309 still blocks the generated ASB suite upstream of
receive, so CI cannot exercise this fix end-to-end until #4309 is also fixed.
