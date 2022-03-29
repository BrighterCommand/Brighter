# Web API and EF Core Example
This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brigher and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

## Enviroments

*Development* - runs locally on your machine, uses Sqlite as a data store; uses RabbitMQ for messaging, can be launched invidvidually from the docker compose file; it represents a typical setup for development.

*Production* - runs in Docker, uses MySql as a data store; uses RabbitMQ for messaging; it emulates a possible production environment.

We provide launchSetting.json files for both, which allows you to run Production; you should launch MySQl and RabbitMQ from the docker compose file; useful for debugging MySQL operations.

In case you are using Command Line Interface for running the project, consider adding --launch-profile:

```sh
dotnet run --launch-profile Production -d
```
## Architecture

### GreetingsAPI

We follow a _ports and adapters_ archtectural style, dividing the app into the following modules:

* **GreetingsAdapters**: The adapters module, handles the primary adapter of HTTP requests and responses to the app

 * **GreetingsPorts**: the ports module, handles requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. The secondary port of the EntityGateway handles access to the DB via EF Core. We choose to treat EF Core as a port, not an adapter itself, here, as it wraps our underlying adapters for Sqlite or MySql.

* **GreetingsEntities**: the domain model (or application in ports & adapaters). In a fuller app, this would contain the logic that has a dependency on entity state.

We 'depend inwards' i.e. **GreetingsAdapters -> GreetingsPorts -> GreetingsEntities**

The assemblies migrations: **Greetings_MySqlMigrations** and **Greetings_SqliteMigrations** hold generated code to configure the Db. Consider this adapter layer code - the use of separate modules allows us to switch migration per enviroment.

### SalutationAnalytics

This listens for a GreetingMade message and stores it. It demonstrates listening to a queue. It also demonstrates the use of scopes provided by Brighter's ServiceActivator, which work with EFCore. These support writing to an Outbox when this component raises a message in turn.

We don't listen to that message, and without any listeners the RabbitMQ will drop the message we send, as it has no queues to give it to. We don't listen because we would just be repeating what we have shown here.

We also add an Inbox here. The Inbox can be used to de-duplicate messages. In messaging the guarantee is 'at least once' if you use a technique such as an Outbox to ensure sending. This means we may receive a message twice. We either need, as in this case, to use an Inbox to de-duplicate, or we need to be idempotent such that receiving the message multiple times would result in the same outcome.


## Build and Deploy

### Building

Use the build.sh file to:

- Build both GreetingsAdapters and GreetingsWatcher and publish it to the out directory. The Dockerfile assumes the app will be published here. 
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

### Sqlite Database Read-Only Errors

A Sqlite database will only have permissions for the process that created it. This can result in you receiving read-only errors between invocations of the sample. You either need to alter the permissions on your Db, or delete it between runs.

Maintainers, please don't check the Sqlite files into source control.

### Queue Creation and Dropped Messages

Queues are created by consumers. This is because publishers don't know who consumes them, and thus don't create their queues. This means that if you run a producer, such as GreetingsWeb, and use tests.http to push in greetings, although a message will be published to RabbitMQ, it won't have a queue to be delivered to and will be dropped, unless you have first run the SalutationAnalytics worker to create the queue.

Generally, the rule of thumb is: start the consumer and *then* start the producer.

You can spot this by looking in the [RabbitMQ Management console](http://localhost:1567) and noting that no queue is bound to the routing key in th exchange.
You can use default credentials for the RabbitMQ Management console:
```sh
user :guest
passowrd: guest
```
### Connection issue with the RabbitMQ
When running RabbitMQ from the docker compose file (without any additional network setup, etc) your RabbitMQ instance in docker will still be accessible by **localhost** as a host name. Consider this when running your application in the Production environment.  
In Production the application by default will have:
```sh
amqp://guest:guest@rabbitmq:5672
```

as an Advanced Message Queuing Protocol (AMQP) connection string.

you will not be able to connect to the RabbitMQ instaince in the docker container with 

## Tests

We provide a tests.http file (supported by both JetBrains Rider and VS Code with the REST Client plugin) to allow you to test operations.
