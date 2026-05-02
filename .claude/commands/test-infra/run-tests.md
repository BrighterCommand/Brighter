---
allowed-tools: Read, Bash, Glob, Grep
description: Discover container runtime and run transport tests with infrastructure
argument-hint: <test-suite: rmq|kafka|asb|all> [--infra-only] [--tests-only]
---

# Run Transport Tests with Infrastructure

You are helping the user run integration tests that require container-based infrastructure (RabbitMQ, Kafka, etc.) or cloud services (Azure Service Bus).

## Step 1: Discover Container Runtime

Check which container runtime is available. Both Podman and Docker Desktop may be installed — only one may be running.

```bash
# Check for podman
which podman && podman ps 2>&1 | head -3

# Check for docker
which docker && docker ps 2>&1 | head -3
```

If **podman** is installed but not running:
```bash
podman machine list
# If a machine exists but is stopped:
podman machine start
# If no machine exists:
podman machine init && podman machine start
```

If **docker** is installed but daemon not running, tell the user to start Docker Desktop.

Set the working runtime as `COMPOSE_CMD`:
- If podman is running: `podman compose` (or `podman-compose` if installed)
- If docker is running: `docker compose` (or `docker-compose`)

## Step 2: Map Test Suite to Infrastructure

The docker-compose files are at the project root:

| Test Suite | Compose File | Test Project |
|---|---|---|
| `rmq` | `docker-compose-rmq.yaml` | `tests/Paramore.Brighter.RMQ.Async.Tests/` and `tests/Paramore.Brighter.RMQ.Sync.Tests/` |
| `kafka` | `docker-compose-kafka.yaml` | `tests/Paramore.Brighter.Kafka.Tests/` |
| `asb` | None (cloud service) | `tests/Paramore.Brighter.AzureServiceBus.Tests/` |
| `all` | All of the above | All of the above |

For Azure Service Bus: no containers needed. Check if `ASB_CONNECTION_STRING` environment variable is set, or look for connection strings in test configuration files.

## Step 3: Start Infrastructure (unless `--tests-only`)

```bash
$COMPOSE_CMD -f docker-compose-<service>.yaml up -d
```

Wait for services to be healthy before running tests:
- **RabbitMQ**: `curl -s http://localhost:15672` returns 200 (management UI)
- **Kafka**: check that kafka container is running and port 9092 is listening

## Step 4: Run Tests (unless `--infra-only`)

```bash
dotnet test tests/<TestProject>/ -v quiet
```

Report results with pass/fail counts per target framework.

## Step 5: Cleanup

After tests complete, ask the user if they want to tear down infrastructure:
```bash
$COMPOSE_CMD -f docker-compose-<service>.yaml down
```

## Notes

- The `$USER` argument specifies which suite to run. If not provided, ask.
- Some test suites have long-running integration tests (Kafka tests can take 5+ minutes).
- Use `--timeout 600000` for Kafka tests.
- Always report which container runtime was detected so the user knows the environment state.
