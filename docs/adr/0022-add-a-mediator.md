# 22. Add a Mediator to Brighter 

Date: 2024-10-22

## Status

Proposed

## Context
We have two approaches to a workflow: orchestration and choreography. In choreography the workflow emerges from the interaction of the participants. In orchestration, one participant executes the workflow, calling other participants as needed. Whilst choreography has low-coupling, it also has low-cohesion. At scale this can lead to the Pinball anti-pattern, where it is difficult to maintain the workflow.

The [Mediator](https://www.oodesign.com/mediator-pattern) pattern provides an orchestrator that manages a workflow that involves multiple objects. In its simplest form, instead of talking to each other, objects talk to the mediator, which then calls other objects as required to execute the workflow.
                                                                                                                                                                                                                                                                
Brighter provides `IHandleRequests<>` to provide a handler for an individual request, either a command or an event. It is possible to have an emergent workflow, within Brighter, through the choreography of these handlers. However, Brighter provides no model for an orchestrator that manages a workflow that involves multiple handlers. In particular, Brighter does not support a class that can listen to multiple requests and then call other handlers as required to execute the workflow.

In principle, nothing stops an end user from implementing a `Mediator` class that listens to multiple requests and then calls other handlers as required to execute the workflow.  So orchestration has always been viable, but left as an exercise to the user. However, competing OSS projects provide popular workflow functionality, suggesting there is demand for an off-the-shelf solution.

Other dotnet messaging platforms erroneously conflate the Saga and Mediator patterns. A Saga is a long-running transaction that spans multiple services. A Mediator is an orchestrator that manages a workflow that involves multiple objects. One aspect of those implementations is typically the ability to store workflow state. 

A particular reference for the requirements for this work is [AWS step functions](https://states-language.net/spec.html). AWS Step functions provide a state machine that mediates calls to AWS Lambda functions. When thinking about Brighter's `IHandleRequests` it is attractive to compare them to Lambda functions in the Step functions model :

  1.  The AWS Step funcions state machine does not hold the business logic, that is located in the functions called; the Step function handles calling the Lambda functions and state transitions (as well as error paths)
  2.  We want to use the Mediator to orchestrate both internal bus and external bus hosted workflows. Step functions provide a useful model of requirements for the latter.

This approach is intended to enable flexible, event-driven workflows that can handle various business processes and requirements, including asynchronous event handling and conditional branching.

We are also influenced by the [Arazzo Specification](https://github.com/OAI/Arazzo-Specification/blob/main/versions/1.0.0.md) for defining workflows from AsyncAPI.


## Decision

We will add a `Mediator` class to Brighter that will: 

	1.	Manages and tracks a WorkflowState object representing the current step in the workflow.
	2.	Supports multiple process states, including:
	•	StartState: Initiates the workflow.
	•	FireAndForgetProcessState: Dispatches a `Command` and immediately advances to the next state.
	•	RequestReactionProcessState: Dispatches a `Command` and waits for an event response before advancing.
	•	ChoiceProcessState: Evaluates conditions using the `Specification` Pattern and chooses the next `Command` to Dispatch based on the evaluation.
	•	WaitState: Suspends execution for a specified TimeSpan before advancing.
	3.	Uses a CommandProcessor for routing commands and events to appropriate handlers.
	4.	Can be passed events, and uses the correlation IDs to match events to specific workflow instances and advance the workflow accordingly.

The Specification Pattern in ChoiceProcessState allows flexible conditional logic by combining specifications with And and Or conditions, enabling complex branching decisions within the workflow.

We assume that the initial V10 of Brighter will contain a minimum viable product version of the `Mediator`. Additional functionality, such as process states, UIs for workflows will be a feature of later releases.

## Consequences

Positive Consequences

	1.	Simplicity: Providing orchestration for a workflow, which is easier to understand 
	2.	Modularity: It is possible to extend the `Mediator' relativey easy by adding new process states.

Negative Consequences

    1.  Increased Brighter scope: Previously we had assumed that developers would use an off-the-shelf workflow solution like [Stateless](https://github.com/nblumhardt/stateless) or [Workflow Core]. The decision to provide our own workflow, to orchestrate via CommandProcessor means that we increase our scope to include the complexity of workflow management.