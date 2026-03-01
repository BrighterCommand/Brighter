# RabbitMQ Mutual TLS Test Harness

A simple Web API test harness for demonstrating and testing RabbitMQ mutual TLS (mTLS) functionality with Brighter.

## Overview

This application provides a minimal example of:
- Publishing messages to RabbitMQ using mTLS
- Consuming messages from RabbitMQ using mTLS
- Simple Todo item creation and event handling

## Prerequisites

1. **Docker** - For running RabbitMQ with mTLS enabled
2. **.NET 8.0 SDK or higher** - For building and running the application
3. **Test Certificates** - Generated using the provided script

## Quick Start

### 1. Generate Test Certificates

From the repository root:

```bash
cd tests
./generate-test-certs.sh
```

This creates:
- `tests/certs/ca-cert.pem` - Certificate Authority
- `tests/certs/server-cert.pem` - Server certificate
- `tests/certs/server-key.pem` - Server private key
- `tests/certs/client-cert.pfx` - Client certificate (used by this app)

### 2. Fix Certificate Permissions

The server private key needs to be readable by the RabbitMQ container:

```bash
cd tests
chmod 644 certs/server-key.pem
```

### 3. Start RabbitMQ with mTLS

```bash
cd tests
docker-compose -f docker-compose.rabbitmq-mtls.yml up -d
```

This starts RabbitMQ on:
- AMQPS port: `5671` (mTLS enabled)
- Management UI: `http://localhost:15672` (username: `guest`, password: `guest`)

**Note**: If the container fails to start, check logs with `docker-compose -f docker-compose.rabbitmq-mtls.yml logs` and ensure the server-key.pem file has correct permissions.

### 4. Run the Application

From the `samples/WebAPI/WebAPI_mTLS_TestHarness/TodoApi` directory:

```bash
dotnet run
```

The API will start on `http://localhost:5000` (or use `dotnet run --launch-profile https` for HTTPS on port 5001).

### 5. Test the Endpoints

#### Using Swagger UI

Navigate to `http://localhost:5000/swagger` and use the interactive UI to test the endpoints.

#### Using curl

**Create a Todo item** (publishes to RabbitMQ):
```bash
curl -X POST "http://localhost:5000/todos?title=Buy%20groceries&isCompleted=false"
```

**Check health**:
```bash
curl -X GET "http://localhost:5000/health"
```

#### Expected Behavior

1. When you POST to `/todos`, the application:
   - Creates a `TodoCreated` event
   - Publishes it to RabbitMQ via mTLS
   - Returns the created todo details

2. The same application is also consuming messages:
   - The `TodoCreatedHandler` receives the published event
   - Logs the todo details to the console
   - You should see log output like:
     ```
     Received TodoCreated event: Buy groceries, IsCompleted: False, CreatedAt: 2025-12-28T...
     ```

## Configuration

Configuration can be modified in `appsettings.json` or via environment variables:

```json
{
  "RabbitMQ": {
    "Uri": "amqps://localhost:5671",
    "ClientCertPath": null,  // Defaults to tests/certs/client-cert.pfx
    "ClientCertPassword": "test-password"
  }
}
```

## Key Implementation Details

### mTLS Configuration

The RabbitMQ connection is configured with mTLS in `Program.cs`:

```csharp
var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
    Exchange = new Exchange("todo.exchange"),
    ClientCertificatePath = certPath,
    ClientCertificatePassword = certPassword,
    TrustServerSelfSignedCertificate = true // For test environment
};
```

### Message Flow

```
HTTP POST /todos
    ↓
TodoCreated event created
    ↓
IAmACommandProcessor.PostAsync()
    ↓
RabbitMQ (via mTLS)
    ↓
TodoCreatedHandler.HandleAsync()
    ↓
Log output
```

### Pub/Sub Pattern

- **Publisher**: The API endpoint publishes `TodoCreated` events
- **Subscriber**: The `TodoCreatedHandler` subscribes to and processes events
- **Exchange**: `todo.exchange` (Direct exchange)
- **Routing Key**: `TodoCreated`
- **Queue**: `TodoChannel` (created automatically)

## Troubleshooting

### Certificate Errors

**Error**: `Client certificate file not found`

**Solution**: Ensure certificates are generated and the path in configuration is correct.

```bash
cd tests
./generate-test-certs.sh
```

### Connection Refused

**Error**: `Connection refused` or `No connection could be made`

**Solution**: Ensure RabbitMQ Docker container is running:

```bash
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml ps
```

### Messages Not Being Consumed

**Issue**: Messages are published but the handler doesn't log anything.

**Check**:
1. Verify the `ServiceActivatorHostedService` is running (check console logs on startup)
2. Check RabbitMQ Management UI to see if queue is bound to exchange
3. Look for any errors in the application logs

### RabbitMQ Management UI

Access at `http://localhost:15672`:
- Username: `guest`
- Password: `guest`

Check:
- **Exchanges** tab: Should see `todo.exchange`
- **Queues** tab: Should see `TodoChannel`
- **Connections** tab: Should see SSL/TLS connection with:
  - **SSL/TLS**: Yes
  - **Peer cert subject**: CN=brighter-test-client
  - **Peer cert issuer**: CN=brighter-test-ca

## Verifying mTLS is Working

To confirm mutual TLS is actually being used:

1. **Check the connection in RabbitMQ Management UI** at `http://localhost:15672`:
   - Navigate to the **Connections** tab
   - You should see your connection with SSL/TLS enabled
   - Click on the connection name to see certificate details

2. **Port verification**: The application connects to port `5671` (AMQPS with mTLS), not `5672` (regular AMQP)

3. **Certificate requirement**: The RabbitMQ server is configured with `fail_if_no_peer_cert = true`, meaning it will reject connections without a valid client certificate. If mTLS wasn't working, the application would fail to connect.

## Cleanup

Stop and remove the RabbitMQ container:

```bash
docker-compose -f tests/docker-compose.rabbitmq-mtls.yml down
```

Remove generated certificates (optional):

```bash
rm -rf tests/certs/
```

## Additional Resources

- [Brighter Documentation](https://www.goparamore.io/)
- [RabbitMQ TLS Support](https://www.rabbitmq.com/ssl.html)
- [RabbitMQ Management Plugin](https://www.rabbitmq.com/management.html)
