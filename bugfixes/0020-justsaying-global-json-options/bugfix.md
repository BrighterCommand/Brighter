# Bugfix: JustSaying `RegisterConverters` races the freeze of the global `JsonSerialisationOptions.Options`

**Linked Issue**: none (no GitHub issue raised yet)
**Status**: Verified
**Scope approved by the user 2026-09-09**: FULL. Fix per-adaptor `JsonSerializerOptions`, cover **both**
`Register()` call sites (`JustSayingMessageMapper.cs:37` and `JustSayingAttribute.cs:33`), and add
regression tests for the outbound wire shape, the inbound parse, and the `JustSayingTransform` tenant
data loss.

## Symptom

Running the whole `tests/Paramore.Brighter.Transforms.Adaptors.Tests` project on **net9.0** fails one test:

```
JustSaying.JustSayingMessageMapperTest.MapToMessage_when_mapping_a_command_with_partial_justsaying_property_should_set_from_request_context [FAIL]
System.InvalidOperationException : This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.
   at System.Text.Json.JsonSerializerOptions.VerifyMutable()
   at System.Text.Json.JsonSerializerOptions.ConverterList.OnCollectionModifying()
   at System.Text.Json.Serialization.ConfigurationList`1.Add(TItem item)
   at Paramore.Brighter.Transformers.JustSaying.JsonConverters.RegisterConverters.Register() … RegisterConverters.cs:line 27
   at Paramore.Brighter.Transformers.JustSaying.JustSayingMessageMapper`1..ctor() … JustSayingMessageMapper.cs:line 37
```

Failed: 1, Passed: 29, Total: 30 on net9.0; **net10.0 green on the same CI run** (run `34385066249`,
job `102579157982`, PR #4325 — whose own diff is 5 deleted lines in `RocketMqMessageProducer.cs` and
so cannot be the cause).

**Expected**: constructing a `JustSayingMessageMapper<T>` never throws, regardless of whether anything
else in the process has already serialized through Brighter's global `JsonSerializerOptions`.

**Reproduction**
1. `dotnet test tests/Paramore.Brighter.Transforms.Adaptors.Tests/Paramore.Brighter.Transforms.Adaptors.Tests.csproj -f net9.0` — reproduces 3/3 locally.
2. Same test with `--filter FullyQualifiedName~MapToMessage_when_mapping_a_command_with_partial_justsaying_property` — **passes**.

So the trigger is cross-test-class concurrency, not the test body. The project has **no**
`xunit.runner.json` (only `Paramore.Brighter.Core.Tests`, `MSSQL`, `PostgresSQL` and the three
`Validation.*` projects have one), **no** `[Collection]` attribute and **no** assembly-level
`CollectionBehavior`/`DisableTestParallelization`. xUnit v2 therefore places each of the six test
classes in its own collection and runs them in parallel up to the core count:

| Test class | Serializes through `JsonSerialisationOptions.Options` | Constructs `JustSayingMessageMapper<T>` |
|---|---|---|
| `tests/Paramore.Brighter.Transforms.Adaptors.Tests/JustSaying/JustSayingMessageMapperTest.cs:11` | yes (e.g. :30, :217) | yes (:16, :45, :76, :105, :148, :191, :243, :299) |
| `…/JustSaying/JustSayingPropertySerializationTests.cs:13` | yes (via mapper) | yes (:19, :66) |
| `…/JustSaying/JustSayingCompressionTransformTest.cs:13` | yes, **directly and without any mapper** (:28, :36, :48, :62, :73) | no |
| `…/JustSaying/JustSayingTransformTest.cs:9` | yes, via `JustSayingTransform.Wrap` → `src/Paramore.Brighter.Transformers.JustSaying/JustSayingTransform.cs:119` | no |
| `…/MassTransit/MassTransitMessageMapperTest.cs:10` | yes (:88, :118, :146; also `MassTransitMessageMapper.cs:113/202/205`) | no |
| `…/MassTransit/MassTransitTransformTest.cs:10` | yes (:29, :86, :140, :198; also `MassTransitTransform.cs:103/125/189/191/243`) | no |

Four of the six classes touch the shared options **without** going through `RegisterConverters`, so
they can freeze the options at an arbitrary point while a JustSaying mapper is being constructed on
another thread.

## Suspected Location

- `src/Paramore.Brighter.Transformers.JustSaying/JsonConverters/RegisterConverters.cs:25-29` — the
  guard-then-mutate block. `:25` reads `IsReadOnly`, `:27`/`:28` mutate; the `lock` at `:17` excludes
  only other `Register()` callers, **not** the `JsonSerializer` calls that actually freeze the options.
  `s_register = true` at `:31` is only reached on success, so a throw leaves the flag `false` and every
  later mapper construction re-enters the body.
- `src/Paramore.Brighter.Transformers.JustSaying/JustSayingMessageMapper.cs:35-38` — the mapper
  constructor calls `RegisterConverters.Register()`, so the mutation is tied to *first mapper
  instantiation* rather than to assembly/DI startup.
- `src/Paramore.Brighter/JsonConverters/JsonSerialisationOptions.cs:21-47` —
  `public static JsonSerializerOptions Options { get; set; }`, a process-global, publicly settable,
  mutable static built once in the static ctor. 41 call sites in `src/Paramore.Brighter` alone read it
  (inbox, outbox, scheduler, mediator: e.g. `InMemoryInbox.cs:165`, `RelationDatabaseOutbox.cs:1438`,
  `Scheduler/Handlers/FireSchedulerRequestHandler.cs:62`).
- `src/Paramore.Brighter/MessageMapperRegistry.cs:82-99` (`Get<TRequest>`) and `:122-137`
  (`GetAsync<TRequest>`) — the mapper is created **per call** via
  `_messageMapperFactory.Create(messageMapperType)`, driven from
  `src/Paramore.Brighter/TransformPipelineBuilder.cs:330-332` (`FindMessageMapper<TRequest>`). This is
  the product path that puts `RegisterConverters.Register()` on the hot dispatch path rather than at
  startup.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:788-794` —
  `ConfigureJsonSerialisation` mutates the same global via
  `configure.Invoke(JsonSerialisationOptions.Options)`; same latent hazard, lower risk because it
  normally runs at DI-build time.

## Root-Cause Hypothesis

**Hypothesis.** `RegisterConverters.Register()` performs an unsynchronisable check-then-act on a
process-global `JsonSerializerOptions` that a *different* thread can freeze between the check and the
act. `System.Text.Json` freezes the options (`MakeReadOnly`) on first (de)serialization;
`JsonSerialisationOptions.Options` is shared by the entire process. The `lock (s_lock)` at
`RegisterConverters.cs:17` serialises `Register()` against itself but provides no mutual exclusion
against `JsonSerializer.Serialize/Deserialize(…, JsonSerialisationOptions.Options)`. With six xUnit
collections running in parallel and no `xunit.runner.json` to serialise them, a
`MassTransit*`/`JustSayingCompressionTransformTest`/`JustSayingTransformTest` collection freezes the
options in the window between `:25` (guard reads `IsReadOnly == false`) and `:27` (`Converters.Add`),
and the `Add` throws out of the `JustSayingMessageMapper<T>` constructor. The `IsReadOnly` guard is not
"broken" — a standalone probe confirmed `options.IsReadOnly` and `options.Converters.IsReadOnly` flip
to true together on first serialization, so the guard reads the state correctly — it is simply **not
atomic** with respect to the mutation it guards.

There are two failure arms, both real:

- **Arm (a) — throw.** Guard is false, `Add` races the freeze → `InvalidOperationException` escapes
  `JustSayingMessageMapper<T>..ctor()`. This is the observed net9.0 failure. Because the throw happens
  before `s_register = true` (`:31`), the flag stays `false` and the failure is retried, not latched.
- **Arm (b) — silent skip.** Guard is true (options already frozen) → the `if` body is skipped
  entirely, `s_register` is set to `true` at `:31`, and `TenantConverter`/`IpAddressConverter` are
  **never** globally registered. `TenantConverter.Write` emits `Tenant` as a flat JSON string and
  `IpAddressConverter.Write` emits `IPAddress` as a flat string. Without them, `Tenant` — a
  `readonly record struct Tenant(string Value)` at
  `src/Paramore.Brighter.Transformers.JustSaying/Tenant.cs:12` — serializes as the object
  `{"value":"…"}`, and `IPAddress` as an object, both wire-incompatible with JustSaying.

  **Arm (b) is currently masked for the library's own base types**: `JustSayingCommand.cs:59`/`:63` and
  `JustSayingEvent.cs:60`/`:64` already carry property-level
  `[JsonConverter(typeof(IpAddressConverter))]` / `[JsonConverter(typeof(TenantConverter))]`, and
  property attributes beat the options' converter list. That is why `JustSayingPropertySerializationTests`
  (asserting `Assert.IsAssignableFrom<JsonValue>(tenantNode)` at `:46`, `:52`, `:93`, `:99`) still
  passes in the failing run — 29 passed, only the constructor throw failed. The residual arm-(b)
  exposure is **user types that implement `IJustSayingRequest` themselves**: the interface declares
  `Tenant? Tenant` (`IJustSayingRequest.cs:36`) and `IPAddress? SourceIp` (`:31`) **without**
  `[JsonConverter]`, and `System.Text.Json` does not inherit attributes from interface members, so such
  a type silently gets the wrong wire shape.

**Product path is affected, not just tests.** `MessageMapperRegistry.Get<TRequest>()`
(`MessageMapperRegistry.cs:97`) creates the mapper per call through the factory, invoked from
`TransformPipelineBuilder.FindMessageMapper<TRequest>` (`TransformPipelineBuilder.cs:332`). In any
normal Brighter application the outbox, inbox, scheduler and mediator (41 call sites in
`src/Paramore.Brighter`) will have serialized through `JsonSerialisationOptions.Options` long before the
first JustSaying message is mapped — so the first `JustSayingMessageMapper<T>` constructed at runtime
hits arm (b) deterministically, and hits arm (a) if it happens to construct concurrently with the very
first serialization.

**Scope check (no widening found).** Grepping `src`, `tests` and `samples` for
`JsonSerialisationOptions.Options.Converters`, `JsonSerialisationOptions.Options =` and
`Options.TypeInfoResolver`, the **only** production mutator of the converter list is
`RegisterConverters.cs:25-28`. `Paramore.Brighter.Transformers.MassTransit` only *reads* the global
options (`MassTransitMessageMapper.cs:113/202/205`, `MassTransitTransform.cs:103/125/189/191/243`);
there is no `Paramore.Brighter.Transformers.CloudEvents` project (the transformer projects are AWS,
AWS.V4, Azure, Gcp, JustSaying, MassTransit, MongoGridFS). Two **test-only** writers exist and matter
for the fix's blast radius:
`tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_Converting_A_Message_With_A_Session_Id.cs:43`
and `:69` swap `JsonSerialisationOptions.Options` wholesale, and
`tests/Paramore.Brighter.Extensions.Tests/When_configuring_json_serialisation.cs:39/56` pins
`Converters.Count`.

**net10.0 vs net9.0 — observation, not proof.** There is no evidence of a framework difference in
*when* `JsonSerializerOptions` freezes; both .NET 9 and .NET 10 freeze on first use, and the probe was
only run on net9.0. The likely explanation is plain scheduling: the race window is a few instructions
wide and whether it is hit depends on thread interleaving, core count and JIT/startup timing. This is
consistent with the bug being intermittent everywhere (`master` `4b328fbc3` and PR #4297 both had green
`build` jobs) — net10.0 simply lost the coin toss less often on that run.

**What would refute this hypothesis:**
- The failure still reproduces with the adaptors test project forced single-threaded (an
  `xunit.runner.json` with `"parallelizeTestCollections": false`, or all six classes in one
  `[Collection]`). If it still fails with no concurrency, the cause is ordering-only — a class that
  freezes the options runs *before* `JustSayingMessageMapperTest` on one thread — which would mean arm
  (b), not arm (a), and the stack would not show a throw at `RegisterConverters.cs:27`.
- A failing stack showing a line other than `RegisterConverters.cs:27`/`:28`, or `Register()` being
  reached with `s_register == true`.
- Evidence that another component outside the six test classes mutates or replaces
  `JsonSerialisationOptions.Options` within this test assembly — the grep says none does.
- A demonstration that `JsonSerializerOptions` in .NET 9 freezes at a point *not* triggered by
  serialization.

## Confirmed Root Cause

`src/Paramore.Brighter.Transformers.JustSaying/JsonConverters/RegisterConverters.cs:25-31` reads
`JsonSerialisationOptions.Options.Converters.IsReadOnly` at `:25` and mutates at `:27`/`:28`.
System.Text.Json flips `JsonSerializerOptions.IsReadOnly` — and with it `Converters.IsReadOnly`, they
are the same flag — on the **first** `Serialize`/`Deserialize` through that instance. The `lock (s_lock)`
at `:17` gives mutual exclusion only against other `Register()` callers; none at all against the **170
references across 70 files** in `src/` that serialize through the same static instance. Three reachable
outcomes, **all observed**:

- **(a) throw** — guard read `false`, another thread froze, `Add` threw `InvalidOperationException` out
  of the mapper constructor. `s_register` stays `false` (`:31` not reached).
- **(a′) PARTIAL registration — NOT in the triage** — `:27` succeeded (`TenantConverter` in, count
  12→13), the freeze landed, `:28` threw. The process is left permanently with `TenantConverter`
  registered and `IpAddressConverter` NOT, `s_register == false`, options frozen forever.
  Reproduced 14/120 runs.
- **(b) silent skip** — guard read `true`, body skipped, `s_register = true` set anyway at `:31`.
  Neither converter is ever registered.

⭐ **The decisive finding: arm (a) is a ONE-SHOT symptom; arm (b) is the PERSISTENT state.** After a
throw the options are already frozen, so the *next* mapper construction takes the guard-true path,
skips, and latches `s_register = true`. There is exactly one throw per process, after which the process
degenerates permanently into arm (b). **The fix must target arm (b). Making the throw go away is not a
fix — it is the bug getting quieter.**

Arm (b) is worse than triage claimed. Not merely a wire-shape skew:
- `tenant` serializes as `{"value":"acme"}` instead of `"acme"` — breaks JustSaying interop, and is then
  *destroyed* by `JustSayingTransform.SetTenant` (see Scope Notes 3).
- A non-null `SourceIp` **hard-crashes** the outbound path: `SocketException (45): Operation not
  supported` from `IPAddress.get_ScopeId()`.
- The inbound path (`MapToRequest`) throws `JsonException: The JSON value could not be converted to
  System.Net.IPAddress` on a genuine JustSaying wire payload.

**Production ordering makes arm (b) deterministic, not racy**: the outbox/inbox/scheduler/mediator
serialize through the global long before the first mapper is constructed.

## Evidence

- [x] **Code-trace**
  - `RegisterConverters.cs:17` `lock (s_lock)`; `:25` the guard; `:27`/`:28` the mutations; `:31`
    `s_register = true` — **outside** the `if`, so it latches on the skip path and is bypassed on throw.
  - `src/Paramore.Brighter/JsonConverters/JsonSerialisationOptions.cs:21` — process-global, publicly
    **settable**, mutable instance, seeded with 12 converters in the static ctor.
  - `src/…/JustSayingMessageMapper.cs:37` — ctor calls `Register()`.
  - ⭐ **`src/…/JustSayingAttribute.cs:33` — a SECOND call site the triage missed.** Attribute instances
    are materialised by reflection inside `TransformPipelineBuilder.FindWrapTransforms`
    (`src/Paramore.Brighter/TransformPipelineBuilder.cs:337-352`), so the same exception can escape from
    *pipeline* construction, not just the mapper ctor. A fix applied only at `JustSayingMessageMapper.cs:37`
    leaves this live.
  - **Per-dispatch construction confirmed.** `TransformPipelineBuilder.cs:100`/`:141` →
    `FindMessageMapper<TRequest>()` (`:330`) → `MessageMapperRegistry.Get<TRequest>()` →
    `_messageMapperFactory.Create(...)` (`MessageMapperRegistry.cs:97`, async `:137`). **No mapper
    instance cache** — only `s_wrapTransformsMemento`/`s_unWrapTransformsMemento` (attribute arrays keyed
    by mapper type) are memoised. DI registers mappers `Transient`
    (`ServiceCollectionMessageMapperRegistryBuilder.cs:80`) and `BrighterOptions.MapperLifetime` defaults
    to `Transient` (`BrighterOptions.cs:52`), so a new mapper — and a `Register()` call — happens on
    every `MapMessage` (`OutboxProducerMediator.cs:1248`, async `:1312`).

- [x] **Red repro — in the repo's own suite**

  `dotnet test tests/Paramore.Brighter.Transforms.Adaptors.Tests/… -f net9.0` — 6 consecutive runs:
  **Failed 3, Passed 3.** Failing runs are `Failed: 1, Passed: 29, Total: 30`, always the same test, with
  a stack byte-identical to the reported one (`RegisterConverters.cs:line 27` ← `JustSayingMessageMapper`1..ctor()`).

- [x] **Red repro — isolating probe** (throwaway console app in the scratchpad, `net9.0`,
  `ProjectReference` to the JustSaying assembly; reads private `s_register` by reflection)

  1. **The flags flip together** — the guard reads correct state, it is just not atomic:
     `BEFORE: IsReadOnly=False count=12 s_register=False` → `AFTER SERIALIZE: IsReadOnly=True count=12 s_register=False`.
  2. ⭐ **Serialize FIRST, then construct the mapper (production ordering) does NOT throw** — the guard
     catches it and it becomes arm (b): `MAPPER CTOR: OK`, `count=12`, `s_register=True`; `SECOND CTOR: OK`.
     Control (mapper first) gives `count=14`. **So the real production failure mode is arm (b), not arm (a).**
  3. **Arm (a) reproduced** by racing a serializer thread against the mapper thread with a tuned start
     offset, warmed JIT, 25 processes per point:
     `spinSer=0 → throws=0`; `spinSer=5000 → throws=24`; `spinSer=10000 → throws=24`;
     `spinSer=20000 → throws=9, ok14=16`; `spinSer=80000 → throws=0, ok14=25`.
  4. **Arm (a′) reproduced** — 14/120 runs at `spinSer=20000`: throw at `RegisterConverters.cs:line 28`,
     leaving `count=13 s_register=False`, options frozen.
  5. **`s_register` is not latched on throw, and the retry does not throw** — but the consequence is the
     opposite of "it is retried": `RETRY CTOR AFTER THROW: OK`, `count=12`, `s_register=True`. It retries
     straight into arm (b).
  6. **Arm (b) outbound, direct `IJustSayingRequest` implementor, options frozen first:**
     `DIRECT SERIALIZE THREW: SocketException: Operation not supported`; with `SourceIp` null,
     `{…,"sourceIp":null,"tenant":{"value":"acme"},…}`. Control with converters registered:
     `{…,"sourceIp":"10.1.2.3","tenant":"acme",…}`. **The shapes differ — a real wire-format defect, and a
     hard crash when `SourceIp` is non-null.**
  7. **Arm (b) inbound:** a genuine JustSaying payload → `JsonException: The JSON value could not be
     converted to System.Net.IPAddress. Path: $.sourceIp`.
  8. **Masking claim confirmed** — byte-identical output either way for attribute-carrying types
     (`count=12` vs `count=14` both give `"sourceIp":"10.1.2.3","tenant":"acme"`). `JustSayingCommand.cs:59/:63`
     and `JustSayingEvent.cs:60/:64` carry the property-level attributes, which beat the options list.
     That is why 29 of 30 tests pass.
  9. ⭐ **STJ does NOT inherit `[JsonConverter]` from interface property declarations** — an interface
     declaring both attributes, implemented by a plain class, serialized with bare options:
     `IFACE-ATTR THREW: SocketException` — the attributes were ignored entirely. **Decisive against fix
     option (iii).**

## Scope Notes

1. ⭐ **`src/…/JustSayingAttribute.cs:33` — second `Register()` call site**, missed by triage. Runs during
   attribute materialisation in `TransformPipelineBuilder.FindWrapTransforms`
   (`TransformPipelineBuilder.cs:337-352`). Same throw, different escape point. **Any fix must cover both.**
2. **Arm (a′) partial registration** leaves an asymmetric, un-repairable state (`count=13`, frozen).
   Neither the triage nor any test contemplates it.
3. ⭐ **`JustSayingTransform` amplifies arm (b) into silent tenant DATA LOSS.**
   `src/…/JustSayingTransform.cs:227` does `node[…"Tenant"] = GetTenant(node.GetString("Tenant"))`, and
   `src/…/Extensions/JsonNodeExtensions.cs:9-19` `GetString` returns `null` unless
   `GetValueKind() == JsonValueKind.String`. Under arm (b) a direct implementor serialises
   `"tenant":{"value":"acme"}`, so `GetString` returns null and the transform **overwrites the tenant**
   with the attribute/context value or `null`. Multi-tenant routing silently loses the tenant.
4. **`s_register` latches against no particular options instance, and `JsonSerialisationOptions.Options`
   has a public setter** (`JsonSerialisationOptions.cs:21`). Any app assigning a fresh options instance
   after `Register()` has run loses the JustSaying converters permanently — `s_register == true`
   short-circuits at `RegisterConverters.cs:11`. Not hypothetical:
   `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_Converting_A_Message_With_A_Session_Id.cs:43`
   does exactly that (restored at `:69`).
5. **`ConfigureJsonSerialisation` is the same class of bug in core.**
   `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:788-793` does
   `configure.Invoke(JsonSerialisationOptions.Options)` **with no freeze guard at all** — if anything has
   already serialized, the user's callback throws from the DI configuration path. Out of scope here;
   **worth a follow-up issue.**
6. **Scale correction.** Triage said "41 call sites". Actual: **170 references across 70 files** in `src/`.
   The race surface is four times larger than stated.
7. **`RegisterConverters.cs:25-28` really is the only production mutator** of the converter list. The only
   other `Converters.Add` in `src/` outside the static ctor is
   `src/Paramore.Brighter.AsyncAPI.NJsonSchema/NJsonSchemaGenerator.cs:101`, on a *local* options object.
8. **Both test-only writers re-verified.** `When_configuring_json_serialisation.cs:39/56` reads
   `converterCountBefore` at runtime rather than hard-coding 12, so it is insensitive to whether the
   JustSaying converters are present.
9. ⭐ **No `IJustSayingRequest` implementor exists outside the JustSaying assembly and its tests** — no
   samples, no benchmarks. **Arm (b) is invisible to the repo's own suite**; only a new direct-implementor
   test catches it. Conversely **no existing test depends on the converters being in the global options**:
   the `JustSayingPropertySerializationTests` types derive from `JustSayingCommand`/`JustSayingEvent`
   (attribute-masked), and `WithPartialJustSayingProperty` (`JustSayingMessageMapperTest.cs:358-367`)
   declares `public string? Tenant` — a plain `string`, not the `Tenant` struct. **The very test that fails
   does not need the converters at all.**

## Fix Direction (recommendation, not a decision)

- **(iii) attributes on the interface — RULED OUT by evidence 9.** STJ ignores `[JsonConverter]` on
  interface properties; attributes would have to be re-applied on every concrete implementor, which
  cannot be enforced for user types. Dead end.
- **(i) eager registration at assembly/DI init — INSUFFICIENT.** There is no `AddJustSaying()` DI
  extension in the repo, so this means a `[ModuleInitializer]` or a new opt-in call. Both merely *move*
  the check-then-mutate earlier; neither makes it atomic. Any host that serializes before the JustSaying
  assembly is first touched reproduces arm (b). Does not fix scope note 4.
- ⭐ **(ii) a `JsonSerializerOptions` owned by the JustSaying assembly — RECOMMENDED.** A static, lazily
  initialised options built as `new JsonSerializerOptions(JsonSerialisationOptions.Options)` plus the two
  converters, used by `JustSayingToMessage`, `GenericToMessage` and `MapToRequest`. The decisive property:
  **copy-constructing from a frozen `JsonSerializerOptions` is legal and never throws** — the ASB test at
  `When_Converting_A_Message_With_A_Session_Id.cs:43` already relies on it. Eliminates arms (a), (a′) and
  (b) in one move; `RegisterConverters` and both call sites can then be deleted.
  - **Breaks nothing in the current suite** (evidence 8 and scope note 9).
  - **Timing of the copy**: prefer deriving the copy on each *cache miss keyed by instance identity*, so a
    later `JsonSerialisationOptions.Options = …` (scope note 4) is picked up rather than silently ignored.
  - `JustSayingTransform.cs:119` operates on a `JsonNode` so converters are irrelevant there, but should
    switch for naming-policy consistency.
- ⛔ **Explicitly rejected**: making `RegisterConverters` swallow the exception, or dropping the
  `IsReadOnly` guard. Either turns arm (a) into arm (b) — **the flaky test goes green while the actual
  wire defect gets worse.**


## Regression Test

**File**: `tests/Paramore.Brighter.Transforms.Adaptors.Tests/JustSaying/When_mapping_a_direct_justsaying_request_after_the_global_options_are_frozen_should_write_flat_values.cs`
**Class**: `JustSayingFrozenGlobalOptionsTests` (three tests sharing one Arrange; xUnit serialises within a
class, which matters because the Arrange mutates a process-global).

**Shared Arrange** — the proven production state: copy the global options, strip any `TenantConverter` /
`IpAddressConverter` a prior test may have registered, **freeze** the copy by serializing through it (as
Brighter's outbox/inbox/scheduler do), then assign it to `JsonSerialisationOptions.Options`. `Dispose`
restores the original — which matters, because the Act throws today. The SUT type
`DirectJustSayingRequest` implements `IJustSayingRequest` **directly**, so the property-level
`[JsonConverter]` attributes on `JustSayingCommand`/`JustSayingEvent` cannot mask the defect.

**All three are RED, deterministically — 3/3 full-project runs, and the 30 pre-existing tests all pass:**

| test | red because |
|---|---|
| `When_mapping_a_direct_justsaying_request_after_the_global_options_are_frozen_should_write_flat_values` | `SocketException : Operation not supported` at `IPAddress.get_ScopeId()` ← `JustSayingToMessage` (`JustSayingMessageMapper.cs:82`). The outbound production crash. |
| `When_mapping_a_justsaying_payload_to_a_request_after_the_global_options_are_frozen_should_read_flat_values` | `JsonException : The JSON value could not be converted to …Tenant. Path: $.tenant`. The inbound parse failure. (Confirm predicted `IPAddress`; `Tenant` is simply earlier in the payload — same cause.) |
| `When_wrapping_a_message_mapped_after_the_global_options_are_frozen_should_keep_the_tenant_on_the_request` | `Assert.Equal() Failure: Expected "acme", Actual "fallback-tenant"` — the silent tenant **data loss** of Scope Note 3, demonstrated end to end. |

⚠️ **Side effect, accepted knowingly.** The shared Arrange freezes the global early, which pushes the whole
assembly into arm (b), so the **original flaky test now passes 3/3**. The suite goes from "1 flaky failure"
to "3 deterministic failures". This is a net gain in signal — and once the fix lands nothing mutates the
global, so the flake cannot return — but the new tests *displace* the old symptom rather than sitting
beside it.

⚠️ **No deterministic red exists for the `JustSayingAttribute.cs:33` call site**, and none was faked.
`s_register` is a process-wide latch, so: with the options frozen the attribute ctor takes the guard-true
path and neither throws nor mutates (green today); with them unfrozen it mutates only if no earlier test in
the assembly has already registered (flaky). The site is instead covered **by construction**: the fix
deletes `RegisterConverters` outright, which removes both call sites. Verification must confirm the type is
gone rather than rely on a test.


## Fix

**One-line summary**: the JustSaying assembly now derives its own `JsonSerializerOptions` from Brighter's
instead of mutating it, so nothing ever writes to the shared, freezable global.

| file | change |
|---|---|
| `src/…/JsonConverters/JustSayingSerialisationOptions.cs` | **new.** `internal static` holder. `Options` copies `JsonSerialisationOptions.Options` and adds `TenantConverter` + `IpAddressConverter` **to the copy**. Copy-constructing from a read-only instance is always legal, which is what makes this immune to the freeze. Re-derives when the global is *replaced* (`ReferenceEquals` on the source instance), so Scope Note 4's instance-swap case is honoured rather than silently ignored. |
| `src/…/JsonConverters/RegisterConverters.cs` | **deleted.** With it go arms (a), (a′) and (b) — there is no longer a check-then-mutate to lose. |
| `src/…/JustSayingMessageMapper.cs` | ctor deleted (it existed only to call `Register()`); the four serialize/deserialize sites now use `JustSayingSerialisationOptions.Options`. |
| `src/…/JustSayingAttribute.cs` | `Register()` call removed from the ctor — **the second call site of Scope Note 1**. |
| `src/…/JustSayingTransform.cs` | `:119` switched to the same options, so the assembly reads its serialisation settings from one place. |

**Result**: `Paramore.Brighter.Transforms.Adaptors.Tests` — **33/33 pass on net9.0 (3 consecutive runs) and
33/33 on net10.0.** The three regression tests are green, and so is the originally flaky
`MapToMessage_when_mapping_a_command_with_partial_justsaying_property_should_set_from_request_context` —
not by being masked, but because **nothing mutates the global any more, so the race cannot occur**.

**Deliberately NOT changed** (out of scope, recorded for follow-up):
- `ServiceCollectionExtensions.cs:788-793` `ConfigureJsonSerialisation` — same design flaw in core, no freeze
  guard at all. Scope Note 5. Deserves its own issue.
- `JsonSerialisationOptions.Options`' public setter and its process-global mutability. Scope Note 4/6.

## Verification (2026-09-09)

| project | result |
|---|---|
| `Paramore.Brighter.Core.Tests` (net9.0) | **978 passed**, 7 skipped, 0 failed |
| `Paramore.Brighter.Extensions.Tests` (net9.0) | **139 / 139** — includes `When_configuring_json_serialisation`, a blast-radius constraint from Scope Note 8 |
| `Paramore.Brighter.Transforms.Adaptors.Tests` (net9.0 ×3, net10.0) | **33 / 33** every run |

1,150 passing, zero failures. Not run: the container-backed transport suites (no infrastructure locally);
CI covers those. The ASB blast-radius constraint
(`When_Converting_A_Message_With_A_Session_Id.cs:43/:69`, which swaps the global options wholesale) lives in
`Paramore.Brighter.AzureServiceBus.Tests` and needs a broker, so it is left to CI — the fix only *reads*
`JsonSerialisationOptions.Options` and re-derives on instance change, which is exactly the case that test
exercises.
