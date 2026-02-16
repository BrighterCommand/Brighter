# 41. Implementing Parallel Split Step for Concurrent Workflow Execution

## Status

Proposed

## Context

Our workflow currently supports sequential steps executed in a single thread of control. Each step in the workflow proceeds one after another, and the Mediator has been designed with this single-threaded assumption.

To support more advanced control flow, we want to introduce a Parallel Split Step based on the Workflow Patterns Basic Control Flow Patterns. The Parallel Split Step is defined as “the divergence of a branch into two or more parallel branches, each of which execute concurrently.” This will enable the workflow to branch into parallel paths, executing multiple threads of control simultaneously. Each branch will operate independently of the others, continuing the workflow until either completion or a synchronization step (such as a Simple Merge) later in the process.

We would expect a some point to implement the Simple Merge step to allow parallel branches to converge back into a single thread of control. However, this ADR will focus on the Parallel Split Step implementation, with the understanding that future steps will be added to support synchronization.   

### Key Requirements
1. Parallel Execution:
  * Parallel Split Step must initiate two or more parallel branches within the workflow.
  * Each branch should proceed as a separate thread of control, executing steps independently.
2. Concurrency Handling in the Mediator:
  * The Mediator needs to manage multiple threads of execution rather than assuming a single-threaded flow. 
  * It must be able to initiate and track multiple branches for each Parallel Split Step within the workflow. 
3. State Persistence for Parallel Branches:
  *	Workflow state management and persistence will need to be adapted to track the branches of the flow. 
  * In the case of a crash, each branch should be able to resume from its last saved state.
4. Integration with Future Synchronization Steps:
  * The Parallel Split Step should integrate seamlessly with a future Simple Merge step, which will allow parallel branches to converge back into a single thread.

## Decision
1. Parallel Split Step Implementation:
  * Introduce a new class, ParallelSplitStep<TData>, derived from Step<TData>.
  * Ths class will define multiple branches by specifying two or more independent workflow sequences to be executed in parallel.
2. Producer and Consumer Model for Parallel Execution
  * The Mediator will now consist of two classes: a producer (Scheduler) and a consumer (Runner).
  * Scheduling a workflow via the Scheduler causes it to send a job to a shared channel or blocking collection.
  * The Runner class will act as a consumer, reading workflow jobs from the channel and executing them.
  * The Runner is single-threaded, and runs a message pump to process jobs sequentially.
  * The job queue is bounded to prevent excessive memory usage and ensure fair scheduling.
  * The user can configure the job scheduler for backpressure (producer stalls) or load shedding (dropping jobs).
  * The user configures the number of Runners; we don't just pull them from the thread pool. This allows users to control how many threads are used to process jobs. For example, a user could configure a single Runner for a single-threaded workflow, or multiple Runners for parallel execution.
3. In the In-Memory version the job channels will be implemented using a BlockingCollection<T> with a bounded capacity.  
    * We won't separately store workflow data in a database; the job channel is the storage for work to be done, or in flight
    * When we branch, we schedule onto the same channel; this means a Runner has a dependency on the Mediator
4. For resilience, we will need to use a persistent queue for the workflow channels.
    * We assume that workflow will become unlocked when their owning Runner crashes, allowing another runner to pick them up 
    * We will use one derived from a database, not a message queue.
    * This will be covered in a later ADR, and likely create some changes

## Consequences

### Positive Consequences
* Concurrency and Flexibility: The addition of Parallel Split allows workflows to handle concurrent tasks and enables more complex control flows.
* Scalability: Running parallel branches improves throughput, as tasks that are independent of each other can execute simultaneously. 
* Adaptability for Future Steps: Implementing parallel branching prepares the workflow for synchronization steps (e.g., Simple Merge), allowing flexible convergence of parallel tasks.
* Resilience:

### Negative Consequences
* Increased Complexity in State Management: Tracking multiple branches requires more complex state management to ensure each branch persists and resumes accurately. 
* Concurrency Overhead in the Mediator: Managing multiple threads of control adds overhead. We now have both a Runner and a Scheduler. 

### Use of Middleware or Db for The Job Channel
* We could use a middleware library to manage the job channel. Brighter itself manages a queue or stream of work with a single-threaded pump
* This would mean the scheduler uses the commandprocessor to deposit a job on a queue, and the runner would be our existing message pump, which would pass to a job handler that executed the workflow.
* The alternative here is to use the database as the job channel. This would mean that the scheduler would write a job to the database, and the runner would read from the database. 
* For now, we defer this decision to a later ADR. First we want to understand the whole scope of the work, through an in-memory implementation, then we will determine what an out-of-process implementation would look like.

### Merge of parallel branches 
* Future ADR for implementing Simple Merge Step for synchronization of parallel branches.
