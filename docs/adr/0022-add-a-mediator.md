# 20. Reduce External Service Bus Complexity

Date: 2024-10-22

## Status

Proposed

## Context
We have two approaches to a workflow: orchestration and choreography. In choreography the workflow emerges from the interaction of the participants. In orchestration, one participant executes the workflow, calling other participants as needed. Whilst choreography has low-coupling, it also has low-cohesion. At scale this can lead to the Pinball anti-pattern, where it is difficult to maintain the workflow.

The [Mediator](https://www.oodesign.com/mediator-pattern) pattern provides an orchestrator that manages a workflow that involves multiple objects. In its simplest form, instead of talking to each other, objects talk to the mediator, which then calls other objects as required.
                                                                                                                                                                                                                                                                
Brighter provides `IHandleRequests<>` to provide a handler for an individual request, either a command or an event. It is possible to have an emergent workflow, within Brighter, through the interaction of these handlers.


## Decision


## Consequences