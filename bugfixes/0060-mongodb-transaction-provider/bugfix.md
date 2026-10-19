# Bugfix: injected MongoDB transaction provider cannot deposit messages

**Linked issue:** https://github.com/BrighterCommand/Brighter/issues/4616
**Status:** Fixed and verified locally.

## Cause

`IAmAMongoDbTransactionProvider` inherits `IAmABoxTransactionProvider<IClientSession>`,
but `MongoDbOutbox` uses `IClientSessionHandle`. Passing the injected interface to
`DepositPostAsync` infers the former generic type, which fails the command
processor's transaction-type check. `MongoDbUnitOfWork` exposes both contracts,
so callers using the concrete provider or an explicit handle-provider cast can
bypass the problem.

## Fix

The MongoDB provider interface now inherits the handle contract, with matching
XML documentation. The redundant handle interface declaration and the
explicit legacy session implementations were removed from `MongoDbUnitOfWork`, leaving one
transaction type consistent with the driver and outbox.

This is a breaking public API change and must be identified as such in the
release notes. Custom implementations must return `IClientSessionHandle` and
`Task<IClientSessionHandle>`; consumers compiled against the old interface must
be rebuilt. Consumers depending explicitly on the legacy generic session
contract must migrate to the handle contract.

## Regression coverage

`MongoDbInjectedTransactionTests` configures the real MongoDB outbox through
`AddProducers` and resolves `IAmAMongoDbTransactionProvider` from DI. Four cases
cover sync/async deposit and commit/rollback. The provider is passed directly to
the deposit API without a cast. The test asserts that a document and message
are visible inside the transaction, invisible outside before commit, and
persist or disappear together. Committed messages can then be dispatched.

The assertion on the returned session checks that it is a driver-compatible
handle while letting the regression compile against the broken interface.
All four cases failed at deposit with the reported transaction-type mismatch
before the fix, on both target frameworks.

Actual transactions require a replica set. The test accepts
`BRIGHTER_MONGODB_TRANSACTION_TEST_CONNECTION_STRING`, defaulting to
`mongodb://localhost:27018/?replicaSet=rs0&directConnection=true`.
`docker-compose-mongodb.yaml` now includes a MongoDB 7 single-node replica set
on loopback port 27018 alongside the existing standalone test server on 27017.
Its health check initializes the replica set and waits for a writable primary.
The MongoDB CI job starts this service with `--wait` before running the tests.
The tests do not silently skip if infrastructure is missing.

## Validation

Start the test servers, then run the full MongoDB suite:

```sh
docker compose -f docker-compose-mongodb.yaml up -d --wait
dotnet test tests/Paramore.Brighter.MongoDb.Tests/Paramore.Brighter.MongoDb.Tests.csproj --configuration Release
```

- Before the fix: all four regression cases failed with
  `Supplied transaction provider doesn't match configured transaction type.`
  on .NET 9 and .NET 10.
- After the fix: all four regression cases passed on both frameworks.
- Full MongoDB suite: 89 passed, 0 failed, 0 skipped on each framework.
- MongoDB provider Release build: all supported targets (net472, net8.0,
  net9.0, net10.0) succeeded with no warnings or errors.
- Local replica-set startup and readiness were verified through Docker Compose.
  The GitHub Actions workflow itself has not been run remotely.

## Final review

No blocking implementation defects were found. Both DI transaction-type
discovery and the command processor now see a single handle contract. The
MongoDB outbox and EF Core provider already use that contract; no other
implementations of the changed interface were found in the repository.

Docker Compose configuration validation passed, and starting only
`mongo-transactions` with `--wait`, as CI does, also passed after a restart.
Workflow lint produced the same six diagnostics on the base revision and the
working tree, with no new diagnostics from this change. The pre-existing
diagnostics concern package-publishing shell commands, a MariaDB port
expression, and a GCP shell loop.

The release must account for the API break described above. Remote GitHub
Actions validation remains outstanding; local build and test evidence does not
replace the remote workflow result.
