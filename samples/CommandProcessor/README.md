# Command Processor Examples

## Architecture
### HelloWorld and HelloWorldAsync

These examples show the usage of the CommandProcessor within a single process. **HelloWorld** and **HelloWorldAsync** are examples of how to use the CommandProcessor in a synchronous and asynchronous way respectively.

We use the .NET Host for simplicity, which allows us to register our handlers and the request that triggers them, and then we send a command to the CommandProcessor to execute.

These examples demonstrate the use of the [CommandProcessor](https://www.dre.vanderbilt.edu/~schmidt/cs282/PDFs/CommandProcessor.pdf) and [Command Dispatcher](https://hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf) patterns.

You will note that Paramore enforces strict Command-Query Separation. Brighter provides a Command or Event - the Command side of CQS. Darker, the sister project provides the Query side of CQS. 

(Note: Some folks think about this as [Mediator](https://imae.udg.edu/~sellares/EINF-ES1/MediatorToni.pdf) pattern. The problem is it is not. The Mediator pattern is a behavioural pattern, whereas the Command Processor is a structural pattern. The Command Processor is a way of structuring the code to make it easier to understand and maintain. It is a way of organising the code, not a way of changing the behaviour of the code.)

### HelloWorldInternalBus

This example also works within a single process, but uses an Internal Bus to provide a buffer between the CommandProcessor and the Handlers. This provides a stricter level of separation between the CommandProcessor and the Handlers, and allows us to convert to a distributed approach using an external bus more easily at a later date.

Note that the Internal Bus does not persist messages, so there is no increased durability here. It is purely a structural change.


