# Review: design — 0027-span-based-performance (Round 3)

**Date**: 2026-05-01
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Consider addressing lower-scored items.

## Findings

### 1. Inconsistency between Section 2 and Implementation Step 3 on deserialization API (Score: 45)

Section 2 says deserialization will use `JsonSerializer.Deserialize<T>(ReadOnlySpan<byte>)`. Implementation step 3 says "deserialise from bytes via `Utf8JsonReader`". These are different APIs — the Decision section is clear and correct; the implementation step is misleading shorthand.

**Evidence**: Section 2: "After: message.Body.Memory.Span → JsonSerializer.Deserialize<T>(ReadOnlySpan<byte>)". Implementation step 3: "deserialise from bytes via `Utf8JsonReader`".

**Recommendation**: Change implementation step 3 to say "deserialise from `ReadOnlySpan<byte>`" to match the Decision section.

---

### 2. FR-8 Span overload deferred without clear trigger (Score: 40)

The ADR explicitly decides NOT to add `ReadOnlySpan<char>` overloads, instead only doing the `OrdinalIgnoreCase` optimization. The rationale is reasonable (no current callers with span-without-string), but could be clearer about what would trigger adding the overload.

**Evidence**: "If a ReadOnlySpan<char> call site appears in future transport work, a span overload can be added then."

**Recommendation**: Minor — could add which transport changes in this ADR might produce such callers.

---

### 3. InMemoryStorageProvider change understates interface constraint (Score: 35)

The ADR proposes storing `ReadOnlyMemory<byte>` internally but doesn't explicitly state that `IAmAStorageProvider`/`IAmAStorageProviderAsync` interfaces remain unchanged per NFR-1.

**Evidence**: ADR section 5 describes the internal change; interface contracts use `Stream` parameters.

**Recommendation**: Add a brief note that storage provider interfaces remain unchanged.

---

### 4. No mention of sync path for ReadOnlyMemoryStream (Score: 30)

The ADR mentions the `ReadOnlyMemoryStream` adapter but doesn't distinguish sync/async usage. Minor since `Stream` naturally works in both.

**Evidence**: Section 5 mentions the adapter without distinguishing paths.

**Recommendation**: No action needed — obvious from the `Stream` abstraction.

---

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 4 |

**Total findings**: 4
**Findings at or above threshold (60)**: 0
