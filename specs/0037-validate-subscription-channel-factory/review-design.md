# Review: design — 0037-validate-subscription-channel-factory (ADR 0073, round 2)

**Date**: 2026-09-21
**Threshold**: 60
**Verdict**: NEEDS WORK

4 findings at or above threshold 60. Address these before approving.

> **Verification note.** Every finding below was re-checked against the working tree by the main
> agent before filing (standing convention: agent findings are claims, not facts). **All ten hold.**
> Two supporting details were corrected in the filing: finding 5's Xunit call count (nineteen lines,
> not eighteen), and finding 10's line citation. No finding was rejected.

## Findings

### 1. `Sweep`'s return type is stated two different ways — the Architecture Overview diagram still carries the pre-D6 contract (Score: 85)

Probe 2 is confirmed. D6 changed `Sweep` to return one entry per examined candidate as
`IReadOnlyList<(Type Subject, string? Reason)>`. Four of the five places that describe the return
were updated; the Architecture Overview diagram was not. It still says the method returns a list of
*strings*, and glosses them as "the reasons" — i.e. failures only, the exact contract D6 withdrew,
and the exact contract the immediately following section spends eight lines arguing against ("A
sweep returning failures alone cannot distinguish a sound assembly from one it never looked at").

A developer who implements from the diagram writes `IReadOnlyList<string> Sweep(Assembly)`; the
generated template at line 253, which asserts "the result is non-empty … and that every `Reason` is
`null`", then does not compile. This is the signature duplication-drift defect of this spec,
surviving into round 2.

**Evidence**: ADR lines 133-134, the only stale one of nine `Sweep` references:

```
  │   Sweep(Assembly gatewayAssembly)                            │
  │        -> IReadOnlyList<string>  the reasons, ordered        │
```

against line 189:

> **`public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)`**
> — the sweep. It returns **one entry per candidate it examined**…

The other three references (line 253 template, line 331 AC-29, line 362 step 2) all agree with line
189. The frontmatter `summary` does not state a return type, so it is not in conflict.

**Recommendation**: Change the diagram line to `-> IReadOnlyList<(Type Subject, string? Reason)>`
with the gloss "one entry per candidate; Reason null = sound", so the diagram carries the property
the design's central argument depends on.

---

### 2. Context's "there is no such path through existing exports" is contradicted by the ADR's own generator-emitted-predicate alternative (Score: 72)

This is probe 3. The widening is licensed in Context by a single test: *is there a path through the
module's existing exports to the behaviour under test?* The ADR answers no. But Alternatives
Considered then describes a design that reaches exactly that behaviour using nothing but existing
exports — `Subscription.ChannelFactoryType`, `IAmAChannelFactory` and `InMemoryChannelFactory` are
all already public — and concedes it works: "it reaches all twelve, it cannot drift (one template),
and it adds **nothing** to the shipped package."

Under `.agent_instructions/testing.md` as the ADR itself quotes it, that is dispositive: "If there
is, widening is unjustified — test through that path." The ADR escapes this only by redefining the
behaviour under test as *the relationship the module has never named* — that is, as the new export
itself, which is circular: the behaviour is inside the module only because this decision puts it
there.

That leaves C-10 as the sole discriminator, and C-10 does not carry that weight. C-10 is an
**accepted exposure**, not a goal: "D2 … corrects the five in-repo cases and FR-12's sweep guards
the twelve shipped gateway assemblies, but **neither reaches out-of-repo types**." The requirements
settle that out-of-repo authors are *not* reached. The Negative bullet promotes reaching them into a
selection criterion — "the narrowest option that reaches all twelve test projects *and* C-10's
out-of-repo authors" — which makes the claim true by construction, since the second conjunct is the
thing that excludes the rival. The alternative's second ground ("a guard whose logic ships in the
package is one the package can be held to") is asserted, not argued, and is the only non-circular
support the rejection has.

This is not a claim that the decision is wrong — it may well be right, and testing.md's own "a
widening that other callers would genuinely want is a real term of the contract" supports it. The
claim is that the ADR's stated justification does not survive its own Alternatives section.

**Evidence**: ADR line 83: "**Here there is no such path**…". ADR lines 480-482: "This is strictly
narrower than the chosen design on public surface: it reaches all twelve, it cannot drift (one
template), and it adds **nothing** to the shipped package." `.agent_instructions/testing.md:117`:
"If there is, widening is unjustified — test through that path." `requirements.md` C-10 (line 265):
"neither reaches out-of-repo types."

**Recommendation**: Either (a) state honestly that a path through existing exports does exist — the
generator-emitted predicate — and rest the decision on testing.md's *second* test ("Having to widen
is a design signal… A widening that other callers would genuinely want is a real term of the
contract"), which is the argument the ADR is actually making; or (b) keep the "no path" claim but
say precisely what behaviour has no path, and reconcile it with the Alternatives bullet. Drop "and
C-10's out-of-repo authors" from the narrowest-option claim, or mark it explicitly as a criterion
this ADR adds beyond the requirements. **(a) is the recommended route** — see finding 6, which makes
it nearly free.

---

### 3. No test anywhere asserts that `Sweep` ever returns a non-null `Reason` — and line 337 misdescribes AC-29 as covering that negatively (Score: 70)

The design's failure paths divide cleanly: `Check`'s failure branches are exercised with literal
arguments (AC-28), and `Sweep`'s composition — candidate discovery, subsumption, closing, the
uninitialised read, and the hand-off of the read value into `Check` — is exercised only over cases
that are **sound**. The twelve generated tests assert every `Reason` is `null`. Step 2's two
Core.Tests doubles are both sound: AC-29's constructor-cannot-succeed double "declares a sound
factory type" and appears "with a `null` `Reason`"; the generic-closing double "declares its own
override". So a `Sweep` that read the wrong property, passed `null` to `Check`, or discarded the
read value entirely would leave every test in this design green.

The ADR asserts otherwise, and contradicts itself doing so: line 337-338 claims the reading path is
"covered positively by the twelve sweeps and **negatively by AC-29**", but lines 331-333 define
AC-29's double as producing a `null` `Reason` — a positive case. There is no negative case for the
reading path anywhere in the design.

**Evidence**: ADR lines 331-333: "a subscription whose constructor cannot succeed … **but which
declares a sound factory type**. It appears among `Sweep(itsOwnAssembly)`'s subjects with a `null`
`Reason`". ADR line 337-338: "The reading path it does not exercise is covered positively by the
twelve sweeps and negatively by AC-29." ADR line 253: "the result is non-empty … and that every
`Reason` is `null`."

**Recommendation**: Add a Core.Tests case that runs `Sweep` over an assembly (or a filtered
candidate set) containing the two AC-28 doubles and asserts their entries carry the expected
non-null `Reason` — the doubles already exist, they are already in `Core.Tests`, and this closes the
read→`Check` seam. Then fix line 337-338, which is wrong about AC-29 either way.

---

### 4. AC-27's "reported at most once" clause has no assertion in the generated template (Score: 65)

AC-27's *Then* has three conjuncts: implements `IAmAChannelFactory`, is not `InMemoryChannelFactory`,
"**with a base/derived pair reported at most once**". The generated template asserts only two things
— non-empty, and every `Reason` null. A subsumption regression that reported both
`RocketSubscription` and `RocketMqSubscription<Command>` would produce two entries, both with `null`
reasons, and the test would pass. The at-most-once property — which the ADR spends a whole bullet
designing (generic-definition reduction, `BindingFlags.DeclaredOnly`) and which FR-12's Scope
paragraph states as a MUST — is therefore guarded nowhere over the twelve real assemblies.

The ADR's only coverage statement for subsumption is oblique: "No shipped assembly has that shape
today, so step 2's synthetic types cover it" (line 216), and that sentence is about the
`FooBar : Foo<Bar>` generic-definition case specifically, not about the twelve shipped pairs.

**Evidence**: `requirements.md:519` — "Then the type implements `IAmAChannelFactory` and is not
`typeof(InMemoryChannelFactory)`, for every type found, **with a base/derived pair reported at most
once**." FR-12 Scope (`requirements.md:193`): "A base/derived pair in the same assembly (e.g.
`RocketSubscription` and `RocketMqSubscription<T>`) MUST be reported at most once." ADR lines
252-255 describe the template as asserting exactly two things.

**Recommendation**: Have the template also assert that `Subject` values are distinct — one line,
costs nothing, and directly discharges AC-27's third conjunct. Say so in the "asserts **both**
halves" sentence, which then becomes three.

---

### 5. Step 4's `SharedGenerator` compile claim omits the Xunit dependency (Score: 55)

Probe 5. The conclusion holds, but the stated reason is incomplete. `DefaultMessageAssertion.cs.liquid`
— one of the four helper files — calls `Xunit.Assert` on nineteen lines. So the rendered helpers
reference `Paramore.Brighter`, `Paramore.Brighter.Observability` **and xunit**. All three new
conformance-only projects do carry `<PackageReference Include="xunit" />`, so the files do compile;
but the ADR's argument as written does not establish that, and a reader checking the claim finds a
dependency the ADR says is not there.

(Note also that `Paramore.Brighter.Observability` is not a separate package — it is a namespace
inside `src/Paramore.Brighter`, with no project directory of its own — so the "both already
available" phrasing suggests two assemblies where there is one. Minor, folded in here.)

**Evidence**: `grep -c "Xunit\.Assert"
tools/Paramore.Brighter.Test.Generator/Templates/DefaultMessageAssertion.cs.liquid` → `19`. A
usings-only check misses it: the file's only `using` is `using Paramore.Brighter;`, because Xunit is
fully qualified at every call site. All three csprojs (`AzureServiceBus`, `MQTT`, `RMQ.Sync`)
contain `<PackageReference Include="xunit" />`. `ls -d src/Paramore.Brighter.Observability` → no
such directory; `src/Paramore.Brighter/Observability/` is the namespace's home.

**Recommendation**: Amend step 4 to "reference only `Paramore.Brighter` (which contains the
`Observability` namespace) and xunit, all three already present in every gateway test project".

---

### 6. The rule that licenses the widening now exists in `testing.md` and the ADR neither cites nor references it (Score: 55)

Commit `f966f8c3b` added `.agent_instructions/testing.md` § *Narrow and deep — and when widening the
surface is legitimate* (lines 109-129), which states the conditional reading, the "is there a path
through existing exports" test, the "widen honestly and record what was widened and why (in the ADR,
or the PR)" instruction, the "a widening that only a test could ever want is a smell" discriminator,
and the "does anything other than a test ever call it?" after-the-fact check. The ADR's Context
reproduces all five, near-verbatim, as its own interpretation of the quoted bullets — and its
References line cites only "*No InternalsVisibleTo*, *Test Scope and Isolation*, test and file
naming, one class per file".

The effect is to weaken the ADR's own position: the Negative bullet concedes "a reader who does not
accept that argument should read this as surface spent to make a guard testable", when the argument
is not the ADR's to accept or reject — it is a project rule with a named section the ADR could
simply point at.

**Evidence**: `.agent_instructions/testing.md:109` — `### Narrow and deep — and when widening the
surface is legitimate`; compare :114-118 with ADR :74-81, and :140-145 with ADR :98-107. ADR :513
References entry names four things from testing.md, none of them *Narrow and deep*.

**Recommendation**: Cite the section by name in Context ("as `.agent_instructions/testing.md`
§ *Narrow and deep* puts it") and add it to the References line. Then finding 2's question — which
of that section's two tests this decision passes — has to be answered explicitly, which is the
point.

---

### 7. "*Test Scope and Isolation* carries three bullets" — the section carries eight bullet lines (Score: 50)

The quoted block is verbatim-accurate, but it is an excerpt of one top-level bullet and two of its
four sub-bullets, presented as the section's full content. The section has two top-level bullets
with two and four sub-bullets respectively — eight bullet lines in all. Nothing gets built
differently, but a grounded claim about a cited source is wrong, and the ADR's numbering ("the third
bullet", twice more at :76-77) does not survive a reader opening the file.

Worth noting against round 1: that round's highest finding was this same passage quoting `:103-104`
and omitting `:105`, the bullet that prohibited the act. `:105` is now included; the description of
the section's extent was not corrected with it.

**Evidence**: `.agent_instructions/testing.md:100-107` — `- Only test exports from an assembly`
(+2 sub-bullets at :101-102) and `- Do not expose more than is necessary from an assembly` (+4
sub-bullets at :104-107, of which the ADR quotes two). ADR :67: "*Test Scope and Isolation* carries
three bullets:".

**Recommendation**: "carries two bullets; the relevant one reads:" and, since the ADR refers to "the
third bullet" twice more, name it instead ("*do not export to test*").

---

### 8. The CI step as described would fail: `--no-build` without `--configuration Release` (Score: 45)

The `build` job runs `dotnet build --configuration Release`; the ADR's step is described as
`--filter "FullyQualifiedName~GatewayChannelFactoryDeclarationTests" --no-build`. `dotnet test
--no-build` defaults to the Debug configuration, which the job never built, so the step errors with
"was not built" on all twelve. The existing transport jobs all pass `--configuration Release`
explicitly.

**Evidence**: `.github/workflows/ci.yml:59-60` — `- name: Build` / `run: dotnet build
--configuration Release`. `:228` and `:361` both include `--configuration Release` on their `dotnet
test` lines. ADR :302-303 quotes the new step without it. (Scored low because the ADR's fragment
does not claim to be the full command line.)

**Recommendation**: Include `--configuration Release` in the quoted fragment.

---

### 9. "Two further reasons belong to `Sweep` rather than `Check`" arguably undercounts (Score: 40)

Three reasons are Sweep-only, not two: a generic definition of unexpected arity or unsatisfied
constraints (line 227-229), a type for which an uninitialised instance cannot be produced, and a
read that throws. Reading "further" as "further to the one just described" makes the sentence
correct, so this is scored low and flags the ambiguity rather than the count — but the closing
reason is the one an implementer is most likely to miss, and it is not enumerated where the
Sweep-reason set is enumerated.

**Evidence**: ADR :227-229 "A definition of any other arity, or one whose constraints the
representative does not satisfy, **yields a reason**"; ADR :234-235 "**Two further reasons** belong
to `Sweep` rather than `Check`: a type for which an uninitialised instance cannot be produced, and a
read that throws."

**Recommendation**: "Three reasons belong to `Sweep` rather than `Check`:" and list the closing
failure with the other two.

---

### 10. `ci.yml:769` is the commented RocketMQ *test step*, not the job declaration (Score: 35)

Probe 4: `:228` (MQTT `Category=MQTT&Fragile!=CI`) and `:361` (Kafka
`Category=Kafka&Category!=Confluent&Fragile!=CI`) are both exact. `:769` is the final line of the
file and is the commented-out `RocketMQ Tests` run line; the commented `rocketmq-ci:` job
declaration is at `:708`. The substantive claim — the job is entirely commented out — is true.

**Evidence**: `grep -n -i rocketmq .github/workflows/ci.yml` → `707:#  TODO: Rafael Andrade is
working on how to run RocketMQ on GHA`, `708:#  rocketmq-ci:`, … and `wc -l` → `769`.

**Recommendation**: Cite `:708-769`, or `:708` for the job declaration.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 3 |
| 50-69 (Medium) | 4 |
| 0-49 (Low) | 3 |

**Total findings**: 10
**Findings at or above threshold (60)**: 4

---

## Probes not upheld

**Probe 1 (a fourth stale copy of the withdrawn surface-widening claim): not found.** A grep over the
ADR for the withdrawn framing's vocabulary returns only the reframed passages at :90-91 and :406,
plus unrelated exception-type mentions. The three passages `5625be4cc` touched are mutually
consistent and no fourth copy survives. Finding 2 is a *different* defect in the reframed argument,
not a stale copy of the old one.

## Grounded references sampled and correct

`Subscription.cs:35/:172/:213/:258`; `Command.cs:42`; the nine `ChannelFactoryType` overrides at
RocketMQ `:50`, GcpPubSub `:108`, Redis `:32`, Kafka `:162`, MQTT `:35`; `RocketMqSubscription.cs:10/:117-118`;
`GcpPubSubSubscription.cs:158-159`; `TestConfiguration.cs:38`; the reference topology
(`ServiceActivator` by exactly six — AWS, AWS.V4, MQTT, RMQ.Async, RMQ.Sync, RocketMQ; `Base.Test` by
exactly three — Gcp, MSSQL, PostgresSQL; `Paramore.Test.Helpers` by one — MQTT; each of the twelve
referencing exactly one gateway); fourteen existing `test-configuration.json` files, all carrying
`Namespace`, nine of them in gateway projects; exactly twelve `src/Paramore.Brighter.MessagingGateway.*`
directories, each matching its root namespace; the two existing generated output shapes (Redis
`MessagingGateway/Generated/{Reactor,Proactor}`, RMQ.Async `MessagingGateway/{Classic,Quorum}/Generated`);
`GeneratedTreeAudit.ExpectedFilesUnder` built from `OutboxGenerator.Plan` and
`MessagingGatewayGenerator.Plan`; `MessagingGatewayGenerator`'s `Suites`/`SuitesFor`/`Plan` trio; the
four `SharedGenerator` helper templates.
