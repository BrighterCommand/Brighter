#region Licence
/* The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.Handlers
{
    /// <summary>
    /// Used with the Event Sourcing pattern that stores the commands that we send to handlers for replay. 
    /// http://martinfowler.com/eaaDev/EventSourcing.html
    /// Note that without a mechanism to prevent raising events from commands the danger of replay is that if events are raised downstream that are not idempotent then replay can have undesired effects.
    /// A mitigation is not to record inputs, only changes of state to the model and replay those. Of course it is possible that publishing events is desirable.
    /// To distinguish the variants the approach is now properly Event Sourcing (because it captures events that occur because of the Command) and the Fowler
    /// approach is typically called Command Sourcing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public partial class UseInboxHandlerAsync<T> : RequestHandlerAsync<T> where T : class, IRequest
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<UseInboxHandlerAsync<T>>();

        // Set once, process-wide, the first time a custom IRequestContext disables Replay, to keep the warning
        // out of the hot path. A benign race may let it log a couple of extra times under concurrent first-hits.
        private static bool s_warnedAboutCustomContext;

        private readonly IAmAnInboxAsync _inbox;
        private readonly IAmACausationTrackingOutbox? _outbox;
        private bool _onceOnly;
        private string? _contextKey;
        private OnceOnlyAction _onceOnlyAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="UseInboxHandlerAsync{T}" /> class.
        /// </summary>
        /// <param name="inbox">The store for commands that pass into the system</param>
        /// <param name="outbox">An optional causation-tracking outbox, used to replay messages when a duplicate is
        /// seen and <see cref="OnceOnlyAction.Replay"/> is configured. Resolved from DI when registered.</param>
        public UseInboxHandlerAsync(IAmAnInboxAsync inbox, IAmACausationTrackingOutbox? outbox = null)
        {
            _inbox = inbox;
            _outbox = outbox;
        }
        
        
        public override void InitializeFromAttributeParams(params object?[] initializerList)
        {
            _onceOnly = (bool?) initializerList[0] ?? false;
            _contextKey = (string?)initializerList[1];
            _onceOnlyAction = (OnceOnlyAction?)initializerList[2] ?? OnceOnlyAction.Throw;

            base.InitializeFromAttributeParams(initializerList);
        }

        /// <summary>
        /// Awaitably logs the command we received to the inbox.
        /// </summary>
        /// <param name="command">The command that we want to store.</param>
        /// <param name="cancellationToken">Allows the caller to cancel the pipeline if desired</param>
        /// <returns>The parameter to allow request handlers to be chained together in a pipeline</returns>
        public override async Task<T> HandleAsync(T command, CancellationToken cancellationToken = default)
        {
            if (_contextKey is null)
                throw new ArgumentException("ContextKey must be set before Handling");

            // Prefer the shared pipeline context so the causation id we stamp below flows to the outbox Add.
            // A custom IAmARequestContextFactory may supply a context that is not a RequestContext; in that
            // case fall back to a fresh context carrying Activity.Current — matching the pre-feature behaviour
            // for the Throw/Warn/Add inbox paths (causation tracking then degrades to a no-op for Replay).
            var requestContext = Context as RequestContext;
            if (requestContext is null)
            {
                // Silent replay degradation is a PITA to diagnose, so warn (once) when a custom context means
                // Replay cannot flow the causation id to the outbox and will therefore be a no-op.
                if (_onceOnlyAction is OnceOnlyAction.Replay && !s_warnedAboutCustomContext)
                {
                    s_warnedAboutCustomContext = true;
                    Log.CustomContextDisablesReplay(s_logger);
                }

                requestContext = new RequestContext { Span = Activity.Current };
            }

            if (!requestContext.Bag.ContainsKey(RequestContextBagNames.CausationId))
                requestContext.Bag[RequestContextBagNames.CausationId] = command.Id.Value;

            // Capture the span once, before the first await. RequestContext.Span is thread-affine (it keys on the
            // current managed thread id), so with ContinueOnCapturedContext == false the continuation typically
            // resumes on a different thread-pool thread where Context?.Span would return null and the telemetry
            // events would be silently dropped. Reuse this captured value on every path (Throw/Warn/Replay/Add).
            var span = Context?.Span;

            if (_onceOnly)
            {
                Log.CheckingIfCommandHasBeenSeen(s_logger, command.Id.Value);
                //TODO: We should not use an infinite timeout here - how to configure
                var exists =
                    await _inbox.ExistsAsync<T>(command.Id.Value, _contextKey, requestContext, -1, cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

                if (exists && _onceOnlyAction is OnceOnlyAction.Throw)
                {
                    Log.CommandHasBeenSeen(s_logger, command.Id.Value);
                    WriteInboxEvent(span, command, "UseInboxHandler Duplicate Throw");
                    throw new OnceOnlyException($"A command with id {command.Id} has already been handled");
                }

                if (exists && _onceOnlyAction is OnceOnlyAction.Warn)
                {
                    Log.CommandHasBeenSeenWarning(s_logger, command.Id.Value);
                    WriteInboxEvent(span, command, "UseInboxHandler Duplicate Warn");
                    return command;
                }

                if (exists && _onceOnlyAction is OnceOnlyAction.Replay)
                {
                    Log.CommandHasBeenSeenReplayingOutbox(s_logger, command.Id.Value);

                    string? causationId = null;
                    if (_inbox is IAmACausationTrackingInbox trackingInbox && _outbox is not null)
                    {
                        causationId = await trackingInbox
                            .GetCausationIdAsync(command.Id.Value, _contextKey, requestContext, -1, cancellationToken)
                            .ConfigureAwait(ContinueOnCapturedContext);
                        if (causationId is not null)
                            await _outbox.ReplayCausationAsync(causationId, requestContext, cancellationToken: cancellationToken)
                                .ConfigureAwait(ContinueOnCapturedContext);
                    }

                    WriteReplayEvent(span, command, causationId);

                    return command;
                }
            }

            Log.WritingCommandToInbox(s_logger, command.Id.Value);

            T handledCommand = await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            //TODO: We should not use an infinite timeout here - how to configure
            await _inbox.AddAsync(command, _contextKey, requestContext, -1, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            WriteInboxEvent(span, command, "UseInboxHandler Add");

            return handledCommand;
        }

        /// <summary>
        /// Writes a telemetry event to the pipeline span recording that a duplicate command triggered an outbox replay.
        /// </summary>
        /// <remarks>
        /// The event is only written when there is a span to write to and the configured
        /// <see cref="InstrumentationOptions"/> for the pipeline include <see cref="InstrumentationOptions.Brighter"/>.
        /// </remarks>
        /// <param name="span">The pipeline <see cref="Activity"/> captured before the replay, or <c>null</c> if there is no span.</param>
        /// <param name="command">The duplicate command that triggered the replay.</param>
        /// <param name="causationId">The causation id whose outbox messages were replayed, if one was found.</param>
        private void WriteReplayEvent(Activity? span, T command, string? causationId)
        {
            if (span is null || Context is null || !Context.InstrumentationOptions.HasFlag(InstrumentationOptions.Brighter))
                return;

            var tags = new ActivityTagsCollection
            {
                { BrighterSemanticConventions.RequestId, command.Id.Value },
                { BrighterSemanticConventions.CausationId, causationId }
            };

            // Distinguish "replayed a causation's messages" from "nothing replayed because no causation id was found",
            // so operators don't read the event as a successful replay when the outbox was never asked to resend.
            var eventName = causationId is null
                ? "UseInboxHandler Duplicate Replay Skipped"
                : "UseInboxHandler Duplicate Replay";

            span.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
        }

        /// <summary>
        /// Writes a telemetry event to the pipeline span recording the outcome of handling a command (Add, Throw, or Warn).
        /// </summary>
        /// <remarks>
        /// The event is only written when there is a span to write to and the configured
        /// <see cref="InstrumentationOptions"/> for the pipeline include <see cref="InstrumentationOptions.Brighter"/>.
        /// </remarks>
        /// <param name="span">The pipeline <see cref="Activity"/>, or <c>null</c> if there is no span.</param>
        /// <param name="command">The command being handled.</param>
        /// <param name="eventName">The name of the telemetry event to write.</param>
        private void WriteInboxEvent(Activity? span, T command, string eventName)
        {
            if (span is null || Context is null || !Context.InstrumentationOptions.HasFlag(InstrumentationOptions.Brighter))
                return;

            var tags = new ActivityTagsCollection
            {
                { BrighterSemanticConventions.RequestId, command.Id.Value }
            };

            span.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "Checking if command {Id} has already been seen")]
            public static partial void CheckingIfCommandHasBeenSeen(ILogger logger, string id);

            [LoggerMessage(LogLevel.Debug, "Command {Id} has already been seen")]
            public static partial void CommandHasBeenSeen(ILogger logger, string id);

            [LoggerMessage(LogLevel.Warning, "Command {Id} has already been seen")]
            public static partial void CommandHasBeenSeenWarning(ILogger logger, string id);

            [LoggerMessage(LogLevel.Debug, "Command {Id} has already been seen; replaying its outbox messages")]
            public static partial void CommandHasBeenSeenReplayingOutbox(ILogger logger, string id);

            [LoggerMessage(LogLevel.Debug, "Writing command {Id} to the Inbox")]
            public static partial void WritingCommandToInbox(ILogger logger, string id);

            [LoggerMessage(LogLevel.Warning, "A custom IRequestContext (not a RequestContext) was supplied; the causation id cannot flow to downstream handlers, so OnceOnlyAction.Replay will be a no-op")]
            public static partial void CustomContextDisablesReplay(ILogger logger);
        }
    }
}

