# Review: design — pipeline-cache-type-key (ADR 0064)

**Date**: 2026-06-18
**Threshold**: 60
**Verdict**: PASS

No findings at or above threshold 60. Both sub-threshold findings addressed post-pass.

## Findings

### 1. Step 4 omits the test helper's return-type change (Score: 45) — ADDRESSED

The Implementation Approach step 4 said the white-box test "must cast/assert against `typeof(MyPreAndPostDecoratedHandler)`", but did not mention the helper method's signature must also change. The helper is `private static IEnumerable<string> GetPostAttributesCacheKeys()` returning `cache.Keys.Cast<string>()`. Once keys become `Type`, both the `Cast<string>()` body and the `IEnumerable<string>` return type must change. `Cast<string>()` over `Type` keys is not a compile error — it throws `InvalidCastException` at runtime — so following only the literal "assert `typeof`" wording without re-typing the helper would still produce a red test.

**Evidence**: ADR step 4; test (`When_Building_A_Pipeline_Post_Attributes_Are_Cached.cs:58-67`): `private static IEnumerable<string> GetPostAttributesCacheKeys() { ... return cache.Keys.Cast<string>(); }`, with `Assert.Contains(nameof(MyPreAndPostDecoratedHandler), ...)` at line 33.

**Resolution**: Step 4 now states the helper's return type moves `IEnumerable<string>` → `IEnumerable<Type>` and `Cast<string>()` → `Cast<Type>()`, with the `InvalidCastException`-at-runtime trap called out.

### 2. "Describe does not read the cache" line citation slightly narrow (Score: 20) — ADDRESSED

The ADR cited `Describe`/`Describe()` as `PipelineBuilder.cs:106-155`. The first overload spans 106-137 and the parameterless one 144-155 (XML-doc at 139-143 between them). The substantive claim — neither reads the memento — is correct. Cosmetic.

**Evidence**: `PipelineBuilder.cs` — `Describe(Type)` 106-137, `Describe()` 144-155; neither references the mementos.

**Resolution**: Citation updated to `106-137, 144-155`.

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 0 |
| 50-69 (Medium) | 0 |
| 0-49 (Low) | 2 |

**Total findings**: 2
**Findings at or above threshold (60)**: 0

## Reviewer grounding note

Every code citation in the ADR was verified accurate to the line: all eight `implicitHandler.Name.ToString()` sites (PipelineBuilder.cs 275/284/292/301 sync, 319/327/334/342 async); cache field declarations (49-50; TransformPipelineBuilder.cs 57-61; TransformPipelineBuilderAsync.cs 57-61); transform key sites (TransformPipelineBuilder.cs 238/253 TryGetValue/TryAdd, TransformPipelineBuilderAsync.cs 201/209 GetOrAdd); `RequestHandler.Name => new HandlerName(GetType().Name)` (68); `HandlerName.ToString()` (51-54); UseInbox `implicitHandler.GetType()` (363/383); all three `ClearPipelineCache`; mis-typed `CreateLogger<TransformPipelineBuilder>` (TransformPipelineBuilderAsync.cs:50); test `cache.Keys.Cast<string>()` (66) / `Assert.Contains(nameof(...))` (33). The `Type.FullName` nullability and cross-assembly non-uniqueness claims are accurate per .NET; the `AssemblyLoadContext` retention characterisation is reasonable and honestly scoped. Only one test file touches the mementos, validating the "one white-box test" claim. No reference to a nonexistent file/class/member.
