# Bugfix: Bound AWS cleanup backlog processing

**Linked Issue**: #4322
**Status**: Verified

## Symptom

A large historical backlog repeatedly exhausted the AWS cleanup job's 30-minute
timeout. Runs were cancelled while deleting queues, leaving work for subsequent
runs. Ian reports that an unrestricted manual sweep took approximately an hour.

Recent small-backlog runs pass. This does not demonstrate that large backlogs can
be processed within the job timeout.

## Suspected Location

References use upstream baseline `c083c0369`, excluding pending S3 PR #4430.

- `.github/workflows/aws-cleanup.yml:20`: job-wide 30-minute timeout.
- `.github/workflows/aws-cleanup.yml:39`: propagation wait and subsequent
  diagnostic listings consume time before cleanup starts.
- `clean_failed_tests_aws_assets.sh:271`: complete tagged-resource discovery.
- `clean_failed_tests_aws_assets.sh:324`: serial tagged-resource cleanup.
- `clean_failed_tests_aws_assets.sh:207`: per-queue creation-time lookup.
- `clean_failed_tests_aws_assets.sh:533`: complete age partition before deletion.
- `clean_failed_tests_aws_assets.sh:548`: SNS processing precedes SQS discovery.
- `clean_failed_tests_aws_assets.sh:606`: paginated SQS discovery.
- `clean_failed_tests_aws_assets.sh:627`: age-check every matched queue.
- `clean_failed_tests_aws_assets.sh:648`: delete every eligible queue.

## Root-Cause Hypothesis

The script's workload grows with the backlog, but its only runtime boundary is
the external job timeout. Parallelism limits simultaneous calls, not total work.
The name sweep checks every matched resource's age before deleting any resource
of that type. Repeated runs therefore repeat a potentially expensive full age
scan before reducing the backlog.

Independent confirmation supports this hypothesis. A deletion-only budget would
leave the preceding age scan unbounded; raising the timeout would move the same
size-dependent failure boundary. The issue's proposed approaches are alternatives,
not an agreed implementation.

## Confirmed Root Cause

The script admits an unbounded amount of backlog-dependent work into a fixed
job runtime. Its name-sweep age partition is a barrier: every matched resource
of a type must be checked before any name-based deletion of that type starts.
Later runs repeat this prerequisite work for the remaining backlog.

Parallelism does not bound the total workload. For N matched old queues, the
queue name sweep makes N age-check CLI calls and N delete CLI calls, in addition
to discovery and preceding phases. Earlier tagged-resource or SNS processing can
also consume the time available to queues.

This confirms the scalability defect, not the precise contributions of AWS
latency, CLI startup, throttling, or retries to the historical duration.
The diagnosis is approved; the confirmation marker is present.

## Evidence

- Historical [run 34194189605](https://github.com/BrighterCommand/Brighter/actions/runs/34194189605)
  reported 0 topics and 5,335 queues. It entered the name sweep at 06:22:00 UTC
  and logged its first name-sweep queue deletion at 06:42:56 UTC, roughly 21
  minutes later. It logged 1,830 successful name-sweep queue deletions before
  cancellation at 06:50:06 UTC. These are logged outcomes, not an independently
  verified account census. The interval includes discovery and age checking;
  the log does not isolate the duration of each phase or AWS call.
- Recent [run 36267251250](https://github.com/BrighterCommand/Brighter/actions/runs/36267251250)
  reported 0 topics, 1 queue, and 0 attempted deletions; it succeeded. The six
  latest runs inspected on 2026-09-27 all succeeded.
- Code trace: `partition_by_age` at line 533 drains all worker output before
  returning. Its invocation at line 627 precedes queue deletion at line 648.
  The per-queue age lookup is at line 211. The exit summary at line 116 does
  not provide an internal deadline or guarantee reporting after forced shutdown.

## Scope Notes

- Preserve resource selection, age protection, dry-run behavior, and deletion
  failure reporting from #4315. Unreadable queue ages must continue to defer
  deletion, and newly seen SNS topics must remain protected.
- Account for discovery and age checks, not only deletion calls. Include earlier
  phases in the deadline; do not promise per-resource-type fairness from a global
  timeout alone.
- Explicitly distinguish a completed sweep, intentionally deferred work, and
  failed operations. Do not claim an exact remaining AWS resource count from an
  eventually consistent listing.
- PR #4430 added S3 cleanup and has been merged into this branch from master.
  Preserve its region, name and age checks within the whole-sweep deadline.
- No live AWS cleanup has been performed. Incremental processing and the explicit
  deadline are implemented; verification uses offline AWS responses.
- Incremental age-check/deletion can remove the barrier but does not alone
  guarantee completion before timeout. A processing allowance bounds admitted
  work, not discovery, retry latency, or already-running calls. A soft deadline
  must be described as such; a hard bound needs bounded in-flight operations and
  headroom for workflow setup and reporting.
- A capped first-N selection must not repeatedly revisit young or unreadable
  resources while starving later eligible candidates. If resumability is promised,
  repeated-run and cross-resource-type progress require explicit coverage.

## Regression Test

`test_clean_failed_tests_aws_assets_offline.sh` now contains the first regression
for both SQS and SNS, which share the age-partition barrier. Each case supplies
three old resources, no tagged resources, age protection enabled, and parallelism
one. It asserts that the first deletion precedes the final age lookup, all three
candidates are checked and deleted, and the final summary records three successes.

The initial baseline run produced 94 passing assertions and 2 failures: both
services failed the incremental-progress assertion. The first implementation
passed all 96 assertions on macOS Bash 3.2 and Linux Bash. This established
incremental progress, not a runtime bound.

### Deadline tests

The next cases in `test_clean_failed_tests_aws_assets_offline.sh` specify
`CLEANUP_TIMEOUT_SECONDS`: omitted or zero preserves unrestricted local runs;
a positive decimal value limits the complete cleanup invocation. Empty, negative,
fractional, and oversized values fail before AWS calls. A configured deadline
without GNU `timeout` available also fails before AWS calls.

The existing offline CLI substitute now accepts a per-response delay and records
TERM interruption. Tests exercise actual process termination during initial
discovery, queue age checking, and queue deletion after earlier progress. Further
cases preserve ordinary failure status, dry-run behavior, and visible AWS errors
before timeout. These tests require GNU coreutils and perform no network I/O.

Before the deadline implementation, the expanded suite produced 114 passing
assertions and 41 failures. After implementation, all 155 assertions pass.
The delayed fixture ignores SIGPIPE so a closed capture pipe cannot prevent its
TERM handler from recording cancellation; assertions briefly await that
asynchronous marker. No real AWS executable is available on the test command path.

## Fix

### Incremental processing

`clean_failed_tests_aws_assets.sh` now dispatches a complete age-check/delete
operation for each matched SNS topic or SQS queue. An eligible resource can be
deleted without waiting for every other candidate's age lookup. The existing age
predicates, concurrency default, selection rules, and deletion-result aggregation
are unchanged. Both real and dry runs use the same eligibility path.

Matched counts are logged before dispatch; deferred resources are reported
individually. The final deletion summary still counts actual completed attempts,
not every matched resource. Worker failures propagate through the checked xargs
pipeline.

### Verification

- Baseline offline suite: 94 passed, 2 failed for the expected ordering defect.
- Deadline baseline: 114 passed, 41 failed for missing deadline behavior.
- Updated offline suite: 155 passed, 0 failed on macOS and Linux; three consecutive
  Linux runs passed. Linux verification used a network-disabled .NET SDK container
  with the repository mounted read-only.
- Bash syntax checks passed for the cleanup script, harness, and CLI substitute.
- ShellCheck 0.10.0 at warning severity passed in a network-disabled container.
- `git diff --check` passed.
- Workflow YAML parsing and deadline/headroom checks passed. The workflow no
  longer has direct AWS diagnostic calls outside the bounded sweep. Actionlint
  was unavailable; the workflow was not executed on GitHub.

No AWS integration tests or real backlog timing measurements were performed.
These results establish incremental progress, deadline interruption of simulated
AWS I/O, and the existing offline coverage. They do not certify live IAM
permissions or how much of a real backlog fits within 25 minutes. The forced-KILL
fallback is configured but was not separately exercised by the regression suite.

### Integration with S3 cleanup

Merged master at `a0990d5dd` after PR #4430 landed, preserving Ian's existing merge
at `f5b1dcfd4`. The two manual conflict resolutions retain both `sleep` and
`python3` in the offline command path and both sets of regression cases.

The combined suite passes all 185 assertions on macOS and in a network-disabled
Linux SDK container with Python installed and the repository mounted read-only.
Bash syntax, ShellCheck at warning severity, workflow YAML/safety settings, and
diff whitespace checks pass. The imported C# fixture files match master exactly.
The S3 sweep runs inside the supervised invocation without changing its resource
selection, age guard, or failure reporting. No live AWS cleanup was run.

### Deadline and reporting

The implementation uses the explicit-deadline alternative in #4322, not resumable
batching. `CLEANUP_TIMEOUT_SECONDS` is validated before AWS access and defaults
to zero for unrestricted local execution. A positive value starts a supervised
invocation with its recursive deadline disabled.

The workflow sets 1,500 seconds for the whole cleanup invocation, including
discovery, with headroom below the 30-minute job limit. Redundant pre-cleanup
diagnostic listings have been removed so they cannot consume that headroom with
unbounded AWS calls. The existing 15-second propagation wait remains.

GNU `timeout` supervises the process group without `--foreground`, allowing the
deadline to reach child processes. It sends TERM at the deadline, with a KILL
fallback after five seconds. TERM/INT handlers stop the cleanup shell and run its
existing exit summary where possible. The supervisor preserves exit status and
reports timeout, forced termination, or supervisor errors as an incomplete sweep.
It never turns interruption into successful cleanup. In-flight outcomes and a
final summary may be unavailable after forced termination. See the [GNU timeout documentation](https://www.gnu.org/software/coreutils/manual/html_node/timeout-invocation.html)
for signal and exit-status behavior.

Later invocations rediscover remaining resources. There is no persistent cursor,
exact remaining-resource census, or guarantee that every resource type receives
work during a timed-out run. Incremental deletion improves progress once a phase
is reached; the deadline makes unfinished work fail visibly instead of relying
on GitHub's job cancellation.
