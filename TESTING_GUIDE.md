# Testing Guide - RabbitMQ mTLS

## Quick Reference for Building and Testing

**Important**: Always run from repository root: `/home/darren/w/brighter/Brighter`

---

## Build Only What You Need

### Build RMQ Gateway Projects Only

```bash
# Async gateway
dotnet build src/Paramore.Brighter.MessagingGateway.RMQ.Async/Paramore.Brighter.MessagingGateway.RMQ.Async.csproj --no-incremental

# Sync gateway
dotnet build src/Paramore.Brighter.MessagingGateway.RMQ.Sync/Paramore.Brighter.MessagingGateway.RMQ.Sync.csproj --no-incremental
```

### Build Test Projects Only

```bash
# Async tests
dotnet build tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj --no-incremental

# Sync tests
dotnet build tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj --no-incremental
```

---

## Unit Tests (No Docker Required)

These test configuration without needing RabbitMQ running:

```bash
# Async unit tests
dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj \
  --filter "Category=MutualTLS&Requires!=Docker-mTLS" \
  --framework net10.0

# Sync unit tests
dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj \
  --filter "Category=MutualTLS&Requires!=Docker-mTLS" \
  --framework net10.0

# Both together
dotnet test \
  --filter "Category=MutualTLS&Requires!=Docker-mTLS" \
  --framework net10.0 \
  tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj \
  tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj
```

**Expected Result**: 14 tests pass (7 Async + 7 Sync)

---

## Acceptance Tests (Docker Required)

These test actual mTLS connections to RabbitMQ.

### One-Time Setup

```bash
# Install Docker (if not already installed)
sudo apt update
sudo apt install docker.io docker-compose -y
sudo systemctl start docker
sudo systemctl enable docker

# Add yourself to docker group (no more sudo needed)
sudo usermod -aG docker $USER
newgrp docker

# Generate certificates (only needed once)
cd tests
./generate-test-certs.sh
cd ..
```

### Running Acceptance Tests

```bash
# 1. Start RabbitMQ with mTLS
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml up -d

# 2. Wait for RabbitMQ to start
sleep 10

# 3. Verify RabbitMQ is running
docker ps | grep rabbitmq

# 4. Run Async acceptance tests
dotnet test tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj \
  --filter "Category=MutualTLS" \
  --framework net10.0

# 5. Run Sync acceptance tests
dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj \
  --filter "Category=MutualTLS" \
  --framework net10.0

# 6. Stop RabbitMQ
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml down
```

**Expected Result**: 32 tests pass (16 Async + 16 Sync)

### One-Liner for Everything

```bash
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml up -d && \
sleep 10 && \
dotnet test --filter "Category=MutualTLS" --framework net10.0 \
  tests/Paramore.Brighter.RMQ.Async.Tests/Paramore.Brighter.RMQ.Async.Tests.csproj \
  tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj && \
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml down
```

---

## Troubleshooting

### "TESTRUNABORT" in Other Test Projects

If you see errors in projects like `Paramore.Brighter.Base.Test`, ignore them - they're unrelated to your changes. Always test **specific projects** to avoid running the entire solution.

### Docker Permission Denied

```bash
# Add yourself to docker group
sudo usermod -aG docker $USER

# Apply group membership (or log out and back in)
newgrp docker
```

### .NET 9.0 Missing

```bash
# Always use net10.0 framework
dotnet test --framework net10.0 ...
```

### Check Docker Logs

```bash
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml logs -f
```

### Clean Start

```bash
# Stop everything
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml down

# Remove old certificates
rm -rf tests/certs

# Regenerate certificates
cd tests && ./generate-test-certs.sh && cd ..

# Start fresh
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml up -d
```

---

## Test Breakdown

### Unit Tests (7 per variant)
1. When certificate is configured, SSL is enabled
2. When no certificate is configured, SSL is not configured
3. When certificate object and path both set, object takes precedence
4. When certificate path is provided, certificate is loaded
5. When certificate file does not exist, throws FileNotFoundException
6. When certificate file is invalid, throws InvalidOperationException
7. When certificate configuration is optional, backwards compatibility is maintained

### Acceptance Tests (9 per variant)
1. When connecting with client certificate, can publish message
2. When connecting with mTLS, can publish and receive message
3. When publishing with trace context over mTLS (TraceParent preserved)
4. When publishing with trace state over mTLS (TraceState preserved)
5. When publishing with baggage over mTLS (Baggage preserved)
6. When publishing with all trace context over mTLS
7. When loading certificate from file path with trace context
8. When using mTLS with quorum queues (trace context)
9. When using mTLS with quorum queues (baggage)

---

## Windows Testing

On Windows, use PowerShell:

```powershell
# Generate certificates
cd tests
.\generate-test-certs.ps1
cd ..

# Run tests (same dotnet commands work on Windows)
```
