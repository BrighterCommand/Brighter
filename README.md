| | |
| ------------- | ------------- |
|![canon](https://raw.githubusercontent.com/BrighterCommand/Brighter/master/images/brightercanon-nuget.png) |[Brighter](https://github.com/BrighterCommand/Brighter)|
||Brighter is a framework for building messaging app with .NET and C#. It can be used with an in-memory bus, or for interoperability in a microservices architecture, out of process via a wider range of middleware transports. |
| Version  | [![NuGet Version](http://img.shields.io/nuget/v/paramore.brighter.svg)](https://www.nuget.org/packages/paramore.brighter/)  |
| Download | [![NuGet Downloads](http://img.shields.io/nuget/dt/paramore.brighter.svg)](https://www.nuget.org/packages/Paramore.Brighter/) |
| Documentation  |  [Technical Documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation/)  |
| Source  |https://github.com/BrighterCommand/Brighter |
| Keywords  |task queue, job queue, asynchronous, async, rabbitmq, amqp, sqs, sns, kafka, redis, c#, command, command dispatcher, command  processor, queue, distributed |

## What Scenarios Can You Use Brighter in?

* When implementing a clean architecture (ports & adapters), one question is how to implement the interactor or port layer (sometimes called a mediator).
  * A common solution is to use the Command pattern to implement the Interactor (port) or a pattern derived from that.
  * Brighter provides an implementation the Interactor (port) using the Command Dispatcher pattern.
  * You can write a command, that is then dispatched to a handler that you write. 
  * Alternatively you can write an event, that is dispatched to zero or more handlers that you write.
  * Brighter also supports the Command Processor pattern, so that you can add middleware between the sender and handler.
  * Handlers are tagged via attributes to include middleware in thier pipeline.
  * Out-of-the-box middleware is provided for logging and Polly (retry, and circuit breaker).

* When integrating two microservices using messaging, one question is how to abstract from the developer the code that sends and receives messages in favor of writing domain code.
  * A common solution is a message pump that: gets a message, translates a message, and dispatches it to user code. 
  * Brighter provides a service activator that implements a message pump.
  * The message pump dispatches to user code via Brighter's Command Dispatcher/Processor.
  * This hides the complexity of a message pump; developers need only write a handler that subscribes to a message
  * This hides the complexity of messaging from developers who just write commands/events and handlers.
  * Developers can take full advantage of Brighter's middleware pipeline when processing messages 
  * Brighter can be configured for a variety of transports including RabbitMQ, and SNS+SQS.
  
## Documentation

* More detailed documentation on the project can be found on the GitBook pages for the project here: [Paramore](https://brightercommand.gitbook.io/paramore-brighter-documentation/)

## What are the different branches?

| Branch        | Description   |
| ------------- | ------------- |
| Master | The tip of active development. Anything in master should ship at the next release. Code here should conform to CI basics: compile, pass tests etc.  |
| Release [X] | The code for an actively supported release. Created when master needs breaking changes that are not compatible with the current release. We support one 'historical' release. |
| [Other]  | A branch for any work that is not ready to go into master (for example would break CI) or is experimental i.e. we don't know if we intend to ever ship, we are just trying out ideas. |

## Using Docker Compose to test ##

We provide a Docker Compose file to allow you to run the test suite, without having to install the pre-requisites, such as brokers or databases locally.

To run it, you will need to scale the redis sentinel to at least 3 nodes, and use at least two redis slaves. For example:

```bash
docker-compose up -d --build --scale redis-slave=2 --scale redis-sentinel=3
```

The goal is to allow you to begin working with Brighter as easily as possible for development.

Note that if you have locally installed versions of these services you will either need to stop them, or edit a local version of the docker compose file.

## How do I get the NuGet packages for the latest build?

We release the build artefacts (NuGet packages) to [NuGet](http://nuget.org) on a regular basis and we update the release notes on those drops. We also tag the master code line. If you want to take the packages that represent master at any point you can download the packages for the latest good build from [GitHub Packages](https://nuget.pkg.github.com/).

<a href="https://scan.coverity.com/projects/2900">
  <img alt="Coverity Scan Build Status"
       src="https://scan.coverity.com/projects/2900/badge.svg"/>
</a>

## Sources

Portions of this code are based on Stephen Cleary's [AsyncEx's AsyncContext](https://github.com/StephenCleary/AsyncEx/blob/master/doc/AsyncContext.md)

![CodeScene Code Health](https://codescene.io/projects/32198/status-badges/code-health)

![CodeScene System Mastery](https://codescene.io/projects/32198/status-badges/system-mastery)

![CI](https://github.com/BrighterCommand/Brighter/workflows/CI/badge.svg)

