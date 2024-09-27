using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter;

internal class MessagePosterSync<TMessage, TTransaction>(
    IAmAProducerRegistry producerRegistry,
    OutboxSync<TMessage, TTransaction> outBox,
    IAmABrighterTracer tracer,
    PolicyRegistry policyRegistry,
    InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) where TMessage : Message
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

    public void Dispatch(IEnumerable<Message> posts, RequestContext requestContext, Dictionary<string, object>? args = null)
    {
        var parentSpan = requestContext.Span;
        var producerSpans = new ConcurrentDictionary<string, Activity>();
        try
        {
            foreach (var message in posts)
                Post(message, requestContext, producerSpans, parentSpan, args); 
        }
        finally
        {
            tracer?.EndSpans(producerSpans);
        }
    }

    private void Post(
        Message message, 
        RequestContext requestContext, 
        ConcurrentDictionary<string, Activity> producerSpans, 
        Activity? parentSpan, Dictionary<string, object>? args
        )
    {
        s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, message.Id);

        var producer = producerRegistry.LookupBy(message.Header.Topic);
        if (producer is not IAmAMessageProducerSync producerSync)
            throw new InvalidOperationException("No sync message producer defined.");
        
        var span = tracer?.CreateProducerSpan(producer.Publication, message, requestContext.Span, instrumentationOptions);
        producer.Span = span;
        if (span != null) producerSpans.TryAdd(message.Id, span);
            
        if (producer is ISupportPublishConfirmation)
        {
            //mark dispatch handled by a callback - set in External Bus Service
            Retry(() => { producerSync.Send(message); }, requestContext);
        }
        else
        {
            var sent = Retry(() => { producerSync.Send(message); }, requestContext);
            if (sent) Retry(() => outBox.MarkDispatched(message.Id, requestContext, args), requestContext);
        }

        Activity.Current = parentSpan;
        producer.Span = null; 
    }

    private bool Retry(Action action, RequestContext? requestContext)
    {
        var policy = policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
        var result = policy.ExecuteAndCapture(action);
        if (result.Outcome != OutcomeType.Successful)
        {
            if (result.FinalException != null)
            {
                s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                outBox.CheckOutstandingMessages(requestContext);
            }

            return false;
        }

        return true;
    }
}
