# Test Infrastructure Commands

Skills for discovering container runtimes and running integration tests that require infrastructure.

## Available Commands

### `/run-tests <suite>`

Discovers the available container runtime (Podman or Docker Desktop), starts the required infrastructure using docker-compose files, runs the specified test suite, and reports results.

**Suites:**
- `rmq` — RabbitMQ (RMQ Async + Sync tests)
- `kafka` — Kafka + Zookeeper + Schema Registry
- `asb` — Azure Service Bus (cloud, no containers)
- `all` — All transport test suites

**Options:**
- `--infra-only` — Start infrastructure without running tests
- `--tests-only` — Run tests assuming infrastructure is already running

**Examples:**
```
/run-tests rmq
/run-tests kafka --infra-only
/run-tests all
```

## Infrastructure Mapping

| Suite | Compose File | Ports |
|---|---|---|
| RabbitMQ | `docker-compose-rmq.yaml` | 5672, 15672 |
| Kafka | `docker-compose-kafka.yaml` | 9092, 8081, 9021 |
| Azure SB | Cloud service | N/A |
