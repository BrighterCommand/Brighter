# 9. Expose Request Context 

Date: 2024-04-28

## Status

Accepted

## Context

We have always had a RequestContext class for use with our CommandProcessor middleware pipeline. The original intent was to provide a way to pass context information between the CommandHandlers. The design goal was to allow a middleware step to create information or resources that could be used by a later step in the pipeline. We use this, for example, to pass the original exception that terminated a pipeline to the Fallback Handler.

The RequestContext is passed through the pipeline. It is created by the CommandProcessor and passed to the first middleware step.

Some users sought to try and use the RequestContext to pass information from outside of the CommandProcessor pipeline, such as authentication and authorization for the user carrying out an action, or values from the HTTPContext in which the request was running. This was difficult as the only entry point was to provide your own RequestContextFactory that supported "session" scoped RequestContexts. This was not a good fit for the use case.

A usual alternative was to put this information on a Request derived class instead. We were forced to adopt this strategy ourselves to add Telemetry information to our pipeline. This has the unforunate side effect of serializing these context fields as part of the request (although we gained from this side effect as our telemetry information included Cloud Events headers which means we were distributing some Cloud Events identifiers as a side effect).

The alternative to using a field on the Request for this, picking up global data, such as Activity.Current, suffers from all the issues of Common Coupling. We have no gurantee that a developer in implementing their own handler won't create their own activity and thus reset Activity.Current, pushing our telemetry information into the wrong scope.

With our decision in V10 to support Cloud Events and review how our OTel support works we needed a better solution than just adding more fields to the Request class.

## Decision

With V10 there is now a RequestContext parameter on the methods of the transmission methods of the CommandProcessor. If you pass null (the default) the behavior is as before, we will use the RequestContextFactory to create a refresh RequestContext and pass it down the pipeline. But if you pass in your own RequestContext, we will use that instead.

This is particularly useful to us in ServiceActivator where our Dispatcher runs your pipeline. In this case we want to originate the RequestContext in the Dispatcher, not in the CommandProcessor, as this allow us to pass information that we know in the Dispatcher to the CommandHandlers. A simple example is the Message itself, which is not currently available to the CommandProcessor and can be used for debugging and accessing metadata.

By passing the same RequestContext to the translation pipeline (which has also been enhanced in V10 to take a Publication) we can now pick up information from the RequestContext when we are working with the message. This also allows us to pass the telemetry Span which we start in the Dispatcher through our pipeline, thus avoiding common coupling to Activity.Current. This divorces us from developers implementing their handlers who choose to start their own Span (with luck they can also choose to grab our Span from the Context property of their Handler and set it as the parent for their span).  (See [ADR-0010](0010-support-otel-standards.md) for more information on how we are working with OTel to provided context in a distributed processing scenarios which this enhances).

## Consequences

The API for the our CommandProcessor has increased complexity because an new question becomes "do I need a RequestContext". By making this an optional parameter we can ease the burdern of understanding this - just fall back on the library to do something sensible if you don't provide it (in our case either create it in the Dispatcher or in the CommandProcessor depending on context).

For more advanced use cases it does prevent the need to users to overload their Request classes with context information that is not part of the request itself. This is particularly useful for cross cutting concerns such as authentication and authorization.

We also have an "args" dictionary which is uses to provide information required by Outboxes etc. In principle there is now redundancy between the RequestContext and the args dictionary. We will need to review this in the future to see if we can remove the redundancy.