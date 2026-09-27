using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

/// <summary>
/// The fire scheduler request handler
/// </summary>
/// <param name="processor">The command processor that executes the scheduled request.</param>
/// <param name="tracer">The tracer used to resume the propagated trace.</param>
public class FireSchedulerRequestHandler(IAmACommandProcessor processor, IAmABrighterTracer? tracer) : RequestHandlerAsync<FireSchedulerRequest>
{
    /// <summary>Creates a handler without tracing.</summary>
    /// <param name="processor">The command processor that executes the scheduled request.</param>
    public FireSchedulerRequestHandler(IAmACommandProcessor processor) : this(processor, null)
    {
    }

    private static readonly ConcurrentDictionary<string, Func<FireSchedulerRequestHandler, FireSchedulerRequest, CancellationToken, Task>> s_executions = new();

    private static readonly MethodInfo s_executeMethod = typeof(FireSchedulerRequestHandler)
        .GetMethod(nameof(ExecuteAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;
    
    public override async Task<FireSchedulerRequest> HandleAsync(FireSchedulerRequest command,
        CancellationToken cancellationToken = default)
    {
        var exec = GetExecution(command.RequestType);
        await exec(this, command, cancellationToken);
        
        return await base.HandleAsync(command, cancellationToken);
    }

    private Func<FireSchedulerRequestHandler, FireSchedulerRequest, CancellationToken, Task> GetExecution(string requestType)
    {
        return s_executions.GetOrAdd(requestType, CreateMethod);

        static Func<FireSchedulerRequestHandler, FireSchedulerRequest, CancellationToken, Task> CreateMethod(string requestType)
        {
            var type = LoadType(requestType);
            var method = s_executeMethod.MakeGenericMethod(type);
            return method
                .CreateDelegate<Func<FireSchedulerRequestHandler, FireSchedulerRequest, CancellationToken, Task>>();
        }

        static Type LoadType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(fullName);
                if (type is { IsClass: true } && type.IsAssignableTo(typeof(IRequest)))
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"The '{fullName}' was not founded");
        }
    }

    private async Task ExecuteAsync<T>(FireSchedulerRequest command, CancellationToken cancellationToken = default)
        where T : class, IRequest
    {
        var request = JsonSerializer.Deserialize<T>(command.RequestData, JsonSerialisationOptions.Options)!;
        var snapshot = command.RequestContextData == null ? null
            : JsonSerializer.Deserialize<ScheduledRequestContext>(command.RequestContextData, JsonSerialisationOptions.Options);
        var context = snapshot?.Restore();
        using var span = snapshot?.TraceParent == null ? null
            : tracer?.ActivitySource.StartActivity($"{typeof(T).Name} process", ActivityKind.Consumer, snapshot.TraceParent);
        if (span != null)
        {
            span.TraceStateString = snapshot!.TraceState;
            if (snapshot.Baggage != null)
            {
                foreach (var entry in snapshot.Baggage)
                    span.AddBaggage(entry.Key, entry.Value);
            }
            context!.Span = span;
        }

        if (command is { SchedulerType: RequestSchedulerType.Send, Async: true })
        {
            await processor.SendAsync(request, context, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Send)
        {
            processor.Send(request, context);
        }
        else if (command is { SchedulerType: RequestSchedulerType.Publish, Async: true })
        {
            await processor.PublishAsync(request, context, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Publish)
        {
            processor.Publish(request, context);
        }
        else if (command is { SchedulerType: RequestSchedulerType.Post, Async: true })
        {
            await processor.PostAsync(request, context, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Post)
        {
            processor.Post(request, context);
        }
    }
}
