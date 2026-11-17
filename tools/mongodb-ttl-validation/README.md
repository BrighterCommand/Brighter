# MongoDB TTL validation

Run the local working tree against MongoDB 7 in Docker, including uncommitted
changes. The runner includes the .NET 10 SDK and .NET 9 runtime; no host .NET
installation is needed. Use Docker Desktop or Docker Engine with a recent
Compose v2+ plugin. The first run needs internet access to download images and
restore NuGet packages.

The image creates or reuses a container user matching your host UID/GID and
checks NuGet initialization as that user during the image build. Inherited .NET
mutex files are removed inside the image so they cannot retain another user's
permissions. Test results are written with your host UID/GID.

From the repository root:

```bash
./tools/mongodb-ttl-validation/run.sh
```

This builds the MongoDB library for all its target frameworks, then runs the TTL
test class and the complete `Paramore.Brighter.MongoDb.Tests` project on both
`net9.0` and `net10.0`. Failures produce a nonzero exit code. Test failures on one
framework do not prevent the other framework from running. Image, startup,
restore, and library-build failures stop the run.

For just the TTL regression tests on both frameworks:

```bash
./tools/mongodb-ttl-validation/run.sh --ttl-only
```

The TTL tests include BSON field selection, configured date serialization,
legacy-index migration, real expiration, and lock reacquisition. MongoDB's TTL
monitor runs every second in this environment to keep the expiration test fast.
The full suite also uses a separate single-node replica set for transaction tests.

## Results and cleanup

Each invocation prints its results directory:

```text
artifacts/mongodb-ttl/<run-id>/
```

It contains:

- `summary.tsv`: test-suite results for each framework, after restore/build succeeds.
- `net9.0/` and `net10.0/`: console output and TRX results for each suite.
- `run.log`, `restore.log`, and `library-build.log`: runner and build diagnostics.
- `image-build.log`, `startup.log`, and `mongodb.log`: Docker and database diagnostics.
- `dotnet-info.log`, `images.txt`, and `compose.resolved.yaml`: environment details.
- `exit-code.txt`: final script exit status; zero means the requested checks passed.
- `cleanup.sh`: removes only this invocation's containers and data volumes.

By default, the script collects logs and cleans up its Docker resources on
success, failure, or interruption. Results and the reusable Docker build cache
remain. To retain the databases for inspection:

```bash
./tools/mongodb-ttl-validation/run.sh --ttl-only --keep
```

Run the printed `cleanup.sh` when finished. If the terminal or Docker daemon
stops before automatic cleanup can finish, that same script can be run later:

```bash
bash artifacts/mongodb-ttl/<run-id>/cleanup.sh
```

## Isolation

Each run has a unique Compose project and fresh database volumes. It uses
Docker's existing default bridge, so it needs no new network or subnet, even when
Docker's automatic address pools are exhausted. Both databases bind only to
`127.0.0.1` inside their shared container network namespace. Other bridge
containers cannot connect to them through that namespace's external interface.
No ports are published to the host.

The test runner shares the same namespace: the authenticated server uses port
27017 and the replica set uses port 27018. The suite's hard-coded `localhost`
addresses resolve inside that namespace, not to a host MongoDB instance.
Cleanup leaves Docker's existing bridge and unrelated networks intact.

The checkout is mounted read-only and copied into the runner before building.
Local `bin`, `obj`, Git metadata, and previous results are excluded. Build outputs
stay inside the runner; only validation logs and reports are written back to the
ignored `artifacts/` directory. No commits or pushes are made.

To repeat the run against another MongoDB image:

```bash
BRIGHTER_TTL_MONGO_IMAGE=mongo:8 ./tools/mongodb-ttl-validation/run.sh
```

Validated on 2026-11-08 against MongoDB 7.0.45 on Linux ARM64: all 124 TTL cases
and all 217 full-suite cases passed on each of .NET 9 and .NET 10, with no skipped
tests. The full suite includes the TTL cases. Real expiration, lock reacquisition,
and legacy-index migration passed. The library built for all four target
frameworks, and automatic cleanup completed successfully.
