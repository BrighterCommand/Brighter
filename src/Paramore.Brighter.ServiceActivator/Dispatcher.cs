#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Status;
using BindingFlags = System.Reflection.BindingFlags;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class Dispatcher.
    /// The 'core' Service Activator class, the Dispatcher controls and co-ordinates the creation of readers from channels, and dispatching the commands and
    /// events translated from those messages to handlers. It controls the lifetime of the application through <see cref="Receive"/> and <see cref="End"/> and allows
    /// the stop and start of individual connections through <see cref="Open(string)"/> and <see cref="Shut(string)"/>
    /// </summary>
    public partial class Dispatcher : IDispatcher, IDisposable, IAsyncDisposable
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<Dispatcher>();

        private Task? _controlTask;
        private readonly object _lifecycleLock = new();
        private int _pendingConsumerOperations;
        private bool _stopping;
        private volatile DispatcherState _state;
        //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange,
        //making the disposal body run exactly once even under concurrent Dispose (an application-level
        //dispose racing the container's)
        private int _disposed;
        private readonly IAmAMessageMapperRegistry? _messageMapperRegistry;
        private readonly IAmAMessageTransformerFactory? _messageTransformerFactory;
        private readonly IAmAMessageMapperRegistryAsync? _messageMapperRegistryAsync;
        private readonly IAmAMessageTransformerFactoryAsync? _messageTransformerFactoryAsync;
        private readonly bool _ownsRegistry;
        private readonly bool _ownsTransformerFactories;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IAmABrighterTracer? _tracer;
        private readonly InstrumentationOptions _instrumentationOptions;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private readonly ConcurrentDictionary<string, IAmAConsumer> _consumers;

        /// <summary>
        /// Gets the command processor.
        /// </summary>
        /// <value>The command processor.</value>
        public IAmACommandProcessor CommandProcessor { get; private set; }
        
        /// <summary>
        /// Gets the connections.
        /// </summary>
        /// <value>The connections.</value>
        public IEnumerable<Subscription> Subscriptions { get; private set; }

        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        public IEnumerable<IAmAConsumer> Consumers => _consumers.Values;

        /// <summary>
        /// Gets or sets the name for this dispatcher instance.
        /// Used when communicating with this instance via the Control Bus
        /// </summary>
        /// <value>The name of the host.</value>
        public HostName HostName { get; set; } = new($"Brighter{Uuid.NewAsString()}");

        /// <summary>
        /// Gets the state of the <see cref="Dispatcher"/>
        /// </summary>
        /// <value>The state.</value>
        public DispatcherState State { get => _state; private set => _state = value; }

        /// <summary>
        /// The maximum time <see cref="Dispose"/> waits for the pumps to drain their in-flight message —
        /// after <see cref="End"/> has pushed a quit onto each pump — before it disposes the owned
        /// mapper/transform factories. Increase it for consumers with long-running handlers (for example
        /// video processing) so a message in progress can finish rather than be interrupted by teardown.
        /// On expiry disposal proceeds regardless; an interrupted message is left un-acknowledged so the
        /// broker redelivers it rather than dropping it. Defaults to 10 seconds.
        /// </summary>
        public TimeSpan ShutdownTimeout { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor we should use with the dispatcher (prefer to use Command Processor Provider for IoC Scope control</param>
        /// <param name="subscriptions">The subscriptions.</param>
        /// <param name="messageMapperRegistry">The message mapper registry.</param>
        /// <param name="messageMapperRegistryAsync">Async message mapper registry.</param>
        /// <param name="messageTransformerFactory">Creates instances of Transforms</param>
        /// <param name="messageTransformerFactoryAsync">Creates instances of Transforms async</param>
        /// <param name="requestContextFactory">The factory used to make a request synchronizationHelper</param>
        /// <param name="tracer">What is the <see cref="BrighterTracer"/> we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="ownsRegistry">
        /// Does this Dispatcher own the message mapper registry, so that <see cref="Dispose"/> should dispose it?
        /// Defaults to <c>false</c> for the manual-wiring path, where the registry is routinely shared with a
        /// <see cref="CommandProcessor"/>'s external bus and must not be torn down from under it. The DI path
        /// (<c>BuildDispatcher</c>) news up a registry solely for this Dispatcher and passes <c>true</c>.
        /// </param>
        /// <param name="ownsTransformerFactories">
        /// Does this Dispatcher own the transform factories, so that <see cref="Dispose"/> should dispose them?
        /// Defaults to <c>false</c> for the manual-wiring path; the DI path passes <c>true</c>.
        /// </param>
        /// <param name="shutdownTimeout">
        /// The maximum time <see cref="Dispose"/> waits for the pumps to drain their in-flight message before
        /// disposing the owned factories (see <see cref="ShutdownTimeout"/>). Defaults to 10 seconds when
        /// <c>null</c>.
        /// </param>
        /// throws <see cref="ConfigurationException">You must provide at least one type of message mapper registry</see>
        public Dispatcher(
            IAmACommandProcessor commandProcessor,
            IEnumerable<Subscription> subscriptions,
            IAmAMessageMapperRegistry? messageMapperRegistry = null,
            IAmAMessageMapperRegistryAsync? messageMapperRegistryAsync = null,
            IAmAMessageTransformerFactory? messageTransformerFactory = null,
            IAmAMessageTransformerFactoryAsync? messageTransformerFactoryAsync= null,
            IAmARequestContextFactory? requestContextFactory = null,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
            bool ownsRegistry = false,
            bool ownsTransformerFactories = false,
            TimeSpan? shutdownTimeout = null)
        {
            CommandProcessor = commandProcessor;
            ShutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(10);
            
            Subscriptions = subscriptions;
            _messageMapperRegistry = messageMapperRegistry;
            _messageMapperRegistryAsync = messageMapperRegistryAsync;
            _messageTransformerFactory = messageTransformerFactory;
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync;
            _ownsRegistry = ownsRegistry;
            _ownsTransformerFactories = ownsTransformerFactories;
            _requestContextFactory = requestContextFactory ?? new InMemoryRequestContextFactory();
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;

            if (messageMapperRegistry is null && messageMapperRegistryAsync is null)
                throw new ConfigurationException("You must provide a message mapper registry or an async message mapper registry");
                                       
            //not all pipelines need a transformer factory
            _messageTransformerFactory ??= new EmptyMessageTransformerFactory();
            _messageTransformerFactoryAsync ??= new EmptyMessageTransformerFactoryAsync();

            State = DispatcherState.DS_NOTREADY;

            _tasks = new ConcurrentDictionary<int, Task>();
            _consumers = new ConcurrentDictionary<string, IAmAConsumer>();

            State = DispatcherState.DS_AWAITING;
        }

        /// <summary>
        /// Disposes the runtime mapper registry and transform factories the Dispatcher owns.
        /// </summary>
        /// <remarks>
        /// The Dispatcher disposes only what it is told it owns (<c>ownsRegistry</c>/<c>ownsTransformerFactories</c>).
        /// On the DI path <c>BuildDispatcher</c> news up a fresh <see cref="IAmAMessageMapperRegistry"/>
        /// plus both transform factories for this Dispatcher and registers none of them in the container, so
        /// the Dispatcher is their sole owner and is constructed owning them. Each factory retains a
        /// per-resolution <see cref="System.IServiceScope"/>
        /// for any mapper/transform obtained but not released; without this cascade those scopes are held
        /// until the process exits rather than at container teardown — the same retention the producer-side
        /// <c>OutboxProducerMediator.Dispose</c> was extended to drain. The Dispatcher is registered as a
        /// container singleton, so the container disposes it at shutdown. On the manual-wiring path the
        /// registry is routinely shared with a <see cref="CommandProcessor"/>'s external bus, so the Dispatcher
        /// is constructed <b>not</b> owning it and this disposal leaves it intact for the other owner.
        /// <para>
        /// Disposal stops the pumps before it frees the factories: it calls <see cref="End"/> — which pushes a
        /// quit onto each running pump so it runs out its current message, acknowledges it, and stops — and
        /// waits for that drain, bounded by <see cref="ShutdownTimeout"/>, before disposing. Without this a
        /// still-running pump's in-flight <c>BuildUnwrapPipeline</c> would resolve through a disposed factory,
        /// throw <see cref="System.ObjectDisposedException"/>, and get the good message rejected as Unacceptable
        /// and dropped. The wait makes disposal self-ordering even when the hosted
        /// <c>ServiceActivatorHostedService.StopAsync</c> was skipped or abandoned on its own timeout, or the
        /// provider was disposed without a graceful stop. If the drain exceeds <see cref="ShutdownTimeout"/>
        /// disposal proceeds regardless; the interrupted message is left un-acknowledged so the broker
        /// redelivers it rather than losing it.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            //claim disposed up front with a single atomic exchange: each disposal below is a teardown backstop
            //that must run at most once. A throw from any step must not leave the flag unclaimed and let a
            //second Dispose() (an application-level one after the container's) re-dispose the factories.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            //stop the pumps before disposing the factories they resolve mappers/transforms from. End() pushes a
            //quit onto each running pump so it runs out its current message, acknowledges it, and stops; waiting
            //for that drain (bounded by ShutdownTimeout) keeps the factories from being torn down under an
            //in-flight message — which would surface as an ObjectDisposedException, be reclassified as
            //Unacceptable, and drop a good message. This makes disposal self-ordering even when the hosted
            //StopAsync was skipped or abandoned on timeout, or the provider was disposed without a graceful stop.
            //On expiry (a handler that will not finish in time) we dispose regardless; the interrupted message is
            //left un-acknowledged so the broker redelivers it rather than losing it.
            try
            {
                if (!End().Wait(ShutdownTimeout))
                    Log.ShutdownDrainTimedOut(s_logger, ShutdownTimeout.TotalMilliseconds);
            }
            catch (Exception e)
            {
                Log.FailedToDrainPumpsOnShutdown(s_logger, e);
            }

            //dispose only what this Dispatcher owns, and each owned factory independently so one factory's fault
            //cannot skip the rest. Disposing the registry cascades to the two mapper factories it holds. The
            //sync and async registry are the same instance on the DI path (BuildDispatcher passes one
            //MessageMapperRegistry as both), so guard the async disposal on reference identity to avoid
            //disposing it twice; MessageMapperRegistry.Dispose is idempotent regardless, but this keeps the
            //teardown log clean.
            if (_ownsRegistry)
            {
                DisposeQuietly(_messageMapperRegistry);
                if (!ReferenceEquals(_messageMapperRegistryAsync, _messageMapperRegistry))
                    DisposeQuietly(_messageMapperRegistryAsync);
            }

            if (_ownsTransformerFactories)
            {
                DisposeQuietly(_messageTransformerFactory);
                DisposeQuietly(_messageTransformerFactoryAsync);
            }
        }

        //Disposes a member if it is IDisposable, swallowing and logging any failure so one factory's fault
        //cannot skip the remaining disposals in the teardown chain.
        private static void DisposeQuietly(object? member)
        {
            try { (member as IDisposable)?.Dispose(); }
            catch (Exception e) { Log.FailedToDisposeOwnedResource(s_logger, member?.GetType().Name ?? "null", e); }
        }

        /// <summary>
        /// The awaitable twin of <see cref="Dispose"/>. Prefer this on the graceful shutdown path.
        /// </summary>
        /// <remarks>
        /// <see cref="Dispose"/> drains the pumps with a blocking <c>End().Wait(ShutdownTimeout)</c>, which parks a
        /// thread for up to <see cref="ShutdownTimeout"/> when a consumer is slow to finish its in-flight message.
        /// A host that tears down its service provider through <c>DisposeAsync</c> — which Microsoft.Extensions.
        /// DependencyInjection honours in preference to <see cref="IDisposable"/> when a service implements both —
        /// gets that drain <b>awaited</b> here instead, freeing the thread while the pumps run out their current
        /// message. The ordering and the ownership rules are identical to <see cref="Dispose"/> (see its remarks):
        /// the drain (bounded by <see cref="ShutdownTimeout"/>) completes before the owned factories are disposed,
        /// and an owned factory that is itself <see cref="IAsyncDisposable"/> is torn down through its async path.
        /// <see cref="IDisposable"/> is kept alongside because MS DI throws if it must synchronously dispose a
        /// service that is only <see cref="IAsyncDisposable"/>; the two paths share the run-at-most-once flag, so
        /// disposing both ways (or twice) drains and disposes exactly once.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            //share the atomic run-at-most-once flag with Dispose(): whichever path (or a race between them) claims
            //it first runs the teardown; the other returns. Each disposal below is a backstop that must run once.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            //await the pump drain rather than blocking on it. End() pushes a quit onto each running pump so it runs
            //out its current message, acknowledges it, and stops; we await that drain bounded by ShutdownTimeout.
            //On expiry we dispose regardless — the interrupted message is left un-acknowledged for redelivery — for
            //the same message-loss reason spelled out on Dispose().
            try
            {
                var drain = End();
                if (await Task.WhenAny(drain, Task.Delay(ShutdownTimeout)).ConfigureAwait(false) != drain)
                    Log.ShutdownDrainTimedOut(s_logger, ShutdownTimeout.TotalMilliseconds);
                else
                    await drain.ConfigureAwait(false); //observe any fault the drain surfaced
            }
            catch (Exception e)
            {
                Log.FailedToDrainPumpsOnShutdown(s_logger, e);
            }

            //dispose only what this Dispatcher owns, each independently, preferring the async path. Mirrors the
            //reference-identity guard in Dispose() for the shared sync/async registry on the DI path.
            if (_ownsRegistry)
            {
                await DisposeQuietlyAsync(_messageMapperRegistry).ConfigureAwait(false);
                if (!ReferenceEquals(_messageMapperRegistryAsync, _messageMapperRegistry))
                    await DisposeQuietlyAsync(_messageMapperRegistryAsync).ConfigureAwait(false);
            }

            if (_ownsTransformerFactories)
            {
                await DisposeQuietlyAsync(_messageTransformerFactory).ConfigureAwait(false);
                await DisposeQuietlyAsync(_messageTransformerFactoryAsync).ConfigureAwait(false);
            }
        }

        //Disposes a member through IAsyncDisposable when it offers one, else IDisposable, swallowing and logging
        //any failure so one factory's fault cannot skip the remaining disposals in the teardown chain.
        private static async ValueTask DisposeQuietlyAsync(object? member)
        {
            try
            {
                switch (member)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception e) { Log.FailedToDisposeOwnedResource(s_logger, member?.GetType().Name ?? "null", e); }
        }

        /// <summary>
        /// Stop listening to messages
        /// </summary>
        /// <returns>A task that completes after accepted consumer operations and all performers have drained.</returns>
        /// <remarks>
        /// New receive, open, and scaling operations are rejected until the returned task completes.
        /// Consumers still being created are disposed without starting their performers.
        /// </remarks>
        public Task End()
        {
            IAmAConsumer[] consumers;
            Task controlTask;
            lock (_lifecycleLock)
            {
                controlTask = _controlTask ?? Task.CompletedTask;
                if (State != DispatcherState.DS_RUNNING)
                    return controlTask;

                _stopping = true;
                consumers = _consumers.Values.ToArray();
            }

            Log.StoppingDispatcher(s_logger);
            consumers.Each(consumer => consumer.Shut(consumer.Subscription.RoutingKey));
            return controlTask;
        }

        /// <summary>
        /// Opens the specified subscription by name 
        /// </summary>
        /// <param name="subscriptionName">The subscription name.</param>
        /// <exception cref="InvalidOperationException">The dispatcher is stopping.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher has been disposed.</exception>
        public void Open(SubscriptionName subscriptionName)
        {
            Open(Subscriptions.Single(c => c.Name == subscriptionName));
        }

        /// <summary>
        /// Opens the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        /// <exception cref="InvalidOperationException">The dispatcher is stopping.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher has been disposed.</exception>
        public void Open(Subscription subscription)
        {
            ExecuteConsumerOperation(_ =>
            {
                Log.OpeningSubscription(s_logger, subscription.Name.Value);
                lock (_lifecycleLock)
                    AddSubscriptionToSubscriptions(subscription);
                return CreateConsumers([subscription]);
            });
        }

        private void AddSubscriptionToSubscriptions(Subscription subscription)
        {
            if (Subscriptions.All(c => c.Name != subscription.Name))
            {
                Subscriptions = new List<Subscription>(Subscriptions) { subscription };
            }
        }

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        /// <exception cref="InvalidOperationException">The dispatcher is stopping.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher has been disposed.</exception>
        public void Receive()
        {
            ExecuteConsumerOperation(_ => CreateConsumers(Subscriptions));
        }

        /// <summary>
        /// Shuts the specified subscription by name
        /// </summary>
        /// <param name="subscriptionName">The name of the subscription</param>
        public void Shut(SubscriptionName subscriptionName)
        {
            Shut(Subscriptions.Single(c => c.Name == subscriptionName));
        }

        /// <summary>
        /// Shuts the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        public void Shut(Subscription subscription)
        {
            if (State == DispatcherState.DS_RUNNING)
            {
                Log.StoppingSubscription(s_logger, subscription.Name.Value);
                var consumersForConnection = Consumers.Where(consumer => consumer.Subscription.Name == subscription.Name).ToArray();
                var noOfConsumers = consumersForConnection.Length;
                for (int i = 0; i < noOfConsumers; ++i)
                {
                    consumersForConnection[i].Shut(subscription.RoutingKey);
                }
            }
        }

        public DispatcherStateItem[] GetState()
        {
            return Subscriptions.Select(s => new DispatcherStateItem(s.Name.Value,
                s.NoOfPerformers,
                _consumers.Where(c => c.Value.Subscription.Name == s.Name)
                    .Select(c => new PerformerInformation(c.Value.Name.Value, c.Value.State)).ToArray())
            ).ToArray();
        }

        /// <summary>
        /// Changes the number of performers for a subscription, starting a new run when stopped.
        /// </summary>
        /// <param name="connectionName">The subscription name.</param>
        /// <param name="numberOfPerformers">The requested number of performers; negative values are treated as zero.</param>
        /// <exception cref="InvalidOperationException">The dispatcher is stopping, or the subscription does not exist.</exception>
        /// <exception cref="ObjectDisposedException">The dispatcher has been disposed.</exception>
        public void SetActivePerformers(string connectionName, int numberOfPerformers)
        {
            ExecuteConsumerOperation(starting =>
            {
                Subscription subscription;
                int currentPerformers;
                int desiredPerformers;
                IAmAConsumer[] consumersToClose;
                lock (_lifecycleLock)
                {
                    subscription = Subscriptions.Single(c => c.Name == connectionName);
                    currentPerformers = starting ? 0 : subscription.NoOfPerformers;
                    subscription.SetNumberOfPerformers(numberOfPerformers);
                    desiredPerformers = subscription.NoOfPerformers;
                    consumersToClose = _consumers.Values
                        .Where(c => c.Subscription.Name == subscription.Name)
                        .Take(Math.Max(0, currentPerformers - desiredPerformers)).ToArray();
                }

                if (currentPerformers < desiredPerformers)
                    return CreateConsumers([subscription], desiredPerformers - currentPerformers);

                consumersToClose.Each(consumer => consumer.Shut(subscription.RoutingKey));
                return Array.Empty<Consumer>();
            });
        }

        private void ExecuteConsumerOperation(Func<bool, IEnumerable<Consumer>> createConsumers)
        {
            var starting = BeginConsumerOperation();
            Consumer[] consumers = [];
            try
            {
                consumers = createConsumers(starting).ToArray();
                lock (_lifecycleLock)
                {
                    foreach (var consumer in consumers)
                        _consumers.TryAdd(consumer.Name.Value, consumer);
                }

                if (starting)
                    Log.DispatcherStarting(s_logger);
                foreach (var consumer in consumers)
                    OpenConsumer(consumer);
                if (starting)
                    Log.DispatcherStartingPerformers(s_logger, _tasks.Count);
            }
            catch
            {
                foreach (var consumer in consumers)
                {
                    consumer.Shut(consumer.Subscription.RoutingKey);
                    if (consumer.Job is null)
                        RemoveUnopenedConsumer(consumer);
                }
                throw;
            }
            finally
            {
                lock (_lifecycleLock)
                {
                    --_pendingConsumerOperations;
                    Monitor.PulseAll(_lifecycleLock);
                }
            }
        }

        private bool BeginConsumerOperation()
        {
            lock (_lifecycleLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(Dispatcher));
                if (_stopping && _controlTask?.IsCompleted != true)
                    throw new InvalidOperationException("The dispatcher is stopping. Await End() before opening consumers.");

                ++_pendingConsumerOperations;
                if (State == DispatcherState.DS_RUNNING)
                    return false;

                _stopping = false;
                State = DispatcherState.DS_RUNNING;
                _controlTask = Task.Factory.StartNew(RunControlLoop, CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
                return true;
            }
        }

        private void RunControlLoop()
        {
            while (true)
            {
                Task[] runningTasks;
                lock (_lifecycleLock)
                {
                    // Accepted operations own resources even before their tasks can be registered.
                    while (_tasks.IsEmpty && _pendingConsumerOperations != 0)
                        Monitor.Wait(_lifecycleLock);
                    if (_tasks.IsEmpty)
                    {
                        State = DispatcherState.DS_STOPPED;
                        break;
                    }
                    runningTasks = _tasks.Values.ToArray();
                }
                HandleNextStoppedPerformer(runningTasks);
            }
            Log.DispatcherStopped(s_logger);
        }

        private void OpenConsumer(IAmAConsumer consumer)
        {
            lock (_lifecycleLock)
            {
                if (_stopping)
                    consumer.Shut(consumer.Subscription.RoutingKey);
                consumer.Open();
                if (consumer.Job is { } job)
                {
                    _tasks.TryAdd(consumer.JobId, job);
                    Monitor.PulseAll(_lifecycleLock);
                    return;
                }
            }

            RemoveUnopenedConsumer(consumer);
        }

        private void RemoveUnopenedConsumer(IAmAConsumer consumer)
        {
            // A consumer shut before opening has no task to trigger the normal cleanup path.
            if (_consumers.TryRemove(consumer.Name.Value, out var unopenedConsumer))
            {
                Log.RemovingConsumer(s_logger, unopenedConsumer.Name.Value);
                DisposeConsumer(unopenedConsumer);
            }
        }

        private static void DisposeConsumer(IAmAConsumer consumer)
        {
            try
            {
                consumer.Dispose();
            }
            catch (Exception ex)
            {
                Log.ErrorOnConsumer(s_logger, ex);
            }
        }

        private void HandleNextStoppedPerformer(Task[] runningTasks)
        {
            var index = Task.WaitAny(runningTasks);
            var stoppingConsumer = runningTasks[index];
            Log.PerformerStopped(s_logger, stoppingConsumer.Status);

            // Retire the completed task before cleanup so a disposal failure cannot leave it pending.
            _tasks.TryRemove(stoppingConsumer.Id, out _);
            try
            {
                using (stoppingConsumer)
                {
                    // WaitAny does not observe faults, including disposal failures inside the pump.
                    if (stoppingConsumer.Exception is { } taskException)
                    {
                        foreach (var exception in taskException.Flatten().InnerExceptions)
                            Log.ErrorOnConsumer(s_logger, exception);
                    }

                    RemoveConsumerForTask(stoppingConsumer);
                }
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex =>
                {
                    Log.ErrorOnConsumer(s_logger, ex);
                    return true;
                });
            }
            catch (Exception ex)
            {
                Log.ErrorOnConsumer(s_logger, ex);
            }
        }

        private void RemoveConsumerForTask(Task stoppingConsumer)
        {
            var consumer = Consumers.SingleOrDefault(c => c.JobId == stoppingConsumer.Id);
            if (consumer is null)
                return;

            Log.RemovingConsumer(s_logger, consumer.Name.Value);

            if (_consumers.TryRemove(consumer.Name.Value, out consumer))
            {
                consumer.Dispose();
            }
        }

        private IEnumerable<Consumer> CreateConsumers(IEnumerable<Subscription> subscriptions, int? numberOfPerformers = null)
        {
            var consumers = new List<Consumer>();
            try
            {
                foreach (var subscription in subscriptions)
                {
                    for (var i = 0; i < (numberOfPerformers ?? subscription.NoOfPerformers); i++)
                        consumers.Add(CreateConsumer(subscription, i + 1));
                }
                return consumers;
            }
            catch
            {
                foreach (var consumer in consumers)
                    DisposeConsumer(consumer);
                throw;
            }
        }

        private Consumer CreateConsumer(Subscription subscription, int? consumerNumber)
        {
            Log.CreatingConsumer(s_logger, consumerNumber, subscription.Name.Value);
                
            if (subscription.MessagePumpType == MessagePumpType.Reactor)
            {
                if (_messageMapperRegistry is null)
                    throw new ConfigurationException("You must provide a message mapper registry for the Dispatcher to work");
                
                var consumerFactory = new ConsumerFactory(CommandProcessor, subscription, _messageMapperRegistry, _messageTransformerFactory, 
                    _requestContextFactory, _tracer, _instrumentationOptions);

                return consumerFactory.Create();
            }
            else
            {
                if (_messageMapperRegistryAsync is null)
                    throw new ConfigurationException("You must provide a message mapper registry for the Dispatcher to work");
                    
                var consumerFactory = new ConsumerFactory(CommandProcessor, subscription, _messageMapperRegistryAsync, _messageTransformerFactoryAsync, 
                    _requestContextFactory, _tracer, _instrumentationOptions);

                return consumerFactory.Create();
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Dispatcher: Stopping dispatcher")]
            public static partial void StoppingDispatcher(ILogger logger);

            [LoggerMessage(LogLevel.Information, "Dispatcher: Opening subscription {ChannelName}")]
            public static partial void OpeningSubscription(ILogger logger, string channelName);
            
            [LoggerMessage(LogLevel.Information, "Dispatcher: Stopping subscription {ChannelName}")]
            public static partial void StoppingSubscription(ILogger logger, string channelName);

            [LoggerMessage(LogLevel.Information, "Dispatcher: Dispatcher starting")]
            public static partial void DispatcherStarting(ILogger logger);

            [LoggerMessage(LogLevel.Information, "Dispatcher: Dispatcher starting {Consumers} performers")]
            public static partial void DispatcherStartingPerformers(ILogger logger, int consumers);

            [LoggerMessage(LogLevel.Debug, "Dispatcher: Performer stopped with state {Status}")]
            public static partial void PerformerStopped(ILogger logger, TaskStatus status);

            [LoggerMessage(LogLevel.Debug, "Dispatcher: Removing a consumer with subscription name {ChannelName}")]
            public static partial void RemovingConsumer(ILogger logger, string channelName);

            [LoggerMessage(LogLevel.Error, "Dispatcher: Error on consumer; consumer shut down")]
            public static partial void ErrorOnConsumer(ILogger logger, Exception ex);

            [LoggerMessage(LogLevel.Information, "Dispatcher: Dispatcher stopped")]
            public static partial void DispatcherStopped(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Dispatcher: Creating consumer number {ConsumerNumber} for subscription: {ChannelName}")]
            public static partial void CreatingConsumer(ILogger logger, int? consumerNumber, string channelName);

            [LoggerMessage(LogLevel.Error, "Dispatcher: Failed to dispose owned resource {Resource} at shutdown")]
            public static partial void FailedToDisposeOwnedResource(ILogger logger, string resource, Exception exception);

            [LoggerMessage(LogLevel.Warning, "Dispatcher: Pumps did not drain within {TimeoutMs}ms on shutdown; disposing anyway. Any in-flight message is left un-acknowledged for redelivery")]
            public static partial void ShutdownDrainTimedOut(ILogger logger, double timeoutMs);

            [LoggerMessage(LogLevel.Error, "Dispatcher: Failed while draining the pumps on shutdown; disposing anyway")]
            public static partial void FailedToDrainPumpsOnShutdown(ILogger logger, Exception exception);
        }
    }
}

