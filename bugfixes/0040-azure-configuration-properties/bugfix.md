# Bugfix: Azure configuration fields are invisible to property-based tooling

**Linked Issue**: #4285
**Status**: Verified locally — broker-free tests and library builds pass; live Azure integration tests not run

## Symptom

Six public configuration fields are absent from property enumeration.
Direct assignments work, but property-based tooling cannot discover or set these options.
Service Bus scalar configuration binding is a regression-test target; delegate values are not expected to bind from text configuration.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusSubscriptionConfiguration.cs:67`: `SqlFilter`.
- Same file, line 72: `UseServiceBusQueue`.
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusPublication.cs:57`: `UseServiceBusQueue`.
- `src/Paramore.Brighter.Locking.Azure/AzureBlobLockingProviderOptions.cs:50`: `StorageLocationFunc`.
- `src/Paramore.Brighter.Archive.Azure/AzureBlobArchiveProviderOptions.cs:50`: `TagsFunc`.
- Same file, line 64: `StorageLocationFunc`.

## Root-Cause Hypothesis

Original hypothesis: these members are fields, so property-only tooling omits them.
Converting exactly these six declarations to public read/write auto-properties should fix discovery.
Keep all initializers and post-construction mutability unchanged.

## Confirmed Root Cause

Independent review confirmed that all six members are public mutable fields, not properties.
Property-only reflection omits them even though direct reads, assignments, and delegate invocation work.
Public read/write auto-properties address the confirmed discovery defect without changing the member values or provider logic.

## Evidence

- The six declaration sites above still exist on upstream commit `075eb440d`.
- `AzureServiceBusConsumerFactory.cs:71` and `AzureServiceBusMessageProducerFactory.cs:78` read the respective queue-selection fields.
- `AzureServiceBusWrappers/AdministrationClientWrapper.cs:116` reads `SqlFilter` when constructing subscription rules.
- `AzureBlobLockingProvider.cs:112` invokes the storage-location callback after resource normalization.
- `AzureBlobArchiveProvider.cs:96` invokes the archive-location callback; line 118 invokes tags only when `TagBlobs` is enabled.
- Public-instance `GetProperty` assertions failed against all six field declarations before the fix.

All 14 regression cases failed on both .NET 9 and .NET 10 before implementation, with missing-property or configuration-binding assertions rather than infrastructure failures.
The same cases pass on both frameworks after the six conversions.

## Scope Notes

- Preserve names, types, defaults, delegate results, and ordinary object-initializer assignments.
- Use `get; set;`, not `init`, because the existing fields remain mutable after construction.
- No matching field-specific `ref`, `out`, explicit `in`, or reflection usage was found in repository searches.
- Already compiled consumers require rebuilding. External field-reflection and by-reference callers may require source changes.
- Do not promise that delegate-valued options become bindable from JSON or generally serializable.
- Property-based serializers may newly encounter unsupported delegate values. No new serialization annotations or behavior are in scope.
- The maintainer accepts the binary compatibility cost with a rebuild-and-test requirement, as relayed by the contributor on 2026-09-28.
- The upgrade note under `Master` in `release_notes.md` is linked from NuGet packages through `src/Directory.Build.props:12`.
- Do not change package versions or release automation. Release Drafter's `Breaking Change` label selects a major bump, while the maintainer plans a minor release.
- A broker-free configuration-test step in the main CI build job covers fork PRs. The existing Azure job excludes fork PRs.

## Regression Test

The tests are in `tests/Paramore.Brighter.AzureServiceBus.Tests/Configuration/`:

- `When_inspecting_azure_configuration_should_find_public_read_write_properties.cs`: seven cases cover the six declarations and inherited discovery on a generic publication.
- `When_binding_service_bus_subscription_options_should_apply_filter_and_queue_selection.cs`: binds nondefault SQL filter and queue-selection values, checks defaults, and retains post-construction assignment.
- `When_binding_service_bus_publication_options_should_apply_queue_selection.cs`: two cases cover ordinary and generic publications, including defaults and later assignment.
- `When_setting_blob_lock_location_as_a_property_should_preserve_defaults_and_allow_replacement.cs`: checks the default location, property-based delegate replacement, and direct reassignment.
- `When_setting_blob_archive_location_as_a_property_should_preserve_defaults_and_allow_replacement.cs`: checks the message-ID default, property-based delegate replacement, and direct reassignment.
- `When_setting_blob_archive_tags_as_a_property_should_preserve_defaults_and_allow_replacement.cs`: two cases preserve the five default tags, including null correlation ID and content-type fallback, then exercise delegate replacement.

There are 14 cases per target framework. Property-discovery assertions and scalar binding failed against the original fields and pass against the properties.
Default and direct-assignment assertions protect existing behavior within those regression cases.
Reflection is limited to public configuration properties: this is the external tooling contract under test, not private implementation inspection.

The existing xUnit project receives test-only references to the Blob archive and locking projects and the centrally versioned configuration binder package.
This avoids creating another test project or changing the existing NUnit-based Azure suite.
`TestDoubles/InMemoryConfigurationTokenCredential.cs` rejects token requests; no test constructs an Azure client or requires Azure credentials.
The generic publication uses the existing `Command` type without constructing handlers or pipelines; no new request types or logging registrations are needed.

## Fix

Converted exactly the six members to public `get; set;` auto-properties, preserving their initializers and leaving provider logic unchanged.
Updated XML documentation, added the rebuild-and-test release note requested by the maintainer, and added the configuration-test CI step.
Only the test project gains dependencies: the existing archive and locking projects and the centrally versioned configuration binder.

## Verification

- Regression tests before the fix: 14 failed on .NET 9 and 14 failed on .NET 10, for the expected assertions.
- Regression tests after the fix: 14 passed on each framework, with no failures or skips.
- Broader broker-free Service Bus suite: 252 passed on each framework, including the 14 new cases, with no failures or skips.
- All three affected libraries build in Release for `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.
- The Service Bus test project builds for .NET 9 and .NET 10. Its initial build reported 656 warnings and zero errors; the subsequent library builds reported no warnings or errors.
- The existing `Paramore.Brighter.Azure.Tests` project also builds in Release (20 warnings, zero errors); its live integration tests were not executed.
- The exact new CI command, including the `GitHubActions` logger and `--no-restore --no-build`, passes all 14 configuration cases on each framework locally.
- Tracked and new-file whitespace checks passed.

Reproduce the focused regression run:

```bash
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj -c Release --filter 'FullyQualifiedName~Paramore.Brighter.AzureServiceBus.Tests.Configuration.'
```

Reproduce the broader broker-free run:

```bash
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj -c Release --filter 'Category!=ASB&Category!=AzureServiceBus'
```

The broader filter excludes the hand-written and generated live Service Bus integration tests.
Live Azure Service Bus and Blob integration tests were not run; no cloud resources or credentials were used.
An initial sandboxed attempt was aborted by the test runner's local socket permission restriction; the permitted rerun completed successfully.
No claim is made about live Azure behavior or remote CI results.
