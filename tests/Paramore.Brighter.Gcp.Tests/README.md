# Paramore.Brighter.Gcp.Tests — running the Spanner suites

The Spanner-backed suites (`Spanner/BoxProvisioning`, `Spanner/Outbox`, `Spanner/Inbox`) run against
the [Cloud Spanner emulator](https://cloud.google.com/spanner/docs/emulator). The emulator ships with
**no instance or database**, so it must be provisioned before the tests can issue DDL — otherwise the
first call fails with `Instance not found: projects/brighter-tests/instances/brighter-spanner`.

Provisioning is now automatic: `docker-compose-spanner.yaml` includes a one-shot `spanner-init`
service that creates the `brighter-spanner` instance and `brightertests` database on every
`compose up`. It is idempotent, so re-running is safe.

## One-step start

From the repository root:

```bash
docker compose -f docker-compose-spanner.yaml up -d --wait
```

`--wait` is important: it makes Compose block until `spanner-init` has finished provisioning (it runs
once and exits 0). Without `--wait`, `up -d` returns as soon as the containers *start*, and a test
launched immediately afterwards can race the still-running init.

Then run the tests with the emulator env vars set:

```bash
export SPANNER_EMULATOR_HOST=localhost:9010 GOOGLE_CLOUD_PROJECT=brighter-tests
dotnet test tests/Paramore.Brighter.Gcp.Tests -f net9.0 --filter "FullyQualifiedName~Spanner"
```

(Use `--filter "FullyQualifiedName~BoxProvisioning"` for just the BoxProvisioning subset.)

> **VPN / port conflict?** If host port `9010` is already bound (e.g. by a VPN tunnel), remap the
> published ports with `SPANNER_GRPC_PORT` / `SPANNER_REST_PORT` and point the tests at the new gRPC
> port:
>
> ```bash
> SPANNER_GRPC_PORT=9110 docker compose -f docker-compose-spanner.yaml up -d --wait
> export SPANNER_EMULATOR_HOST=localhost:9110
> ```
>
> Both default to the standard ports when unset. The `spanner-init` sidecar is unaffected either way —
> it reaches the emulator over the Compose network at `spanner:9020`, not via a published host port.

## Starting the emulator outside of Compose

If you start the emulator by some other means (not via one of the compose files), provision it with
the standalone script, which performs the same wait-then-create steps and is likewise idempotent:

```bash
bash ./setup-spanner-emulator.sh
```

## Notes

- The emulator loses all state on container recreation; because `spanner-init` runs on every
  `compose up`, the instance + database are always re-created — no manual step required.
- The `spanner-init` sidecar reaches the emulator over the Compose network at `spanner:9020` (its
  REST/admin port), independent of whichever host ports are published.
