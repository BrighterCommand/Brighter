# Async RMQ Support

## Purpose

With V7 the ```RMQ.Client``` dropped support for a blocking API.  This is a problem for the Brighter RMQ client as it supports both a blocking (Reactor) and non-blocking (Proactor) approach to concurrency. This package, ```Paramore Brighter.MessagingGateway.RMQ.Async``` uses the async API for the RMQ client. It natively supports non-blocking. It supports blocking by using ```BrighterAsyncContext``` to block over async.

## Alternatives

We also support the older blocking API for RMQ.Client from V6. We will do so until it drops out of support. Use the package ```Paramore.Brighter.MessagingGateway.RMQ.Sync```. This avoids using a synchronization context to block async calls, and so may be more reliable at scale.

## Mixing

Because both depend on ```RMQ.Client``` you can't mix both the blocking and non-blocking API as a dependency in the same assembly. However, it is a reasonable strategy to take a dependency on the non-blocking package when producing and the blocking package when consuming. 