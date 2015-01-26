Projects 
===
* [Brighter](http://iancooper.github.io/Paramore/Brighter.html)
  	- An implementation of the Command Dispatcher and Command Processor patterns, suitable for providing both dispatch and orthoganal concerns such as retry, circuit breaker, timeout, logging etc. 
* Rewind
	- A full example of using Brighter in a hexagonal architecture 	

 =================================
 Brighter
=================================

|               |               |
| ------------- | ------------- |
|![canon] (https://openclipart.org/people/amilo/canon.svg)|Brighter is a command dispatcher, processor, and task queue|
| Version  | 2.0.1  |
| Web  |http://iancooper.github.io/Paramore/  |
| Download  |https://www.nuget.org/packages/Paramore.Brighter.CommandProcessor/ |
| Source  |https://github.com/iancooper/Paramore |
| Keywords  |task queue, job queue, asynchronous, async, rabbitmq, amqp, c#, command, command dispatcher, command  processor, queue, distributed |

Why a Command Dispatcher, Command Processor, and Task Queue?
========
* When implementing a hexagonal architecture, one question is how to implement a port.
	- Brighter shows how to implement ports using a Command approach (with a Command Dispatcher)
* A command processor let's you add orthogonal concerns seperately to the processing of commands such as logging, undo, validation, retry, and circuit breaker
 	- Brighter provides a Command Processor, using a 'Russian Doll' model to allow a pipeline of handlers to operate on a command.
* A task queue allows a one process to send work to be handled asynchronously to another process, using a message queue as the channel, for processing. A common use case is to help a web server scale by handing off a request to another process for back-end processing. This allows both a faster ack and throttling of the request arrival rate to that which can be handled by a back end processing component. For another project with this goal, see [Celery](https://github.com/celery/celery)
 	- Brighter provides a Task Queue implementation for handling commands asynchronously via a work queue. 
* More detailed documentation on the project can be found on the GitHub pages for the project here: [Paramore](http://iancooper.github.io/Paramore/)


What are the different branches?
====
| Branch        | Description   |
| ------------- | ------------- |
| Release | The source for the current NuGet package (or the release candidate that is being verified)|
| Master | The tip of active development. Anything in master should ship at the next release. Code here should conform to CI basics: compile, pass tests etc.  |
| Other  | A branch for any work that is not ready to go into master (for example would break CI) or is experimental i.e. we don't know if we intend to ever ship, we are just trying out ideas  |

What is the current NuGet package version?
====
2.0.1

=================================
Rewind 
=================================
* An example .NET project using Brighter
* Provides an example of the following architectural styles:
 * Hierachical Systems  
   * N-Tier inc. Hexagonal Architecture (Ports and Adapters) 
   * CQRS
 * Data Centric Systems  
   * OO Domain Model (see below..  )
 * Data Flow Systems  
   * Piplines
 * Interacting Processess  
   * Broker
 * Client-Server (REST, SPA)  


<a href="https://scan.coverity.com/projects/2900">
  <img alt="Coverity Scan Build Status"
       src="https://scan.coverity.com/projects/2900/badge.svg"/>
</a>

[![Build status](https://ci.appveyor.com/api/projects/status/kuigla5ifar07r1v?svg=true)](https://ci.appveyor.com/project/IanCooper/paramore)


