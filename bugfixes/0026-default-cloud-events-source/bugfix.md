# Bugfix: Use the shared default CloudEvents source

**Linked Issue**: #4313
**Status**: Verified

## Symptom

Two production expressions construct a default CloudEvents source from the literal
`http://goparamore.io` instead of `MessageHeader.DefaultSource`. Their values match
today, but changing the canonical constant would leave these defaults behind.
This is a consistency defect, not a demonstrated current runtime failure.

## Suspected Location

- `src/Paramore.Brighter/Publication.cs:64`: `Source` initializer.
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageCreator.cs:368-372`:
  `ReadSource` fallback when URI parsing fails.
- `src/Paramore.Brighter/MessageHeader.cs:90`: canonical source constant.

## Root-Cause Hypothesis

The two defaults retained independent literals instead of referencing the shared
constant. Replacing those literals should preserve current behavior while removing
the risk of these expressions diverging after a change to the constant.

## Confirmed Root Cause

Source inspection confirms both duplicated defaults. The Kafka expression belongs
to `ReadSource`, despite its local variable being named `dataSchema`; schema parsing
is handled separately. The proposed replacement addresses both reported sites.

## Evidence

- `MessageHeader.cs:90` defines `DefaultSource` as `http://goparamore.io`, and its
  `Source` initializer at line 303 already uses that constant.
- `Publication.cs:64` independently repeats the same string.
- `KafkaMessageCreator.cs:369` reads `CLOUD_EVENTS_SOURCE`; line 372 uses the literal
  when `Uri.TryCreate` fails. Line 131 passes that result to the message header's
  `source` parameter.
- `ReadDataSchema` at lines 362-366 returns null on failure and is not affected.
- A production-source search found exactly these two `new Uri` expressions with
  the duplicated literal.

## Scope Notes

- Replace only the two literal arguments with `MessageHeader.DefaultSource`.
- Do not change the constant's value, URI parsing, explicit source overrides, or
  fallback semantics. The misleading Kafka local variable name is outside scope.
- A missing Kafka source header becomes an empty string in `ReadHeader` at line
  435. It must not be assumed to exercise the invalid-URI fallback; verification
  should use a URI that is known to fail parsing.

## Regression Test

Existing Core CloudEvents and Kafka header tests cover the unchanged behavior.
A temporary public-API diagnostic also covered the publication default, explicit
publication source, valid/malformed Kafka source headers, and schema preservation.

Ordinary value assertions pass before and after this behavior-preserving change;
they are not a failing regression reproduction. An isolated diagnostic build with
a different canonical constant demonstrated the dependency before and after the
fix. Both assemblies were rebuilt because the value is a C# constant. The diagnostic
and constant mutation were kept outside the repository; no new permanent test was
added for this behavior-preserving cleanup.

## Fix

Replaced the literal argument in `Publication.Source` and Kafka's `ReadSource`
fallback with `MessageHeader.DefaultSource`. No parsing or default-value changes.

## Verification

- Baseline Core CloudEvents tests: 41 passed on each of .NET 9 and .NET 10.
- Baseline targeted Kafka header tests: 4 passed per framework.
- Full Core suite after the fix: 1,040 passed, 7 skipped, 0 failed per framework.
- Expanded Kafka header and timestamp checks after the fix: 6 passed per framework
  (`KafkaHeaderToBrighterTests`, `KafkaHeaderUtf8EncodingTests`,
  `KafkaDefaultMessageHeaderBuilderTests`, `KafkaLegacyTimeStampFormatTests`, and
  `KafkaTimeStampRoundTripTests`).
- Broker-backed Kafka suite with Kafka 4.0.2 and schema registry 8.0.6: 173 passed,
  0 failed, 0 skipped on each of .NET 9 and .NET 10 in Release mode. The CI filter
  `Category=Kafka&Category!=Confluent&Fragile!=CI` excludes Confluent-cloud and
  CI-fragile cases; this was not an unfiltered run of every Kafka test.
- Release build of the Kafka project and its Core dependency: successful for
  .NET Standard 2.0, .NET 8, .NET 9, and .NET 10, with no warnings or errors.
  The Kafka test-project Release build also succeeded, but reported 1,160 warnings.
- Public-API diagnostic on .NET 10: all 15 checks passed before and after the fix
  with the original constant. Missing and empty Kafka sources remained empty
  relative URIs; they do not enter the malformed-source fallback.
- Isolated changed-constant experiment: exactly two checks failed before the fix
  (publication default and malformed Kafka source fallback); all 15 passed after
  applying the same two substitutions. Explicit sources and schemas were preserved.
- The production constant remains `http://goparamore.io`; `git diff --check` passed.

Reproduction commands for the broader suites:

```bash
dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj
dotnet build src/Paramore.Brighter.MessagingGateway.Kafka/Paramore.Brighter.MessagingGateway.Kafka.csproj -c Release
docker compose -p brighter-4313 -f docker-compose-kafka.yaml up -d kafka schema-registry
dotnet test tests/Paramore.Brighter.Kafka.Tests/Paramore.Brighter.Kafka.Tests.csproj -c Release --filter 'Category=Kafka&Category!=Confluent&Fragile!=CI'
docker compose -p brighter-4313 -f docker-compose-kafka.yaml stop kafka schema-registry
```

Wait for the broker and schema registry to respond before running the Kafka suite.
