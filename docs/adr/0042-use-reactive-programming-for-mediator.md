# 42. Use Reactive Programming For Mediator

Date: 2025-01-13

## Status

Accepted

## Context

We have scenarios in any workflow where we need to split and then later merge. Our decision to handle the split in 
[0041](./0041-add-parallel-split-to-mediator.md) led us on the path to seperating a scheduler and a runner - a 
classic producer and consumer pattern. We can use `Channels` (or a `BlockingCollection`) in dotnet to support the 
implementation of an internal producer-consumer (as opposed to one using messaging.

Our approach to resolve split was simply to have one channel for the workflow to be scheduled on, so that we could 
schedule the splits back to the channel. We don't have a solution for merging those splits. 

We also have an approach to waiting for an external event, that we halt the flow, save it's state, and then reschedule 
once we are notified of the event we are waiting for. This works well for a single event, but external. It works 
less well for multiple events, or internal events, that go best over a channel.

## Decision

We will move to a Flow Based Programming approach to implementing the work. Each `Step<>` in the workflow will 
derive from a new type `Component`.

As a FBP component it has an `In` port, an instance of `IAmAJobChannel`. When a component is activated it runs a 
message pump to read work from the `In` port, until the port is marked as completed. Once there is no more work, the 
`Component` deactivates. A component should save state before it deactivates, to indicate that it was completed.

An `Out` port is actually a call to the next component. Putting work on the `Out`port activates the next component 
and puts work on its `In` port.

```
--> [In][Component][Out] -->
```

On a split, there is an array of `Out` ports to write to, instead of a single port. Generically then we require an 
overload of any Out method call on the base 'Component' that takes an array of `IAmAJobChannel`

```
--> [In][Component][Out...] -->
```

On a merge that is an array of 'In' ports to write to, instead of a single port. We may force you to wait for 
everything to arrive before continuing, or allow you to proceed as soon as you arrive in the joined flow.

We may choose to use the FBP brackets approach to any merge. The upstream sends an 'opening bracket' to 'In' 
indicating a sequence follows. The 'bracket' indicates whether we are 'WaitAll' or 'WaitAny' and the channels to 
listen on. The downstream component then listens to those channels, until they complete, and obeys the 
'WaitAll' or 'WaitAny' as appropriate.

For configuration of a downstream a component needs an `Opt` channel which can take generic configuration information 
(most likely the payload here is a `Configuration` class with an `object` payload).

## Consequences

FBP is stongly aligned with workflows, so adopting concepts from FBP gives us a strong programming model to work with. 
FBP has already solved many of the problems around running workflows, so it gives us a strong plan to work with.

