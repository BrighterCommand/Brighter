# Web API and EF Core Example

This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brighter and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

# Architecture

### Outbox

Brighter does have an [Outbox pattern support](https://paramore.readthedocs.io/en/latest/OutboxPattern.html). In case you are new to it, consider reading it before diving deeper.

### GreetingsWeb

We follow a _ports and adapters_ architectural style, dividing the app into the following modules:

* **GreetingsWeb**: The adapters' module, handles the primary adapter of HTTP requests and responses to the app

* **GreetingsApp**: The application module, handles the ports and domain model

- ports: handle requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. 
    - The secondary port of the EntityGateway handles access to the DB via EF Core. We choose to treat EF Core as a port, not an adapter itself, here, as it wraps our underlying adapters for Sqlite or MySql.
- domain: in a fuller app, this would contain the logic that has a dependency on entity state.

We 'depend on inwards' i.e. **GreetingsWeb -> GreetingsApp**

The assemblies migrations: **Greetings_MySqlMigrations** and **Greetings_SqliteMigrations** hold generated code to configure the Db. Consider this adapter layer code - the use of separate modules allows us to switch migration per environment.

### SalutationAnalytics

This listens for a GreetingMade message and stores it. It demonstrates listening to a queue. It also demonstrates the use of scopes provided by Brighter's ServiceActivator. These support writing to an Outbox when this component raises a message in turn.

We follow a _ports and adapters_ architectural style, dividing the app into the following mod§ules:

* **SalutationAnalytics**: The adapters' module, handles the primary adapter of HTTP requests and responses to the app

* **SalutationsApp**: The application module, handles the ports and domain model

- ports: handle requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. 
    - The secondary port of the EntityGateway handles access to the DB via EF Core. We choose to treat EF Core as a port, not an adapter itself, here, as it wraps our underlying adapters for Sqlite or MySql.
- domain: in a fuller app, this would contain the logic that has a dependency on entity state.

We 'depend on inwards' i.e. **SalutationsAnalytics -> GreetingsApp**

We also add an Inbox here. The Inbox can be used to de-duplicate messages. In messaging, the guarantee is 'at least once' if you use a technique such as an Outbox to ensure sending. This means we may receive a message twice. We either need, as in this case, to use an Inbox to de-duplicate, or we need to be idempotent such that receiving the message multiple times would result in the same outcome.

The assemblies migrations: **Salutations_MySqlMigrations** and **Salutations_SqliteMigrations** hold generated code to configure the Db. Consider this adapter layer code - the use of separate modules allows us to switch migration per environment.

## Environments

*Development*

- Uses a local Sqlite instance for the data store.
- We support Docker hosted messaging brokers, either RabbitMQ or Kafka.

*Production*
- We offer support for MySQL using Docker.
- We support Docker hosted messaging brokers, using RabbitMQ.

### Configuration

We provide launchSetting.json files for all of these, which allows you to run Production with the appropriate db; you should launch your SQL data store and broker from the docker compose file.

In case you are using Command Line Interface for running the project, consider adding --launch-profile:

```sh
dotnet run --launch-profile XXXXXX -d
```

## Build and Deploy

### Building

Use the build.sh file to:

- Build both GreetingsAdapters and SalutationAnalytics and publish it to the /out directory. The Dockerfile assumes the app will be published here.
- Build the Docker image from the Dockerfile for each.

(Why not use a multi-stage Docker build? We can't do this as the projects here reference projects not NuGet packages for Brighter libraries and there are not in the Docker build context.)

A common error is to change something, forget to run build.sh and use an old Docker image.

### Deploy

We provide a docker compose file to allow you to run a 'Production' environment or to startup RabbitMQ for production:
```sh
docker compose up -d rabbitmq   # will just start rabbitmq
```

```sh
docker compose up -d mysql   # will just start mysql
```

and so on.

## Tests

We provide a tests.http file (supported by both JetBrains Rider and VS Code with the REST Client plugin) to allow you to test operations.

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

