# Bugfix: AWS CloudEvents source header

**Linked Issue**: #4458
**Status**: Verified — local emulator suites and all-target transport builds pass

## Symptom

AWS SNS and SQS messages carry `souce` instead of the CloudEvents `source` key inside `cloudeventheaders`.
External consumers cannot find the canonical key. Brighter round trips hide the mismatch because readers use the same spelling.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AWSSQS/HeaderNames.cs:48` and its V4 counterpart define the spelling.
- `SnsMessagePublisher.cs:129` in both packages writes the source into the JSON headers.
- `SqsMessageSender.cs:149` (v3) and `:147` (v4) write the same key.
- `SqsMessageCreator.cs:172` (v3) and `:170` (v4) read raw delivery headers.
- `SqsInlineMessageCreator.cs:279` (v3) and `:247` (v4) read SNS envelope headers.
- `HeaderNames.cs:74` in both packages classifies source as a known header.

## Root-Cause Hypothesis

Both packages share the misspelled `HeaderNames.Source` value between writers and readers.
The writer therefore omits canonical `source`, and the reader cannot recognize canonical source from external producers.

The issue suggests canonical writes and legacy `souce` read fallback.
This suggestion was initially unverified. The confirmation trace below establishes the cause and the additional compatibility requirements.

## Confirmed Root Cause

Both `HeaderNames.Source` constants contain `souce`. Each SNS publisher and SQS sender uses that constant as a JSON dictionary key.
Neither writer emits canonical `source`. Both readers also look up only `souce`, masking the error in Brighter round trips.
Canonical `source` from external producers is therefore missed by the affected readers.

## Evidence

- Independent read-only review confirmed both constants and all four writer sites listed above.
- Raw SQS readers deserialize `cloudeventheaders` before looking up the source key. They do not currently parse a standalone source attribute.
- SNS envelope readers try a standalone source attribute before the nested JSON headers. Each candidate must pass `Uri.TryCreate`.
- `SqsInlineMessageCreator.ReadSource` preserves this order at v3 lines 279–293 and v4 lines 247–261.
- `HeaderNames.IsKnown` excludes recognized standalone attributes from `MessageHeader.Bag` in both reader forms.
  The v3 call sites are `SqsMessageCreator.cs:267` and `SqsInlineMessageCreator.cs:197`; both V4 readers have equivalent loops.
- Before the production fix, the final regression fixtures failed 28 of 46 cases in each SDK on both .NET 9 and .NET 10.
  Failures were assertions on the canonical source, source precedence, and extra-header bag; the 18 compatibility controls passed.

## Scope Notes

The suggested fix is partial without explicit compatibility coverage.

Approved scope:

- Write canonical `source` in both AWS packages through the shared constant.
- Accept legacy `souce` in each supported reader location. Do not introduce standalone-source parsing for raw SQS delivery.
- Preserve the SNS envelope reader's standalone-before-nested ordering. Prefer a valid canonical value over a legacy value within the same location.
- Preserve existing URI validation and default-source behavior. Cover missing and malformed values in regression tests.
- Keep both spellings recognized by `HeaderNames.IsKnown`, preventing legacy standalone attributes from leaking into the bag.
- Do not dual-write the misspelled key without maintainer agreement.

Legacy read fallback supports old producers and stored messages, but does not update old consumers.
For mixed versions, upgrade consumers before producers, or agree on a separate dual-write transition with the maintainer.
The issue's suggested fallback lifetime of one release does not create an automatic expiry mechanism.
Documentation in the separate Docs repository requires coordinated follow-up; no external documentation has been edited.

## Regression Test

Tests are written in both `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/` and
`tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/`:

- `When_publishing_an_aws_message_should_write_the_canonical_source_header.cs`:
  publish through the public SNS and SQS writers, then inspect the actual SQS wire attributes.
  Assert literal `source` contains the nondefault URI and literal `souce` is absent.
- `When_receiving_an_aws_message_should_read_canonical_and_legacy_source_headers.cs`:
  send externally constructed wire messages through the AWS SDK, then receive through the public Brighter consumer.
  Cover raw delivery and SNS envelopes, canonical and legacy keys, competing values, malformed values, relative URIs, and defaults.
  Preserve standalone-before-nested precedence and verify raw delivery does not acquire standalone-source parsing.
  Both spellings remain absent from the extra-header bag, while an unrelated external attribute remains present.
  Each of the 22 input cases exercises both synchronous and asynchronous receive.

There are 46 cases per SDK test project: 2 writer cases and 44 reader cases.
Resources have unique names and are registered with `AwsTestResourceReaper` before creation.
Cleanup runs in `finally`, including when assertions fail.
The tests require the repository's AWS emulator or an explicitly configured AWS environment.
They do not expose internal creators or use reflection, and introduce no request types or closed pipeline generics.

Diagnosis and regression tests were approved before implementation.
The initial writer fixture needed a request type and the SNS topic ARN; both setup errors were corrected before recording the final red run.
After the fix, all 46 cases passed in each SDK on both target frameworks (184 executions).

Sensitivity checks on .NET 9 in both SDKs also confirmed that:

- Removing legacy parsing causes 14 source-value assertion failures per SDK.
- Removing legacy known-header classification causes 14 bag-exclusion assertion failures per SDK.

Both temporary mutations were restored. No tests were skipped or weakened.

## Fix

- `HeaderNames.cs` in both packages now defines canonical `source` for the existing SNS and SQS writers.
  An internal `LEGACY_SOURCE` constant retains the old spelling for reads and known-header classification.
- `SqsMessageCreator.cs` reads valid canonical source first, then legacy source, from the nested CloudEvents headers.
- `SqsInlineMessageCreator.cs` applies canonical-then-legacy lookup to standalone attributes before nested headers.
  URI validation, default values, and standalone-before-nested precedence are unchanged.

## Verification

Tests use Floci 1.5.19 from `docker-compose-aws.yaml`, with `AWS_SERVICE_URL=http://localhost:4566`.
Targeted runs use Release configuration and `--filter FullyQualifiedName~AwsCloudEventSource`.
Local TRX evidence is under `/private/tmp/brighter-4458-test-results/`:

- `4458-red-final-v3*` and `4458-red-final-v4*`: final pre-fix failures.
- `4458-green-v3*` and `4458-green-v4*`: all targeted tests pass.
- `4458-mutation-read-*` and `4458-mutation-bag-*`: expected sensitivity failures.
- `4458-suite-v3*` and `4458-suite-v4*`: broader suites after restoring both mutations.

Broader runs use CI's `--filter 'LiveAWS!=true'` for both AWS test projects.
Real AWS tests are excluded; emulator results do not establish live-service behavior.

| SDK | Target framework | Passed | Existing skips | Failed |
| --- | --- | --- | --- | --- |
| v3 | .NET 9 | 368 | 8 | 0 |
| v3 | .NET 10 | 368 | 8 | 0 |
| v4 | .NET 9 | 368 | 8 | 0 |
| v4 | .NET 10 | 368 | 8 | 0 |

These totals include all 46 new cases per combination.
The eight skips in each run are existing generated dead-letter requeue tests deferred under #4341.
No skip attributes or CI filters were changed.

Both transport projects also passed `dotnet build --no-restore -c Release` across all declared library targets:
`netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.
The final incremental builds reported zero warnings and zero errors; earlier test-project compilation reported warnings.
Build logs are `/private/tmp/brighter-4458-build-v3.log` and `/private/tmp/brighter-4458-build-v4.log`.

Whitespace checks passed for tracked changes and all new files. The two SDK test pairs differ only by namespace imports and declarations.
Implementation branch: `bugfix/4458-aws-cloudevents-source`.
