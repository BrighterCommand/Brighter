# Bugfix: Report AWS cleanup deletion failures

**Linked Issue**: #4315
**Status**: Verified

## Symptom

The AWS cleanup sweep can fail every deletion and still return success to the
dedicated cleanup workflow. Warnings do not provide an aggregate failure signal.

## Suspected Location

- `clean_failed_tests_aws_assets.sh`: serial delete/unsubscribe calls, parallel
  SNS/SQS deletion workers, and final exit status.
- `.github/workflows/aws-cleanup.yml`: direct invocation of the cleanup script.

## Root-Cause Hypothesis

Successful warning output replaces each failed AWS command's exit status.
Parallel subprocesses do not communicate deletion outcomes to the parent, which
unconditionally exits successfully. Counting warning lines would be unreliable
because a resource can disappear between discovery and deletion.

## Confirmed Root Cause

Independent code inspection confirmed both status losses. All serial deletion
paths use `aws ... || echo WARNING`. Parallel workers return the status of their
final `echo`, and the parent does not check the worker pipeline before `exit 0`.

Closely related reads merge CLI diagnostics into resource data and suppress the
failure status. Failed subscription/schedule listings, queue URL lookup, and the
additional tagging query can therefore fabricate deletion arguments. These reads
must check status before their output is consumed.

## Evidence

- The baseline serial paths discard failures at lines 47-48, 214, 238, 246, 278,
  315-316, and 360-361.
- Baseline parallel workers at lines 466-473 and 522-529 discard the AWS error
  output and finish with successful logging commands. Line 534 exits zero.
- The dedicated cleanup workflow runs the script directly without
  `continue-on-error`, so no workflow change is needed to propagate its failure.
- The initial offline tests failed before the implementation. The final expanded
  suite was also run against the unmodified script from base commit `8bcc9a0a9`:
  24 assertions passed and 62 failed. The modified script passes all 86.

## Scope Notes

- Count completed deletion attempts, not matched or unique resources. Repeated
  discovery in the tag and name passes can cause multiple attempts for one resource.
- Preserve resource selection, age guards, dry-run behavior, and cleanup order.
- Apply `CLEANUP_MAX_DELETE_FAILURES` after finishing the sweep. Its default is 0;
  allowed values are decimal integers from 0 through 999999999, with leading zeros
  interpreted as decimal. Invalid input is rejected before AWS calls.
- Only narrowly matched service/action/error-code combinations classify an error
  as already absent. Authorization, throttling, network, and unrecognized errors
  remain failures even if their messages contain words such as "not found".
- Deletion-worker failures and incomplete result reporting are hard failures,
  independent of the deletion tolerance. The guarded resource-list/lookup failures
  also fail the run rather than becoming successful cleanup.
- Leave timeout, deletion budgets, retry/backoff tuning, and performance work to
  #4322. Existing age-read deferral behavior is unchanged.
- The pre-existing scheduler ARN categorization pattern does not match normal
  `arn:aws:scheduler:region:account:schedule-group/name` ARNs. This separate
  discovery defect is not changed here; scheduler tests use the existing additional
  Brighter-tagged group path with valid ARNs.

## Regression Test

`test_clean_failed_tests_aws_assets_offline.sh` executes the real script with
`tests/fixtures/aws-cleanup/InMemoryAwsCli.sh` replacing AWS CLI I/O. Each run has an
empty environment and a closed command path containing no real AWS executable.
The substitute returns local fixtures and fails on unexpected operations; it has
no network client or fallback to the real CLI.

Coverage includes:

- Serial topic, subscription, queue, schedule, and schedule-group failures.
- Mixed parallel outcomes at parallelism 1, 4, and 16, with continued cleanup
  after errors and exact aggregate counts.
- Default, equal-to-threshold, above-threshold, leading-zero, upper-bound, and
  invalid threshold inputs.
- Known already-absent responses, misleading error text, mismatched operation
  names, and unstructured CLI errors.
- Failed listings/lookups, pending and empty subscription markers, and duplicate
  discovery without invented deletion arguments or unique-resource claims.
- Dry runs, first-seen topic handling, young resources, and empty sweeps.
- Failed temporary-storage setup, deletion-worker failure, serial/parallel result
  write failures, and removal of the private result file.
- Reporting completed deletion outcomes when a later discovery call fails.

### Verification

- macOS Bash: 86 assertions passed, none failed.
- Linux Bash in an SDK container: 86 assertions passed, none failed, with
  networking disabled and the repository mounted read-only.
- `bash -n` passed for the cleanup script and both new test scripts.
- ShellCheck 0.10.0, warning severity: passed for all three scripts.
- `git diff --check` passed.

```bash
bash test_clean_failed_tests_aws_assets_offline.sh
bash -n clean_failed_tests_aws_assets.sh test_clean_failed_tests_aws_assets_offline.sh tests/fixtures/aws-cleanup/InMemoryAwsCli.sh
```

No live AWS cleanup or live-resource integration tests were run. These results
verify shell control flow, counts, error classification, and safety against local
fixtures; they do not certify AWS permissions, actual deletion, or sweep duration.

## Fix

All deletion/unsubscribe paths use one wrapper that preserves diagnostics and
records success, already-absent, or failure outcomes. Parallel workers append
single-byte records to a private temporary file instead of modifying subprocess
counters. An exit handler aggregates results, applies the threshold, preserves
other nonzero exit statuses, and removes the file. Worker/result-recording errors
cannot be hidden by a permissive deletion threshold.

The adjacent resource reads now separate failed commands from returned resources.
CI runs the offline suite in the build job, including on fork PRs. The cleanup ADR
documents the revised exit-status policy.
