# Bugfix: Azure Blob luggage connection-string configuration

**Linked Issue**: [#4478](https://github.com/BrighterCommand/Brighter/issues/4478)
**Status**: Fixed — local tests pass; live Azure verification blocked by endpoint DNS failures

## Symptom

Constructing `AzureBlobLuggageStore` with `ConnectionString = "UseDevelopmentStorage=true"` and `ContainerName = "luggage"` throws `ArgumentException` with an empty message.
The documented connection-string configuration should initialize the store successfully.

## Suspected Location

- `src/Paramore.Brighter.Transformers.Azure/AzureBlobLuggageStore.cs:60`: the constructor selects the connection configuration.
- The same file, line 67: the connection-string branch requires both strings to be null or empty.
- The same file, line 73: rejected configurations produce an empty exception message.
- `src/Paramore.Brighter.Transformers.Azure/AzureBlobLuggageOptions.cs:25`: documentation identifies connection string plus container name as supported.

## Root-Cause Hypothesis

Original hypothesis, now confirmed by code trace: the connection-string branch inverted its intended condition.
With both strings populated and no complete URI/credential pair, neither branch matches.
The constructor therefore throws before creating the SDK client.
Conversely, missing strings enter the SDK constructor instead of Brighter's invalid-configuration branch.

The issue suggests negating both checks and naming the supported configuration pairs in the exception message.

## Confirmed Root Cause

Before the fix, the constructor selected connection-string configuration only when both strings were null or empty and no complete URI/credential pair existed.
Valid populated values therefore reached Brighter's empty-message `ArgumentException`.
The condition contradicted the two supported configurations documented on the options and constructor.

## Evidence

Source references in this section describe the pre-fix implementation at `6abf8c7a4`.

- `AzureBlobLuggageOptions.cs:18` and line 25 document the URI/credential and connection-string/container-name pairs.
- `AzureBlobLuggageStore.cs:63` gives a complete URI/credential pair precedence.
- With no complete URI/credential pair, line 67 selects the connection-string branch only for two missing strings.
- Line 69 passes those missing values to the SDK, while line 73 rejects populated strings with an empty message.

| Container name | Connection string | Pre-fix result without a complete URI/credential pair |
| --- | --- | --- |
| Populated | Populated | Brighter throws at line 73 |
| Null or empty | Populated | Brighter throws at line 73 |
| Populated | Null or empty | Brighter throws at line 73 |
| Null or empty | Null or empty | Missing values reach the SDK at line 69 |

The constructor-only reproduction is `new AzureBlobLuggageStore(new AzureBlobLuggageOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "luggage" })`.
Before the fix, this reproduction threw before SDK construction instead of returning a store.
The reproduction needs neither Azure credentials nor a running Azurite instance; storage operations are separate methods.
The diagnosis was confirmed by code trace and then reproduced by the approved constructor tests.
Before implementation, 13 cases failed on each of .NET 9 and .NET 10; the two URI/credential cases passed.
Failures were expected assertion failures about construction, exception type, parameter name, or message content.

Requiring both strings to be nonempty and explaining the supported pairs in the exception message makes all 15 cases pass.

## Scope Notes

- Preserve precedence for a complete `ContainerUri`/`Credential` pair.
- Accept a complete `ConnectionString`/`ContainerName` pair when the URI/credential pair is incomplete or absent.
- Reject incomplete configurations with an `ArgumentException` whose parameter is `options` and whose message names both supported pairs.
- Cover valid connection-string construction, incomplete pairs, and existing URI/credential precedence.
- Leave whitespace and malformed connection-string handling to the SDK; broader configuration validation is outside this issue.
- No matching branching defect was found in `AzureBlobArchiveProvider.cs:11` or `AzureBlobLockingProvider.cs:41`; both construct clients directly from URI/token credentials.
- Existing transformer tests in `Paramore.Brighter.Azure.Tests` use NUnit and live Azure storage.
  New constructor tests use xUnit in the same project, with centrally versioned xUnit and runner package references.
  Existing NUnit tests remain unchanged.

## Regression Test

Three behavior-focused files under `tests/Paramore.Brighter.Azure.Tests/Transformers/Configuration/` cover the approved scope:

- `When_configuring_blob_luggage_with_a_connection_string_should_construct_the_store.cs`: three cases cover the supported connection-string pair, including an incomplete URI/credential alternative.
- `When_configuring_blob_luggage_with_incomplete_options_should_explain_the_supported_pairs.cs`: ten cases cover missing or empty strings and incomplete URI/credential alternatives.
  Each expects an `ArgumentException` for `options` with a message naming all four configuration properties.
- `When_configuring_blob_luggage_with_uri_and_credential_should_prefer_that_pair.cs`: two cases protect URI/credential construction with missing or malformed connection-string alternatives.

The tests construct the real store without storage operations, credentials lookup, reflection, or test doubles.
An `AzureCliCredential` instance supplies the credential option, but no token is requested.
All 15 constructor cases pass on .NET 9 and .NET 10.

The URI/credential cases characterize existing behavior and passed initially.
The approved named mutation **IgnoreUriCredentialPair** temporarily removed the first constructor branch, forcing connection-string selection.
Both precedence cases failed their no-exception assertions on both frameworks.
The mutation was reverted byte-for-byte to the pre-fix source, and both cases passed again before implementation.
No mutation remains in the final source.

## Fix

`AzureBlobLuggageStore` now requires both `ConnectionString` and `ContainerName` to be nonempty before constructing the SDK client from them.
The invalid-configuration exception names both supported pairs and retains `ParamName == "options"`.
The URI/credential branch retains its original precedence.

No storage operations, dependencies in the production project, or public API signatures changed.
The test project adds centrally versioned xUnit packages alongside its existing NUnit packages.

## Verification

All test runs used Debug configuration on .NET 9 and .NET 10.

| Check | .NET 9 | .NET 10 |
| --- | --- | --- |
| Constructor cases before the fix | 13 failed, 2 passed | 13 failed, 2 passed |
| IgnoreUriCredentialPair mutation | 2 expected failures | 2 expected failures |
| Precedence guards after reverting the mutation | 2 passed | 2 passed |
| Constructor cases after the fix | 15 passed | 15 passed |
| Full Azure test project | 28 passed, 13 failed | 28 passed, 13 failed |
| Final constructor and scheduler tests | 28 passed | 28 passed |

Every full-suite failure reports DNS resolution failure for `brighterarchivertest.blob.core.windows.net`.
The endpoint is configured by existing live archive, locking, and luggage tests.
No constructor regression failed, but the full suite is not green and live Azure verification remains outstanding.

The Azure transformer library builds for `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0` with zero warnings and errors.
The new test files explicitly enable nullable analysis and produce no compiler warnings.
Existing NUnit tests and their framework remain unchanged.
The final source contains only the constructor predicate correction and explanatory exception; the mutation is absent.

Run the constructor regression cases:

```bash
dotnet test tests/Paramore.Brighter.Azure.Tests/Paramore.Brighter.Azure.Tests.csproj --filter 'FullyQualifiedName~Paramore.Brighter.Azure.Tests.Transformers.Configuration'
```

Run all local constructor and scheduler cases without contacting Azure:

```bash
dotnet test tests/Paramore.Brighter.Azure.Tests/Paramore.Brighter.Azure.Tests.csproj --filter 'FullyQualifiedName~Paramore.Brighter.Azure.Tests.Transformers.Configuration|FullyQualifiedName~Paramore.Brighter.Azure.Tests.Scheduler'
```

Run the full suite when the existing Blob Storage endpoint is available and Azure CLI credentials are configured:

```bash
dotnet test tests/Paramore.Brighter.Azure.Tests/Paramore.Brighter.Azure.Tests.csproj
```
