# Web API and Dapper Example
This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brighter and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

## Architecture

### GreetingsWeb

We follow a _ports and adapters_ architectural style, dividing the app into the following modules:

* **GreetingsWeb**: The adapters' module, handles the primary adapter of HTTP requests and responses to the app
* **GreetingsApp**: The application module, handles the ports and domain model
    - ports: handle requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries.
    - domain: in a fuller app, this would contain the logic that has a dependency on entity state.
* **GreetingsSweeper** : This reads the Outbox table and sends messages to the broker. By default, the `AddGreetingHandlerAsync.cs` will clear the outbox immediately so the Sweeper will only send the message on a failure to talk to the broker. If you comment out the ClearOutbox line from the handler, you will be able to fall back to using the Sweeper to send the message.

We 'depend on inwards' i.e. **GreetingsWeb -> GreetingsApp**

The assemblies migrations: **Greetings_MySqlMigrations** and **Greetings_SqliteMigrations** hold generated code to configure the Db. Consider this adapter layer code - the use of separate modules allows us to switch migration per environment.

Note that you will need to run the Sweeper in a separate terminal window to the rest of the app.

In the Sweeper appsettings.*.json file you will need to set the path to the Outbox database. This is the same database as the main app uses, but the Sweeper needs to know where it is to read the Outbox. Using an absolute path is recommended, but requires you to use values from your machine.

### SalutationAnalytics

This listens for a GreetingMade message and stores it. It demonstrates listening to a queue. It also demonstrates the use of scopes provided by Brighter's ServiceActivator. These support writing to an Outbox when this component raises a message in turn.

We follow a _ports and adapters_ architectural style, dividing the app into the following modules:

* **SalutationAnalytics**: The adapters' module, handles the primary adapter of HTTP requests and responses to the app
* **SalutationsApp**: The application module, handles the ports and domain model
    - ports: handle requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. 
    - domain: in a fuller app, this would contain the logic that has a dependency on entity state.

* **SalutationsSweeper**: This reads the Outbox table and sends messages to the broker. By default, the `GreetingMadeHandlerAsync.cs` will clear the outbox immediately so the Sweeper will only send the message on a failure to talk to the broker. If you comment out the ClearOutbox line from the handler, you will be able to fall back to using the Sweeper to send the message. By default transports that do not store messages when there are no subcribers, such as RabbitMQ will drop the sent message if you do not create a subscriber for it, as this project does not contain a subscriber to SalutationReceived.

We 'depend on inwards' i.e. **SalutationsAnalytics -> GreetingsApp**

We also add an Inbox here. The Inbox can be used to de-duplicate messages. In messaging, the guarantee is 'at least once' if you use a technique such as an Outbox to ensure sending. This means we may receive a message twice. We either need, as in this case, to use an Inbox to de-duplicate, or we need to be idempotent such that receiving the message multiple times would result in the same outcome.

## Telemetry

The apps use OpenTelemetry to provide observability. This is configured in the `Program.cs` or `Startup.cs` file. 

Docker files are supported for an Open Telemetry Collector that exports telemetry to Jaeger and metrics to Prometheus. There is a supported config file for the Open Telemetry collector at the root. You can view the telemetry in the [Jaeger UI](http://localhost:16686)

Alternatively you can use [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/overview?tabs=bash) dashbaord to view the telemetry, which removes the need for running the collector and Jaeger. You can run it using Docker with:

`docker run --rm -it -p 18888:18888 -p 4317:18889 -d --name aspire-dashboard -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS='true' mcr.microsoft.com/dotnet/aspire-dashboard:8.0.0`

### Configuration

We offer support for a range of common SQL stores (MySQL, PostgreSQL, SQL Server) using Docker.

We support Docker hosted messaging brokers, either RabbitMQ or Kafka.

Configuration is via Environment variables. The following are supported:

- BRIGHTER_GREETINGS_DATABASE => "Sqlite", "MySql", "Postgres", "MsSQL"
- BRIGHTER_TRANSPORT => "RabbitMQ", "Kafka"

We provide launchSetting.json files for all of these, which allows you to run Production with the appropriate db; you should launch your SQL data store and broker from the docker compose file.

In case you are using Command Line Interface for running the project, consider adding --launch-profile:

```sh
dotnet run --launch-profile XXXXXX -d
```

### Possible issues
#### Sqlite
**Sqlite Database Read-Only Errors**

A Sqlite database will only have permissions for the process that created it. This can result in you receiving read-only errors between invocations of the sample. You either need to alter the permissions on your Db, or delete it between runs.

Maintainers, please don't check the Sqlite files into source control.

**Sqlite Database Read-Only Errors**

A Sqlite database will only have permissions for the process that created it. This can result in you receiving read-only errors between invocations of the sample. You either need to alter the permissions on your Db, or delete it between runs.

Maintainers, please don't check the Sqlite files into source control.

#### RabbitMQ

**Queue Creation and Dropped Messages**

Queues are created by consumers. This is because publishers don't know who consumes them, and thus don't create their queues. This means that if you run a producer, such as GreetingsWeb, and use tests.http to push in greetings, although a message will be published to RabbitMQ, it won't have a queue to be delivered to and will be dropped, unless you have first run the SalutationAnalytics worker to create the queue.

Generally, the rule of thumb is: start the consumer and *then* start the producer.

You can spot this by looking in the [RabbitMQ Management console](http://localhost:1567) and noting that no queue is bound to the routing key in the exchange.
You can use default credentials for the RabbitMQ Management console:
```sh
user :guest
passowrd: guest
```
**Connection issue with the RabbitMQ**
When running RabbitMQ from the docker compose file (without any additional network setup, etc.) your RabbitMQ instance in docker will still be accessible by **localhost** as a host name. Consider this when running your application in the Production environment.
In Production, the application by default will have:
```sh
amqp://guest:guest@rabbitmq:5672
```

as an Advanced Message Queuing Protocol (AMQP) connection string.  
So one of the options will be replacing AMQP connection string with:
```sh
amqp://guest:guest@localhost:5672
```
In case you still struggle, consider following these steps: [RabbitMQ Troubleshooting Networking](https://www.rabbitmq.com/troubleshooting-networking.html)


#### Helpful documentation links
* [Brighter technical documentation](https://paramore.readthedocs.io/en/latest/index.html)
* [Rabbit Message Queue (RMQ) documentation](https://www.rabbitmq.com/documentation.html)
* [Kafka documentation](https://kafka.apache.org/documentation/)
