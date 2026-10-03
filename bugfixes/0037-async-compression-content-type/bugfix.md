# Bugfix: Async-compressed messages are not decompressed

**Linked Issue**: [#4432](https://github.com/BrighterCommand/Brighter/issues/4432)
**Status**: Verified

## Symptom

Messages compressed with `CompressPayloadTransformer.WrapAsync` remain compressed
when passed to either `Unwrap` or `UnwrapAsync`. The original payload and content
type should be restored. The issue reports this for GZip, Zlib, and Brotli using
a JSON payload with 4,096 repeated characters, `Optimal` compression, and a zero
threshold. Removing the compressed header's charset reportedly makes the same
bytes decompress successfully.

## Suspected Location

- `src/Paramore.Brighter/Transforms/Transformers/CompressPayloadTransformer.cs:123-132`:
  asynchronous compression sets the compressed content type's charset.
- `src/Paramore.Brighter/Transforms/Transformers/CompressPayloadTransformer.cs:145,220`:
  both decompression entry points return immediately if recognition fails.
- `src/Paramore.Brighter/Transforms/Transformers/CompressPayloadTransformer.cs:301-312`:
  recognition compares serialized content types with bare MIME strings, including
  the separate .NET Standard GZip branch.
- `src/Paramore.Brighter/Transforms/Transformers/CompressPayloadTransformer.cs:199-207`:
  synchronous compression does not explicitly add a charset.

## Root-Cause Hypothesis

UNVERIFIED at triage: a header such as `application/gzip; charset=utf-8` does not
equal `application/gzip`, so recognition rejects valid compressed messages before
decompression begins. The suggested use of `ContentType.MediaType` needs
confirmation independently of stream finalization or payload encoding.

## Confirmed Root Cause

Independent code-trace confirms the hypothesis. `WrapAsync` emits a compressed
MIME type with a charset; `IsCompressed` compares its full serialized value with
a bare MIME type. Both decompression methods consequently return the compressed
message without reaching their restoration logic.

Comparing `ContentType.MediaType` in all four existing comparisons addresses the
cause while retaining the current compressed wire headers and payload guards.
Removing the outgoing charset alone would leave already-produced messages affected.
Diagnosis and regression tests approved on 2026-09-28.

## Evidence

Investigation baseline: upstream master `ea294324d`.
All six regression cases failed before the fix on both net9.0 and net10.0 at
the original-byte assertion: actual bytes were still compressed. All six pass
on both frameworks after the fix, without changes to the approved tests.

- `CompressPayloadTransformer.cs:114-120`: the compression stream is closed
  before output is read, so missing stream finalization does not explain this bug.
- `CompressPayloadTransformer.cs:123-132`: async compression assigns the charset
  and saves the original content type.
- `MessageBody.cs:188-198,289-293` and
  `Extensions/CharacterEncodingExtensions.cs:42-49`: raw body encoding maps to
  null and leaves the assigned charset intact.
- `CompressPayloadTransformer.cs:301,305,309,312`: each recognition comparison
  includes parameters that are absent from the expected MIME string.
- `CompressPayloadTransformer.cs:145-148,220-223`: both unwrapping methods return
  immediately on failed recognition.

Source paths above are relative to `src/Paramore.Brighter/`; transformer references
are under `Transforms/Transformers/`.

## Scope Notes

Existing asynchronous compression tests check compressed output but do not
decompress it. Existing decompression tests construct bare header MIME types;
the memory-based round-trip test covers only synchronous compression.

Cover all three algorithms through both public decompression methods, including
the .NET Standard GZip comparison in the implementation. Preserve null-header
handling, GZip/Zlib signature and length checks, and the existing wire format.
No new dependencies, public APIs, or stream-handling changes are needed.

A separate pre-existing concern is non-UTF-8 restoration: the decompression paths
construct `MessageBody` with its default UTF-8 encoding, which can overwrite the
saved charset. This is outside the confirmed recognition defect and is not part
of this fix without a separate scope decision.

## Regression Test

Written in
`tests/Paramore.Brighter.Core.Tests/Compression/When_decompressing_an_async_compressed_message_should_restore_the_original_payload.cs`.
Reviewed and run before and after the fix on net9.0 and net10.0.

For each of GZip, Zlib, and Brotli, the regression asynchronously compresses
a UTF-8 JSON payload containing 4,096 repeated characters, using `Optimal` and a
zero threshold. It decompresses through `Unwrap` and `UnwrapAsync` with separate,
correctly configured transformers and verifies the original payload bytes and text,
and both header and body content types. Setup checks ensure async compression
really occurred and its header retains the charset that triggers the defect.

The full core suite, including existing compression tests, passes on both
net9.0 and net10.0: 1,490 passed, 7 existing skips, and no failures per framework.
No tests were skipped or weakened for this fix.

## Fix

In `src/Paramore.Brighter/Transforms/Transformers/CompressPayloadTransformer.cs`,
use `ContentType.MediaType` instead of `ToString()` for all four compression-type
comparisons. No outgoing headers, signature guards, public APIs, or dependencies
changed.

The core library builds for netstandard2.0, net8.0, net9.0, and net10.0 with no
warnings or errors. `git diff --check` passes.

Local TRX results are under `/private/tmp/brighter-4432-test-results/`:

- `4432-red-net9.trx` and `4432-red-net10.trx`: six expected failures each.
- `4432-green_net9.0_20260928122433.trx` and
  `4432-green_net10.0_20260928122433.trx`: six passes each.
- `4432-full-core_net9.0_20260928122710.trx` and
  `4432-full-core_net10.0_20260928122708.trx`: full core suite results.
- `4432-pre-pr-core_net9.0_20260928123410.trx` and
  `4432-pre-pr-core_net10.0_20260928123409.trx`: repeated pre-PR verification,
  with the same full-suite totals.

The full solution and external broker/database suites were not run; this change
is confined to the core compression transformer.
