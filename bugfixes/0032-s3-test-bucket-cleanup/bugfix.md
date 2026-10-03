# Bugfix: S3 test bucket cleanup

**Linked Issue**: #4316
**Status**: Verified locally — real AWS permissions and sweep integration not exercised

## Symptom

The AWS SDK v3 and v4 luggage tests leave `brightertestbucket-<guid>` buckets
behind. Upload tests leave an empty bucket even on success; failures can also
leave objects. The scheduled AWS cleanup script does not discover or delete S3
buckets.

## Suspected Location

The diagnosis references the pre-fix code at upstream commit `c083c0369`.
The following paths are relative to both
`tests/Paramore.Brighter.AWS.Tests/Transformers/` and
`tests/Paramore.Brighter.AWS.V4.Tests/Transformers/`:

- `When_uploading_luggage_to_S3.cs:20`: no teardown lifecycle.
- `When_wrapping_a_large_message.cs:18`: async disposal and constructor setup.
- `When_unwrapping_a_large_message.cs:19`: async disposal and uninitialized client.
- `When_validating_a_luggage_store_exists.cs:59`: happy-path-only teardown.
- `clean_failed_tests_aws_assets.sh:288`: discovery handles no S3 resources.

## Root-Cause Hypothesis

The issue's proposed fixture teardown and S3 sweep address separate failure
paths: ordinary test completion/failure and process termination. Initially
unverified, this hypothesis is supported by the code trace below. Merely adding
a bucket deletion call is insufficient when teardown is not invoked, setup
throws, or objects remain.

## Confirmed Root Cause

The upload fixtures have no bucket teardown. Wrap/unwrap use
`System.IAsyncDisposable`, but these projects use xUnit v2, whose async lifecycle
requires `IAsyncLifetime`. Their existing callbacks are not runner-recognized.
The unwrap callback would also dereference an uninitialized `_client`.

Wrap/unwrap create buckets in constructors. Bucket creation can succeed before
later configuration fails, leaving no constructed fixture available for cleanup.
Wrap cleanup depends on a claim assigned after assertions; unwrap cleanup assumes
the transformation removed its object. Either assumption fails on error paths.
The store-exists test deletes its bucket only after the test body succeeds.

## Evidence

- [x] Initial confirmation by code trace, without live AWS calls.
- [x] Regression failures observed before implementation; local verification below.
- Upload: bucket creation at line 47, claim-only deletion at line 69, no lifecycle
  interface at line 20 in both projects.
- Wrap: constructor provisioning at line 62, assertions before claim assignment
  at lines 76–81, claim-dependent cleanup at lines 87–96 in both projects.
- Unwrap: `_client` declared at line 22 but never assigned; provisioning at line
  53; bucket deletion dereferences `_client` at line 106 in both projects.
- Store-exists: provisioning at line 43 and inline deletion at lines 59–62.
- `Directory.Packages.props:167` pins xUnit 2.9.3; both AWS test projects reference
  `xunit`, not `xunit.v3`. The [xUnit lifecycle documentation](https://xunit.net/docs/shared-context)
  confirms that v2 does not support standalone `IAsyncDisposable`.
- `src/Paramore.Brighter.Transformers.AWS/S3LuggageStore.cs:328` creates the
  bucket before lifecycle, public-access, ownership and tagging operations. The
  v4 equivalent starts at line 327. Failed setup can therefore leave an untagged
  bucket.
- `clean_failed_tests_aws_assets.sh` discovers SNS, SQS and Scheduler resources
  only. Its existing failure reporting and offline test harness can be extended.

## Scope Notes

- Suggested fix: **PARTIAL**. Include correct xUnit lifecycle wiring, constructor
  setup safety, the uninitialized unwrap client, and leftover objects in both
  SDK test projects.
- Cleanup must use the exact fixture-owned bucket name and empty it before
  deletion, even when no claim was captured. Missing buckets may be benign;
  access-denied and other service failures must remain visible.
- Missing-parameter tests do not create buckets: the HTTP-factory check at
  `S3LuggageStore.cs:106` and ACL check at line 130 precede creation at line 137.
  The missing-ACL path can perform identity/existence reads. No extra bucket
  teardown is needed for these negative cases.
- The positive store-validation test constructs a Validate-configured store
  without invoking validation. Record this coverage weakness; avoid unrelated
  production changes.
- The sweep must preserve dry-run, age protection and deletion-failure reporting.
  Match only test buckets; avoid expanding the script's regional scope silently.
  S3 listing is account-wide unless filtered and must handle pagination.
- [AWS ListBuckets documentation](https://docs.aws.amazon.com/cli/latest/reference/s3api/list-buckets.html)
  supplies creation time and regional filtering. Creation time may change after
  bucket changes; treat it conservatively and never delete on an unreadable age.
- [AWS DeleteBucket documentation](https://docs.aws.amazon.com/cli/latest/reference/s3api/delete-bucket.html)
  requires all objects, versions and delete markers to be absent. These tests do
  not enable versioning; do not add broad version-purge permissions to the fix.
  Unexpected nonempty/versioned buckets must fail visibly rather than report success.
- Document the required S3 list/delete permissions for maintainers. Do not alter
  IAM, run a real account sweep, or delete existing remote resources as part of
  local verification.
- Changes belong in test infrastructure, the sweep and its regression coverage;
  no production luggage-store behavior change is currently indicated.

## Regression Test

The first test-first batch extends `test_clean_failed_tests_aws_assets_offline.sh`
and its existing `tests/fixtures/aws-cleanup/InMemoryAwsCli.sh` I/O substitute.
It executes the real script with a closed PATH and an empty environment; it
cannot fall back to a real AWS CLI or real credentials.

The 14 added scenarios cover old nonempty buckets, multiple listed buckets,
young/future/unreadable ages, unrelated names, dry-run, listing failures, object
removal failures, continued cleanup after bucket deletion errors, concurrent
bucket deletion, unexpected remaining versions and deletion-failure tolerance.
The CLI substitute also rejects S3 discovery outside the configured region and
refuses bucket deletion while its simulated objects remain.

After test approval, the unchanged script produced **101 passed / 15 failed
assertions**. Failures showed missing S3 discovery, deletion and failure reporting.
After implementation, the complete offline harness produced **116 passed / 0
failed assertions**. These counts are assertions, not separate test cases.

The fixture batch adds
`Transformers/When_s3_fixture_finishes_should_support_async_cleanup.cs` to both
AWS test projects. Its four cases per project verify that bucket-owning fixtures
implement the public `IAsyncLifetime` contract required by xUnit v2. These tests
do not instantiate fixtures or contact AWS. Before implementation all four cases
failed in each project on .NET 10; afterward all four passed. They also pass as
part of the full suites on both target frameworks. This contract check alone
does not prove object removal or cleanup after partial setup; the additional
emulator checks are recorded below.

Real CLI pagination and S3 integration also require separate verification; an
aggregated offline response does not prove pagination. The implementation uses
the documented CLI auto-pagination with `--page-size 1000`, without a result cap.

## Fix

Branch: `bugfix/4316-s3-test-bucket-cleanup` from upstream master.

`clean_failed_tests_aws_assets.sh` now lists buckets in the configured region,
matches the exact generated GUID naming shape, protects young or unreadable-age
buckets and previews candidates without mutation in dry-run mode. It empties
objects before deleting a bucket and preserves aggregate failure reporting and
the configured failure tolerance. Each object-removal CLI invocation counts as
one attempt, not as a count of individual objects. Version history is not purged.
Required S3 permissions are documented in the script and cleanup workflow.

All four bucket-owning fixtures in each AWS project implement `IAsyncLifetime`.
Remote provisioning runs in the test body, not the constructor or initialization
hook. This distinction matters: the [xUnit v2.9.3 runner](https://github.com/xunit/xunit/blob/v2-2.9.3/src/xunit.execution/Sdk/Frameworks/Runners/TestInvoker.cs)
does not reach async disposal if `InitializeAsync` throws. Body exceptions are
captured by the runner before it invokes disposal.

Each project's internal `S3TestBucketCleanup` helper repeatedly lists and deletes
objects by the fixture-owned bucket name before deleting the bucket. Re-reading
the first page drains more than 1,000 objects without depending on captured
claims. The cleanup client is initialized and disposed locally; only the exact
`NoSuchBucket` error is ignored. Other failures remain visible to the runner.

No production library code, IAM policies or real AWS resources have been changed.

## Verification

- Full emulator-compatible AWS SDK v3 suite: **293 passed, 8 skipped** on each of
  .NET 9 and .NET 10.
- Full emulator-compatible AWS SDK v4 suite: **293 passed, 8 skipped** on each of
  .NET 9 and .NET 10.
- These runs use CI's `LiveAWS!=true` filter and Floci 1.5.19 on an isolated
  loopback endpoint. They are not real-account certification. Existing compiler
  and analyzer warnings remain in the test projects.
- A local-only diagnostic exercised the actual public fixtures against Floci:
  successful execution and post-creation setup failure for all four fixtures;
  cleanup of 1 and 1,001 orphan objects without a captured claim; visible
  access-denied cleanup errors; and harmless disposal before bucket creation.
  All **12 checks per SDK project** passed, including an empty-account check
  after each cleanup. This diagnostic is not part of the committed regression
  suite.
- A forwarding proxy then rejected bucket tagging during actual xUnit runs.
  Each SDK run reported the expected four setup failures, and request tracking
  confirmed all four created buckets were deleted automatically. These were
  intentional failure-injection runs, separate from the green normal suites.
- The complete offline cleanup harness reports **116 passed, 0 failed
  assertions**. It never invokes the real AWS CLI.
- Shell syntax and `git diff --check` pass. ShellCheck reports no warnings or
  errors; its default output retains three existing SC2016 informational notices
  for intentionally single-quoted child-shell commands.
- The temporary Floci container and its disposable data were removed after
  verification. Diagnostic code and logs remain outside the repository.

Maintainer follow-up: confirm the cleanup identity's required S3 permissions and
preview the sweep with `--dry-run` in the intended account and region before a
real sweep. Real AWS CLI pagination, IAM enforcement and real-account deletion
were not exercised locally.
