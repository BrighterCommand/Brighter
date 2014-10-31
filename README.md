=================================
 Brighter - Command Dispatcher and Processor
=================================
| ------------- | ------------- |
| Version  | 2.0.1  |
| Web  |http://iancooper.github.io/Paramore/  |
| Download  |https://www.nuget.org/packages/Paramore.Brighter.CommandProcessor/ |
| Source  |https://github.com/iancooper/Paramore |
| Keywords  |task queue, job queue, asynchronous, async, rabbitmq, amqp, c#, command, command dispatcher,
  command processor, queue, distributed |

Paramore
========
* When implementing a hexagaonal architecture, one question is how to implement a port.
 * Paramore shows how to implement ports using a Command approach (with a Command Dispatcher & Processor called Brighter).
* Brighter also provides a Task Queue implementation for handling those commands asynchronously. In this role it has design goals similar to [Celery](https://github.com/celery/celery)
* More detailed documentation on the project can be found on the GitHub pages for the project here: [Paramore](http://iancooper.github.io/Paramore/)

Brighter 
===
* Brighter  
  * An implementation of the Command Dispatcher and Command Processor patterns, suitable for providing both dispatch and orthoganal concerns such as retry, circuit breaker, timeout, logging etc.  
  * [Brighter](http://iancooper.github.io/Paramore/Brighter.html)

Rewind 
===  
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
