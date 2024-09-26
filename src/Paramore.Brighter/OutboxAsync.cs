using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter;

internal class OutboxAsync<TMessage, TTransaction>(
    IAmAnExternalBusService<TMessage, TTransaction> bus,
    IAmAnOutboxAsync<TMessage, TTransaction> outBox, 
    Dictionary<string, object> outBoxBag,
    TimeProvider timeProvider, 
    TimeSpan maxOutStandingCheckInterval, 
    DateTimeOffset lastOutStandingMessageCheckAt
    ) where TMessage : Message
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();
    
    //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static
    //bool should be made thread-safe by locking the object
    private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new(1, 1);


    public void CheckOutstandingMessages(RequestContext? requestContext)
    {
        var now = timeProvider.GetUtcNow();

        var timeSinceLastCheck = now - lastOutStandingMessageCheckAt;

        s_logger.LogDebug(
            "Time since last check is {SecondsSinceLastCheck} seconds",
            timeSinceLastCheck.TotalSeconds
        );

        if (timeSinceLastCheck < maxOutStandingCheckInterval)
        {
            s_logger.LogDebug($"Check not ready to run yet");
            return;
        }                                                    

        s_logger.LogDebug(
            "Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait",
            now, timeSinceLastCheck.TotalSeconds
        );
        //This is expensive, so use a background thread
        Task.Run(
            () => OutstandingMessagesCheck(requestContext)
        );
    }   
    
    private void OutstandingMessagesCheck(RequestContext? requestContext)
    {
        //REFACTORING: THIS IS FOR THESE ACROSS THE WHOLE BUS
        s_checkOutstandingSemaphoreToken.Wait();

        lastOutStandingMessageCheckAt = timeProvider.GetUtcNow();
        s_logger.LogDebug("Begin count of outstanding messages");
        try
        {
                //REFACTORING: THIS NEEDS TO UPDATE THE OUTSTANDING COUNT IN THE EXTERNAL SERVICE BUS? PROPERTY AND REFERENCE TO PARENT?
                //An Async version of the the count does not exist??

                bus.OutStandingCount =
                    await outBox.OutstandingMessagesAsync(maxOutStandingCheckInterval, requestContext, args: outBoxBag);
        }
        catch (Exception ex)
        {
            //if we can't talk to the outbox, we would swallow the exception on this thread
            //by setting the _outstandingCount to -1, we force an exception
            s_logger.LogError(ex, "Error getting outstanding message count, reset count");
            bus.OutStandingCount = 0;
        }
        finally
        {
            s_logger.LogDebug("Current outstanding count is {OutStandingCount}", bus.OutStandingCount);
            s_checkOutstandingSemaphoreToken.Release();
        }
    }

    public async Task MarkDispatchedAsync(string messageId, RequestContext requestContext, CancellationToken cancellationToken)
    {
        await outBox.MarkDispatchedAsync(messageId, requestContext, timeProvider.GetUtcNow(), cancellationToken: cancellationToken);
    }
}
