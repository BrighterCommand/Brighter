# Web API and EF Core Example
This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brigher and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

## Enviroments

*Development* - runs locally on your machine, uses Sqlite as a data store; uses RabbitMQ for messaging, can be launched invidvidually from the docker compose file; it represents a typical setup for development

*Production* - runs in Docker, uses MySql as a data store; uses RabbitMQ for messaging; it emulates a possible production environment.

We provide launchSetting.json files for both, which allows you to run Production; you should launch MySQl and RabbitMQ from the docker compose file; useful for for debugging MySQL operations.


## Architecture

### GreetingsAPI

We follow a ports and adapters archtectural style, dividing the app into the following modules:

**GreetingsAdapters**: The adapters module, handles the primary adapter of HTTP requests and responses to the app

**GreetingsPorts**: the ports module, handles requests from the primary adapter (HTTP) to the domain, and requests to secondary adapters. In a fuller app, the handlers for the primary adapter would correspond to our use case boundaries. The secondary port of the EntityGateway handles access to the DB via EF Core. We choose to treat EF Core as a port, not an adapter itself, here, as it wraps our underlying adapters for Sqlite or MySql.

**GreetingsEntities**: the domain model (or application in ports & adapaters). In a fuller app, this would contain the logic that has a dependency on entity state.

We 'depend inwards' i.e. **GreetingsAdapters -> GreetingsPorts -> GreetingsEntities**

The assemblies migrations: **Greetings_MySqlMigrations** and **Greetings_SqliteMigrations** hold generated code to configure the Db. Consider this adapter layer code - the use of separate modules allows us to switch migration per enviroment.

### GreetingsWatcher

This just listens for a GreetingMade message and dumps it to the console. It demonstrates listening to a queue. In this case it is too trivial for a ports & adapters separation, but that approach could be used if there was an entity layer and domain logic.


## Build and Deploy

### Building

Use the build.sh file to:

- Build both GreetingsAdapters and GreetingsWatcher and publish it to the out directory. The Dockerfile assumes the app will be published here. 
- Build the Docker image from the Dockerfile for each.

(Why not use a multi-stage Docker build? We can't do this as the projects here reference projects not NuGet packages for Brighter libraries and there are not in the Docker build context.)

A common error is to change something, forget to run build.sh and use an old Docker image.

### Deploy

We provide a docker compose file to allow you to run a 'Production' environment or to startup RabbitMQ for production

-- docker compose up -d rabbitmq   -- will just start rabbitmq

-- docker compose up -d mysql   -- will just start mysql

and so on

## Tests

We provide a tests.http file (supported by both JetBrains Rider and VS Code with the REST Client plugin) to allow you to test operations.




