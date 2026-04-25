# TickerQ Brighter Sample

This project demonstrates the use of [Brighter](https://www.goparamore.io/) with [TickerQ](https://github.com/AboubakrNasef/TickerQ) for message scheduling and processing. It uses .NET Aspire to orchestrate a Producer, a Consumer, and a RabbitMQ broker.

## Project Structure

- **Greeting.AppHost**: The .NET Aspire AppHost that orchestrates the services.
- **Greeting.Producer**: An ASP.NET Core application that schedules greeting messages using TickerQ and Brighter.
- **Greeting.Consumer**: A worker service that consumes greeting messages and processes them.
- **Greeting.Models**: Shared message models.
- **Greeting.ServiceDefaults**: Shared service configuration defaults.

## Endpoints

### Producer (`Greeting.Producer`)
- **Root**: `GET /` - Returns "helloProducer".
- **Send One**: `POST /send-one` - Manually schedules a single greeting message.
- **Send Multiple**: `POST /send-multiple` - Manually schedules multiple greeting messages.
- **TickerQ Dashboard**: `GET /dashboard` - Interactive dashboard to manage and monitor scheduled messages.
- **Health/Metrics (via ServiceDefaults)**:
    - `/health`: Service health check.
    - `/alive`: Service liveness check.

### Consumer (`Greeting.Consumer`)
- **Root**: `GET /` - Returns "helloConsumer".
- **Health/Metrics (via ServiceDefaults)**:
    - `/health`: Service health check.
    - `/alive`: Service liveness check.

### AppHost (`Greeting.AppHost`)
- **Dashboard**: The .NET Aspire dashboard (accessible at the URL provided in the console when running) provides logs, metrics, and tracing for all services.

## How to Run

1.  **Prerequisites**:
    - .NET 8.0 or later SDK.
    - Docker Desktop or Podman (for RabbitMQ and SQLite storage).
2.  **Run the Project**:
    - Open a terminal in the root directory (`TickerQ`).
    - Run `dotnet run --project Greeting.AppHost/Greeting.AppHost.csproj`.
3.  **Explore**:
    - Open the Aspire Dashboard URL displayed in the console.
    - Navigate to the Producer's root endpoint to see it running.
    - Visit `/dashboard` on the Producer to see the TickerQ scheduler in action.
