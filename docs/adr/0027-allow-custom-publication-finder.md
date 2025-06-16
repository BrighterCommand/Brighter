# 27. Allow custom publication finder 

Date: 2025-05-30

## Status

Accepted

## Context

Today, we search for publications solely by request type. This approach works well when a single request type maps to 
a single publication. However, in more complex scenarios, one request type might need to publish to different publications, 
with the specific publication being determined dynamically. This dynamic definition could be based on factors like a 
tenant ID in the HTTP request/message handler, or any other runtime context.

## Decision

We will create a new interface called `IAmAPublicationFinder`. This interface will be responsible for finding the 
appropriate `Publication` given an `IAmAProducerRegistry` (which contains all configured publications) and the 
current `RequestContext`.

### Add Topic Property to RequestContext
A new `Topic` property will be added to the `RequestContext` class. This property will allow users to explicitly set 
the desired publication topic dynamically before dispatching a message, influencing the publication lookup process.

### Add PublicationTopicAttribute
A new attribute called PublicationTopicAttribute will be introduced. This attribute will allow users to directly set 
the publication topic on the request type itself. This enables scenarios where multiple request types can be routed 
to the same publication.

```csharp
[PublicationTopic("greeting.addGreetingCommand")]
public class AddGreetingCommand : Command
{
    public AddGreetingCommand() : base(Guid.NewGuid()) { }

    public string GreetingMessage { get; set; } = "Hello Paul.";

    public bool ThrowError { get; set; } = false;
}
```

### Default implement
The default implementation of `IAmAPublicationFinder` will attempt to find a publication using the following priority:

1. `RequestContext.Topic`: If `RequestContext.Topic` is not null (e.g., set by the user before dispatch, or by a 
pipeline handler), Brighter will search for a publication whose configured topic matches this value.
2. `PublicationTopicAttribute`: If the request message has a PublicationTopicAttribute, Brighter will search for a 
publication whose configured topic matches the attribute's value.
3. Request Type: If neither of the above methods yields a publication, Brighter will search for a publication associated 
with the request type itself.

This approach ensures backward compatibility with current Brighter code while providing new flexibility for users to 
override the default behavior.

#### Custom implementation 
For custom implementations, we recommend inheriting from `FindPublicationByPublicationTopicOrRequestType` to leverage 
the default lookup logic.

```c#
namespace SomeApp;

serviceCollection
    .AddBrighter()
    .UseExternalBus((configure) =>
    {
        ...
    })
    .UsePublicationFinder<CustomPublicationFinder>()
        ...
    ;

class CustomPublicationFinder : FindPublicationByPublicationTopicOrRequestType
{
     private static readonly Dictionary<Type, string> s_typeRouteMapper = new()
     {
         [typeof(GreetingEvent)] = "greeting.event",
         [typeof(FarewellEvent)] = "farewell.event"
     };

     public override Publication Find<TRequest>(IAmAProducerRegistry registry, RequestContext context)
     {
         if (s_typeRouteMapper.TryGetValue(typeof(TRequest), out var topic))
         {
             // If you have a tenant topic you could have this 
             // MAP: [typeof(A), "some-topic-{tenant}"
             // topic.Replace("{tenant}", tenantContext.Tenant)
             return registry.LookupBy(topic).Publication;
         }
         
         return base.Find<TRequest>(registry, context);
     }
}
```

