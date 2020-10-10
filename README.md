| | |
| ------------- | ------------- |
|![canon](https://raw.githubusercontent.com/BrighterCommand/Brighter/master/images/brightercanon-nuget.png) |[Brighter](https://github.com/BrighterCommand/Brighter)|
||Brighter is a Command Dispatcher and Command Processor.It can be used with an in-memory bus, or for interoperability in a microservices architecture, out of process via a wider range of middleware transports. |
| Version  | [![NuGet Version](http://img.shields.io/nuget/v/paramore.brighter.svg)](https://www.nuget.org/packages/paramore.brighter/)  |
| Download | [![NuGet Downloads](http://img.shields.io/nuget/dt/paramore.brighter.svg)](https://www.nuget.org/packages/Paramore.Brighter/) |
| Documentation  |  [Introduction](https://www.goparamore.io); [Technical Documentation](https://paramore.readthedocs.io); [Wiki](https://github.com/BrighterCommand/Brighter/wiki)  |
| Source  |https://github.com/BrighterCommand/Brighter |
| Chat | [![Join the chat at https://gitter.im/iancooper/Paramore](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iancooper/Paramore?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge) |
| Keywords  |task queue, job queue, asynchronous, async, rabbitmq, amqp, c#, command, command dispatcher, command  processor, queue, distributed |

## What Scenarios Can You Use Brighter in?
* When implementing a clean architecture (ports & adapters), one question is how to implement the interactor or port layer.
        - A common solution is to use the Command pattern to implement the Interactor (port) or a pattern derived from that.
        - Brighter provides an implementation the Interactor (port) using the Command Dispatcher pattern.
        - Brighter also supports the Command Processor pattern and supports a middleware pipeline between the sender and receiver for orthogonal concerns such as logging, undo, validation, retry, and circuit breaker.
        - Brighter integrates with the Polly library and Polly policies can form part of its middleware pipeline.
* When integrating two microservices using messaging, one question is how to provide a message pump that reads messages from middleware, and calls user code to process that message
        - A common solution is a message pump that: gets a message, translates a message, and dispatches the message to user code that then handles it 
        - Brighter provides a service activator that implements a message pump
        - The message pump dispatches to user code via Brighter's Command Dispatcher/Processor
        - We hide the complexity of the pump, so that developers need only write a handler that subscribes to a message and configure a transport for their middleware, to begin recieving messages.
        - This removes the need for developers to learn how to reliably deliver messages, and focus on the domain logic.


## Documentation
* More detailed documentation on the project can be found on the GitHub pages for the project here: [Paramore](https://github.com/BrighterCommand/Brighter)


## What are the different branches?

| Branch        | Description   |
| ------------- | ------------- |
| Master | The tip of active development. Anything in master should ship at the next release. Code here should conform to CI basics: compile, pass tests etc.  |
| [Other]  | A branch for any work that is not ready to go into master (for example would break CI) or is experimental i.e. we don't know if we intend to ever ship, we are just trying out ideas. |

## Using Docker Compose to test ##
We provide a Docker Compose file to allow you to run the test suite, without having to install the pre-requisites, such as brokers or databases locally.

To run it, you will need to scale the redis sentinel to at least 3 nodes, and use at least two redis slaves. For example:

```
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

![CI](https://github.com/BrighterCommand/Brighter/workflows/CI/badge.svg)

