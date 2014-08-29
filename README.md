Paramore
========

Explorations in Distributed .NET Architecture

Paramore is a proving ground for approaches to distributed development in .NET. It's main purpose is to support personal exploration, learning and teaching. It is possible to harvest some parts of Paramore for re-use within applications, and we have some examples of that, however it is primarily intended to be exemplary not re-useable.  
Where we do have reusable software it follows the libraries not frameworks philosophy of assuming that you already have a preferred framework and just want to use a library to help with a specific part of the solution.  

More detailed information can be found here: [Paramore](http://iancooper.github.io/Paramore/)

Reusable Software Libraries
===
* Brighter  
  * An implementation of the Command Dispatcher and Command Processor patterns, suitable for providing both dispatch and orthoganal concerns such as retry, circuit breaker, timeout, logging etc.  
  * [Brighter](http://iancooper.github.io/Paramore/Brighter.html)

Exploration of Architectural Styles
===  
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


Exploration of Domain Modelling Techniques
===  
* Responsibility Driven Design  
* Domain Driven Design  
* Tell Don't Ask  


<a href="https://scan.coverity.com/projects/2900">
  <img alt="Coverity Scan Build Status"
       src="https://scan.coverity.com/projects/2900/badge.svg"/>
</a>
