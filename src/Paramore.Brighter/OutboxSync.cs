using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter;

public class OutboxSync<TMessage, TTransaction> where TMessage : Message
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();
    private readonly Dictionary<string, List<TMessage>> _outboxBatches = new();
    private readonly IAmAnExternalBusService<TMessage, TTransaction> _bus;
    private readonly IAmAnOutboxSync<TMessage, TTransaction> _outBox;
    private readonly IAmAnArchiveProvider _archiveProvider;
    private readonly MessagePosterSync<TMessage, TTransaction> _poster;
    private readonly Dictionary<string, object>? _outBoxBag;
    private readonly IPolicyRegistry<string> _policyRegistry;
    private readonly int _maxOutStandingMessages;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maxOutStandingCheckInterval;
    private DateTimeOffset _lastOutStandingMessageCheckAt;
    private readonly int _archiveBatchSize;
    private readonly IAmABrighterTracer _tracer;
    private readonly InstrumentationOptions _instrumentationOptions;
    private readonly TimeSpan _outboxTimeout;
    
    private const string NoSyncOutboxError = "A sync Outbox must be defined.";
    private const string NoPolicyRegistry = "Missing Policy Registry for External Bus Services";
    private const string NoArchiveProviderError = "An Archive Provider must be defined.";

    public OutboxSync(
        IAmAnExternalBusService<TMessage, TTransaction> bus,
        IAmAnOutboxSync<TMessage, TTransaction> outBox, 
        IAmAnArchiveProvider archiveProvider,
        Dictionary<string, object>? outBoxBag,
        IPolicyRegistry<string> policyRegistry,
        int maxOutStandingMessages,
        TimeSpan maxOutStandingCheckInterval, 
        DateTimeOffset lastOutStandingMessageCheckAt,
        TimeSpan? outboxTimeout,
        int archiveBatchSize,
        IAmABrighterTracer tracer,
        TimeProvider timeProvider, 
        InstrumentationOptions instrumentationOptions)
    {
        _bus = bus;
        _outBox = outBox;
        _archiveProvider = archiveProvider;
        _outBoxBag = outBoxBag;
        _policyRegistry = policyRegistry;
        _maxOutStandingMessages = maxOutStandingMessages;
        _timeProvider = timeProvider;
        _maxOutStandingCheckInterval = maxOutStandingCheckInterval;
        _lastOutStandingMessageCheckAt = lastOutStandingMessageCheckAt;
        _archiveBatchSize = archiveBatchSize;
        _tracer = tracer;
        _instrumentationOptions = instrumentationOptions;
        
        outboxTimeout ??= TimeSpan.FromMilliseconds(300);
        _outboxTimeout = outboxTimeout.Value;
    }
   
    /// <summary>
    /// Adds a message to the outbox
    /// </summary>
    /// <param name="message">The message we intend to send</param>
    /// <param name="overridingTransactionProvider">A transaction provider that gives us the transaction to use with the Outbox</param>
    /// <param name="requestContext">The context of the request pipeline</param>
    /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
    /// <exception cref="ChannelFailureException">Thrown if we fail to write all the messages</exception>
    public void AddToOutbox(
        TMessage message,
        RequestContext requestContext,
        IAmABoxTransactionProvider<TTransaction>? overridingTransactionProvider = null,
        string? batchId = null
    )
    {
        if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
        if (batchId != null)
        {
            _outboxBatches[batchId].Add(message);
            return;
        }

        CheckOutboxOutstandingLimit();

        BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Add, message, requestContext.Span,
            overridingTransactionProvider != null, false, _instrumentationOptions);

        var written = Retry(() =>
            {
                _outBox.Add(message, requestContext, _outboxTimeout, overridingTransactionProvider);
            },
            requestContext
        );

        if (!written)
            throw new ChannelFailureException($"Could not write message {message.Id} to the outbox");
    }
    
    /// <summary>
    /// Archive Message from the outbox to the outbox archive provider
    /// Throws any archiving exception
    /// </summary>
    /// <param name="dispatchedSince">Minimum age</param>
    /// <param name="requestContext">The request context for the pipeline</param>
    public void Archive(TimeSpan dispatchedSince, RequestContext requestContext)
    {
        try
        {
            if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
            if (_archiveProvider is null) throw new ArgumentException(NoArchiveProviderError);
            var messages = _outBox
                .DispatchedMessages(dispatchedSince, requestContext, _archiveBatchSize)
                .ToArray();

            s_logger.LogInformation(
                "Found {NumberOfMessageArchived} message to archive, batch size : {BatchSize}",
                messages.Count(), _archiveBatchSize
            );

            if (messages.Length <= 0) return;

            foreach (var message in messages)
            {
                _archiveProvider.ArchiveMessage(message);
            }

            _outBox.Delete(messages.Select(e => e.Id).ToArray(), requestContext);

            s_logger.LogInformation(
                "Successfully archived {NumberOfMessageArchived}, batch size : {BatchSize}",
                messages.Count(),
                _archiveBatchSize
            );
        }
        catch (Exception e)
        {
            s_logger.LogError(e, "Error while archiving from the outbox");
            throw;
        }
    }


    public void CheckOutstandingMessages(RequestContext? requestContext)
    {
        var now = _timeProvider.GetUtcNow();

        var timeSinceLastCheck = now - _lastOutStandingMessageCheckAt;

        s_logger.LogDebug(
            "Time since last check is {SecondsSinceLastCheck} seconds",
            timeSinceLastCheck.TotalSeconds
        );

        if (timeSinceLastCheck < _maxOutStandingCheckInterval)
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
    
    private void CheckOutboxOutstandingLimit()
    {
        s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _bus.OutStandingCount);
        // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
        bool exceedsOutstandingMessageLimit = _maxOutStandingMessages != -1 && _bus.OutStandingCount > _maxOutStandingMessages;

        if (exceedsOutstandingMessageLimit)
            throw new OutboxLimitReachedException(
                $"The outbox limit of {_maxOutStandingMessages} has been exceeded");
    }
    
    public void ClearOutbox(
        string[] posts,
        RequestContext requestContext,
        Dictionary<string, object>? args = null
    )
    {
        // Only allow a single Clear to happen at a time
        _bus.LockClear();
        var parentSpan = requestContext.Span;

        var childSpans = new ConcurrentDictionary<string, Activity>();
        try
        {
            if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
            foreach (var messageId in posts)
            {
                var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span,
                    messageId, _instrumentationOptions);
                if (span is not null)
                {
                    childSpans.TryAdd(messageId, span);
                    requestContext.Span = span;
                }
                    
                var message = _outBox.Get(messageId, requestContext);
                if (message is null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Get, message, span, false, false,
                    _instrumentationOptions);

                _poster.Dispatch(new[] { message }, requestContext, args);
            }
        }
        finally
        {
            _tracer?.EndSpans(childSpans);
            requestContext.Span = parentSpan;
            _bus.LockClear();
        }

        CheckOutstandingMessages(requestContext);
    }
    
    private void OutstandingMessagesCheck(RequestContext? requestContext)
    {
        //REFACTORING: THIS IS FOR THESE ACROSS THE WHOLE BUS
        _bus.LockCheckOutStanding();

        _lastOutStandingMessageCheckAt = _timeProvider.GetUtcNow();
        s_logger.LogDebug("Begin count of outstanding messages");
        try
        {
                //REFACTORING: THIS NEEDS TO UPDATE THE OUTSTANDING COUNT IN THE EXTERNAL SERVICE BUS? PROPERTY AND REFERENCE TO PARENT?
            
                _bus.OutStandingCount = _outBox
                    .OutstandingMessages(
                        _maxOutStandingCheckInterval,
                        requestContext,
                        args: _outBoxBag
                    )
                    .Count();
        }
        catch (Exception ex)
        {
            //if we can't talk to the outbox, we would swallow the exception on this thread
            //by setting the _outstandingCount to -1, we force an exception
            s_logger.LogError(ex, "Error getting outstanding message count, reset count");
            _bus.OutStandingCount = 0;
        }
        finally
        {
            s_logger.LogDebug("Current outstanding count is {OutStandingCount}", _bus.OutStandingCount);
           _bus.ReleaseCheckOutstanding(); 
        }
    }

    public void MarkDispatched(string messageId, RequestContext requestContext, Dictionary<string,object>? args)
    {
        _outBox.MarkDispatched(messageId, requestContext, _timeProvider.GetUtcNow(), args);
    }
    
    private bool Retry(Action action, RequestContext? requestContext)
    {
        if (_policyRegistry is null) throw new ConfigurationException(NoPolicyRegistry);
            
        var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
        var result = policy.ExecuteAndCapture(action);
        if (result.Outcome != OutcomeType.Successful)
        {
            if (result.FinalException != null)
            {
                s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                CheckOutstandingMessages(requestContext);
            }

            return false;
        }

        return true;
    }

}
