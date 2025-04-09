# 26. Use Source Generated Logging

Date: 2025-04-08

## Status

Accepted

## Context

We want to improve the performance of Brighter. Whilst this is a difficult and long process, there is some low-hanging fruit, one of which is moving to source generated logging "it offers superior performance and stronger typing" [as documented by Microsoft](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-library-authors).

## Decision

### Logging Pattern
- Source generated logging is always used in the `src` folder
- Any class needing to write logs will have a private static class `Log` as a direct child to colocate logging methods attributed with `[LoggerMessage]` with where they're used
- EventId is omitted - at least for now, as it's not specified in existing logs

### Bulk Migration

#### Approach
- Limit the scope of the initial migration to perform a direct 1 to 1 mapping from string templated logging to source generated logging:
- Leverage LLMs to do the heavy lifting
- Manually touch up any gaps left by the LLM

#### Rules
- Log levels MUST remain the same
- String templates SHOULD remain the same unless change is required or super easy:
	- Replace all indexed template vars with named vars [SYSLIB1014](https://learn.microsoft.com/en-gb/dotnet/fundamentals/syslib-diagnostics/syslib1014)
	- Remove exception template vars [SYSLIB1013](https://learn.microsoft.com/en-gb/dotnet/fundamentals/syslib-diagnostics/syslib1013)
	- Convert camelCase template vars to PascalCase

#### Example
**String Templated Logging** (before)
```c#
namespace Brighter;

class SomeClass
{
    static readonly ILogger s_logger = ...;
  
    void Hello(name)
    {
        s_logger.LogInformation("Hello {Name}", name);
    }
}
```

**Source Generated Logging** (after)
```c#
namespace Brighter;

partial class SomeClass
{
    static readonly ILogger s_logger = ...;
  
    void Hello(name)
    {
        Log.Hello(s_logger, name);
    }
  
    static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "Hello {Name}")]
        public static partial void Hello(ILogger logger, string name)
    }
}
```

### LLM Supported Review

On top of manual review, LLMs are useful at review stage too. Feeding file scoped diffs to the LLM gives the smallest most concise text (so that it can make the least mistakes) alongside the conversion instructions. For example, the LLM can be instructed to pair each deleted string templated log to the new source generated log and find any issues. Here is an example review where the LLM was asked to highlight any non-exact string template mappings:

```
=== Review for src/Paramore.Brighter/CommandProcessor.cs ===

Issues found:
- Mismatch in template for finding async pipelines. The original template used positional arguments and referred to 'event', while the new template uses named arguments and refers to 'command'.
    - Original: `"Found {0} async pipelines for event: {EventType} {Id}"`
    - New: `"Found {HandlerCount} async pipelines for command: {Type} {Id}"`
- Mismatch in whitespace for sending request with routingkey. The original template had an extra space after "request".
    - Original: `"Sending request  with routingkey {ChannelName}"`
    - New: `"Sending request with routingkey {ChannelName}"`

=== Review for src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs ===

Issues found:
- "Extra space removed in preparing to send message template"
    - Original: `"RmqMessageProducer: Preparing  to send message via exchange {ExchangeName}"`
    - New: `"RmqMessageProducer: Preparing to send message via exchange {ExchangeName}"`
```
