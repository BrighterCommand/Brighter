# Table of contents
- [Web API and Dapper Example](#web-api-and-dapper-example)
    * [Environments](#environments)
    * [Architecture](#architecture)
        + [GreetingsAPI](#greetingsapi)
        + [SalutationAnalytics](#salutationanalytics)
    * [Acceptance Tests](#tests)
    * [Possible issues](#possible-issues)
        +[Sqlite Database Read-Only Errors](#sqlite-database-read-only-errors)
        + [RabbitMQ Queue Creation and Dropped Messages](#queue-creation-and-dropped-messages)
        + [Helpful documentation links](#helpful-documentation-links)

# Web API and Dapper Example
This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brighter and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

## Environments

*Development* 

- Uses a local Sqlite instance for the data store. 
- We support Docker hosted messaging brokers, either RabbitMQ or Kafka. 

*Production* 
- We offer support for a range of common SQL stores (MySQL, PostgreSQL, SQL Server) using Docker. 
- We support Docker hosted messaging brokers, either RabbitMQ or Kafka.

### Configuration

Configuration is via Environment variables. The following are supported:

- BRIGHTER_GREETINGS_DATABASE => "Sqlite", "MySql", "Postgres", "MsSQL"
- BRIGHTER_TRANSPORT => "RabbitMQ", "Kafka"

We provide launchSetting.json files for all of these, which allows you to run Production with the appropriate db; you should launch your SQL data store and broker from the docker compose file. 

In case you are using Command Line Interface for running the project, consider adding --launch-profile:

```sh
dotnet run --launch-profile XXXXXX -d
```

## Architecture

### GreetingsAPI

We follow a _ports and adapters_ architectural style, dividing the app into the following modules:

* **GreetingsAdapters**: The adapters' module, handles the primary adapter of HTTP requests and responses to the app

* **GreetingsPorts**: the ports' module, handles requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. 
In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. The secondary port uses either an IAmARelationalDbConnectionProvider or an IAmATransactionConnectionProvider. 
Both of these are required for Brighter's Outbox. If you register the former with ServiceCollection, you can use it use it for your own queries; we use Dapper with that connection. 
The latter is used by the Outbox to ensure that the message is sent within the same transaction as your writes to the entity and you should use its transaction support for transactional messaging. 

* **GreetingsEntities**: the domain model (or application in ports & adapters). In a fuller app, this would contain the logic that has a dependency on entity state.

We 'depend on inwards' i.e. **GreetingsAdapters -> GreetingsPorts -> GreetingsEntities**

The assemblies migrations: **Greetings_Migrations** hold code to configure the Db. 

GreetingsAPI uses an Outbox for Transactional Messaging - the write to the entity and the message store are within the same transaction and the message is posted from the message store.

### SalutationAnalytics

* **SalutationAnalytics** The adapter subscribes to GreetingMade messages. It demonstrates listening to a queue. It also demonstrates the use of scopes provided by Brighter's ServiceActivator, which work with Dapper. These support writing to an Outbox when this component raises a message in turn.
   
* **SalutationPorts** The ports' module, handles requests from the primary adapter to the domain, and requests to secondary adapters. It writes to the entity store and sends another message. We don't listen to that message. Note that without any listeners RabbitMQ will drop the message we send, as it has no queues to give it to. 
If you want to see the messages produced, use the RMQ Management Console (localhost:15672) or Kafka Console (localhost:9021). (You will need to create a subscribing queue in RabbitMQ)

* **SalutationEntities** The domain model (or application in ports & adapters). In a fuller app, this would contain the logic that has a dependency on entity state.

We add an Inbox as well as the Outbox here. The Inbox can be used to de-duplicate messages. In messaging, the guarantee is 'at least once' if you use a technique such as an Outbox to ensure sending. This means we may receive a message twice. We either need, as in this case, to use an Inbox to de-duplicate, or we need to be idempotent such that receiving the message multiple times would result in the same outcome.

The assemblies migrations: **Salutations_Migrations** hold code to configure the Db.

## Acceptance Tests

We provide a tests.http file (supported by both JetBrains Rider and VS Code with the REST Client plugin) to allow you to test operations on the API.

## Possible issues

#### Sqlite Database Read-Only Errors

A Sqlite database will only have permissions for the process that created it. This can result in you receiving read-only errors between invocations of the sample. You either need to alter the permissions on your Db, or delete it between runs.

Maintainers, please don't check the Sqlite files into source control.

#### RabbitMQ Queue Creation and Dropped Messages

For Rabbit MQ, queues are created by consumers. This is because publishers don't know who consumes them, and thus don't create their queues. This means that if you run a producer, such as GreetingsWeb, and use tests.http to push in greetings, although a message will be published to RabbitMQ, it won't have a queue to be delivered to and will be dropped, unless you have first run the SalutationAnalytics worker to create the queue.

Generally, the rule of thumb is: start the consumer and *then* start the producer.

You can spot this by looking in the [RabbitMQ Management console](http://localhost:15672) and noting that no queue is bound to the routing key in the exchange.
You can use default credentials for the RabbitMQ Management console:
```sh
user :guest
passowrd: guest
```
#### Helpful documentation links
* [Brighter technical documentation](https://paramore.readthedocs.io/en/latest/index.html)
* [Rabbit Message Queue (RMQ) documentation](https://www.rabbitmq.com/documentation.html)
* [Kafka documentation](https://kafka.apache.org/documentation/)

