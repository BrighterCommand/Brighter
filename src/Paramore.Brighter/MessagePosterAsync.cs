using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter;

internal class MessagePosterAsync<TMessage, TTransaction>(
    IAmAProducerRegistry producerRegistry,
    OutboxAsync<TMessage, TTransaction> outBox,
    IAmABrighterTracer tracer,
    PolicyRegistry policyRegistry,
    InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) where TMessage : Message
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

       private async Task DispatchAsync(
            IEnumerable<Message> posts,
            RequestContext requestContext,
            bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new ConcurrentDictionary<string, Activity>();

            try
            {
                foreach (var message in posts)
                {
                    s_logger.LogInformation(
                        "Decoupled invocation of message: Topic:{Topic} Id:{Id}",
                        message.Header.Topic, message.Id
                    );

                    var producer = producerRegistry.LookupBy(message.Header.Topic);
                    var span = tracer?.CreateProducerSpan(producer.Publication, message, parentSpan, instrumentationOptions);
                    producer.Span = span;
                    if (span != null) producerSpans.TryAdd(message.Id, span);

                    if (producer is IAmAMessageProducerAsync producerAsync)
                    {
                        if (producer is ISupportPublishConfirmation)
                        {
                            //mark dispatch handled by a callback - set in constructor
                            await RetryAsync(
                                    async _ => await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                    requestContext, 
                                    continueOnCapturedContext,
                                    cancellationToken)
                                .ConfigureAwait(continueOnCapturedContext);
                        }
                        else
                        {
                            var sent = await RetryAsync(
                                    async _ => await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                    requestContext,
                                    continueOnCapturedContext,
                                    cancellationToken
                                )
                                .ConfigureAwait(continueOnCapturedContext
                                );

                            if (sent)
                                await RetryAsync(
                                    async _ => await outBox.MarkDispatchedAsync(message.Id, requestContext, cancellationToken),
                                    requestContext,
                                    cancellationToken: cancellationToken
                                );
                        }
                    }
                    else
                        throw new InvalidOperationException("No async message producer defined.");
                }
            }
            finally
            {
                tracer?.EndSpans(producerSpans);
                requestContext.Span = parentSpan;
            }
        }

        private async Task<bool> RetryAsync(
            Func<CancellationToken, Task> send,
            RequestContext? requestContext,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            var result = await policyRegistry.Get<AsyncPolicy>(CommandProcessor.RETRYPOLICYASYNC)
                .ExecuteAndCaptureAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);

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
