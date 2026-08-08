# Bugfix: Failed-build cleanup skips transforms on a throw and can mask the build error

**Linked Issue**: PR #4254 round-9 review — item 2
**Status**: Verified

## Symptom
When a wrap/unwrap pipeline build fails part-way, the builder releases the mapper and transforms it
already created directly, because no pipeline was returned to the caller to own them. Round 8 fixed
this exact "a throw skips the rest / a retry double-releases" shape in
`TransformLifetimeScope[Async].ReleaseTrackedObjects`, but the sibling on the *partial-build* path
did not get it.

`TransformPipelineBuilder.cs` / `TransformPipelineBuilderAsync.cs`, `ReleaseTransforms`:

```csharp
transforms.Each(transform => _messageTransformerFactory.Release(transform));
```

A throwing `Release` on transform *i* skips *i+1..n*. Unlike the lifetime-scope case there is no
retry to fall back on — no pipeline was constructed, so these transforms sit in no `InstanceScope`
and no finalizer will ever see them; their DI scopes leak permanently. This PR deliberately lets
`Release`/`Dispose` surface exceptions (MS DI's synchronous scope `Dispose` throws for an
`IAsyncDisposable`-only service; a user `Dispose` may throw), so this is now reachable.

Relatedly, the outer catch blocks:

```csharp
catch (Exception e)
{
    CleanUpAfterFailedBuild(pipeline, transforms, messageMapper);   // may now throw
    throw new ConfigurationException("Error building wrap pipeline ...", e);
}
```

Because cleanup can now throw, a `Release`/`Dispose` failure during cleanup escapes *before* the
`throw new ConfigurationException(..., e)` runs, so the caller sees the disposal failure instead of
the original configuration error `e` they actually need to fix.

Expected: cleanup releases every transform even when one `Release` throws, and a cleanup failure
never replaces the build error.

## Suspected Location
- `src/Paramore.Brighter/TransformPipelineBuilder.cs` — `ReleaseTransforms` (`transforms.Each(...)`)
  and the wrap/unwrap `catch (Exception e)` blocks calling `CleanUpAfterFailedBuild`.
- `src/Paramore.Brighter/TransformPipelineBuilderAsync.cs` — the async twins of both.

## Root-Cause Hypothesis (confirmed)
`ReleaseTransforms` iterates with `Each` and stops at the first throw; on the partial-build path
there is no owning pipeline and no finalizer, so the remaining transforms are unreachable and leak.
Separately, `CleanUpAfterFailedBuild` is not guarded, so a cleanup throw pre-empts the rethrow of
the build error.

## Fix
1. `ReleaseTransforms` (both builders): drain a materialised copy, removing each transform before
   releasing it, and swallow a per-transform `Release` failure so a throw neither skips the rest nor
   masks the build error rethrown by the caller. There is genuinely no retry here (unlike
   `TransformLifetimeScope`, whose drain relies on a finalizer retry), so releasing must complete in
   the single pass.
2. Wrap the `CleanUpAfterFailedBuild(...)` call in `try { ... } catch { }` in all four catch blocks
   (wrap + unwrap, sync + async), so a disposal failure cannot mask the `ConfigurationException`
   carrying the original build error.

## Test
`tests/Paramore.Brighter.Core.Tests/MessageSerialisation/When_A_Transform_Release_Throws_During_A_Failed_Build.cs`
— sync + async (4 facts):
- **partial build**: a mapper declares three wrap transforms; the factory builds the first two but
  cannot build the third, so the build fails and cleanup releases the two built transforms. Releasing
  the first throws. Asserts the second is still released (drain continues) **and** the caller sees the
  real `ConfigurationException("Could not create transformer ...")`, not the release failure.
- **post-construction cleanup**: the wrap pipeline is constructed (one transform), then unwrap
  discovery throws because `MapToRequest` is an explicit implementation; disposing the discarded
  pipeline throws when releasing its transform. Asserts the caller still sees the wrap-build
  `ConfigurationException` whose inner is the unwrap-discovery failure, not the release throw.

Proven RED against the pre-fix code (all four): the later transform was absent from `Released`, and
the cleanup exception (`InvalidOperationException("release failed")`) replaced the
`ConfigurationException`. GREEN after the fix. Regression-clean: `MessageSerialisation` 95/95 and full
`Paramore.Brighter.Core.Tests` on net9.0 + net10.0; production build clean on all TFMs including
netstandard2.0.

## Base commit
Branch `memory-leak`, HEAD `01debe459` at time of fix.
