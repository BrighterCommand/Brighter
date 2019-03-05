| | |
| ------------- | ------------- |
|![canon](https://raw.githubusercontent.com/BrighterCommand/Brighter/master/images/brightercanon-nuget.png) |[Brighter](https://github.com/BrighterCommand/Brighter)|
||Brighter is a command dispatcher, processor, and task queue. It can be used to implement the [Command Invoker](http://servicedesignpatterns.com/WebServiceImplementationStyles/CommandInvoker) pattern. It can be used for interoperability in a microservices architecture as well. |
| Version  | [![NuGet Version](http://img.shields.io/nuget/v/paramore.brighter.svg)](https://www.nuget.org/packages/paramore.brighter/)  |
| Download | [![NuGet Downloads](http://img.shields.io/nuget/dt/paramore.brighter.svg)](https://www.nuget.org/packages/Paramore.Brighter/) |
| Documentation  |  [Introduction](https://www.goparamore.io); [Technical Documentation](https://paramore.readthedocs.io); [Wiki](https://github.com/BrighterCommand/Brighter/wiki)  |
| Source  |https://github.com/BrighterCommand/Brighter |
| Chat | [![Join the chat at https://gitter.im/iancooper/Paramore](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/iancooper/Paramore?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge) |
| Keywords  |task queue, job queue, asynchronous, async, rabbitmq, amqp, c#, command, command dispatcher, command  processor, queue, distributed |

## Why a Command Dispatcher, Command Processor, and Task Queue?
* When implementing a hexagonal architecture, one question is how to implement a port.
	- Brighter shows how to implement ports using a Command approach (with a Command Dispatcher)
	- This is the strategy described for services in Service Design Patterns as  [Command Invoker](http://servicedesignpatterns.com/WebServiceImplementationStyles/CommandInvoker)
* A command processor lets you add orthogonal concerns seperately to the processing of commands such as logging, undo, validation, retry, and circuit breaker
 	- Brighter provides a Command Processor, using a 'Russian Doll' model to allow a pipeline of handlers to operate on a command.
* A task queue allows a one process to send work to be handled asynchronously to another process, using a message queue as the channel, for processing. A common use case is to help a web server scale by handing off a request to another process for back-end processing. This allows both a faster ack and throttling of the request arrival rate to that which can be handled by a back end processing component. For another project with this goal, see [Celery](https://github.com/celery/celery)
 	- Brighter provides a Task Queue implementation for handling commands asynchronously via a work queue. 

## Documentation
* More detailed documentation on the project can be found on the GitHub pages for the project here: [Paramore](https://github.com/BrighterCommand/Brighter)


## What are the different branches?

| Branch        | Description   |
| ------------- | ------------- |
| Master | The tip of active development. Anything in master should ship at the next release. Code here should conform to CI basics: compile, pass tests etc.  |
| [Other]  | A branch for any work that is not ready to go into master (for example would break CI) or is experimental i.e. we don't know if we intend to ever ship, we are just trying out ideas.  |

## Using Docker Compose to test ##
We provide a Docker Compose file to allow you to run the test suite, without having to install the pre-requisites, such as brokers or databases locally.

To run it, you will need to scale the redis sentinel to at least 3 nodes, and use at least two redis slaves. For example:

```
docker-compose up -d --build --scale redis-slave=2 --scale redis-sentinel=3
```

The goal is to allow you to begin working with Brighter as easily as possible for development.

Note that if you have locally installed versions of these services you will either need to stop them, or edit a local version of the docker compose file.

## How do I get the NuGet packages for the latest build?
We release the build artefacts (NuGet packages) to [NuGet](http://nuget.org) on a regular basis and we update the release notes on those drops. We also tag the master code line. If you want to take the packages that represent master at any point you can download the packages for the latest good build from [AppVeyor](https://ci.appveyor.com/nuget/paramore-brighter-m289d49fraww). The easiest approach to using those is to download them into a folder that you add to your NuGet sources. 

<a href="https://scan.coverity.com/projects/2900">
  <img alt="Coverity Scan Build Status"
       src="https://scan.coverity.com/projects/2900/badge.svg"/>
</a>

[![Build status](https://ci.appveyor.com/api/projects/status/srgw17idv5994wkm/branch/master?svg=true)](https://ci.appveyor.com/project/BrighterCommand/paramore-brighter/branch/master)
