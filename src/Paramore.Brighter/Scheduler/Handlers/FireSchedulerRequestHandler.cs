using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

/// <summary>
/// The fire scheduler request handler
/// </summary>
public class FireSchedulerRequestHandler(IAmACommandProcessor processor) : RequestHandlerAsync<FireSchedulerRequest>
{
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
        if (command is { SchedulerType: RequestSchedulerType.Send, Async: true })
        {
            await processor.SendAsync(request, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Send)
        {
            processor.Send(request);
        }
        else if (command is { SchedulerType: RequestSchedulerType.Publish, Async: true })
        {
            await processor.PublishAsync(request, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Publish)
        {
            processor.Publish(request);
        }
        else if (command is { SchedulerType: RequestSchedulerType.Post, Async: true })
        {
            await processor.PostAsync(request, cancellationToken: cancellationToken);
        }
        else if (command.SchedulerType == RequestSchedulerType.Publish)
        {
            processor.Post(request);
        }
    }
}
