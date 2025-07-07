# Async RMQ Support

## Purpose

With V7 the ```RMQ.Client``` dropped support for a blocking API.  This is a problem for the Brighter RMQ client as it supports both a blocking (Reactor) and non-blocking (Proactor) approach to concurrency. This package, ```Paramore Brighter.MessagingGateway.RMQ.Sync``` uses the RMQ.Client from V6 to support the older blocking API for RMQ.Client. It does not support a non-blocking API for RMQ. We will continue to support the older blocking API for RMQ.Client ntil it goes out of support. 

## Alternatives

We also support the newer non-blocking API for RMQ.Client from V7.  Use the package ```Paramore.Brighter.MessagingGateway.RMQ.Async```. 

## Mixing

Because both depend on ```RMQ.Client``` you can't mix both the blocking and non-blocking API as a dependency in the same assembly. However, it is a reasonable strategy to take a dependency on the non-blocking package when producing and the blocking package when consuming. 