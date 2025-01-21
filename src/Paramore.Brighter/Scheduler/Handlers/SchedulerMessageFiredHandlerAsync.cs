using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

public class SchedulerMessageFiredHandlerAsync(IAmACommandProcessor processor)
    : RequestHandlerAsync<SchedulerMessageFired>
{
    private static readonly ConcurrentDictionary<string, Type> s_types = new();

    private static readonly MethodInfo s_executeAsyncMethod = typeof(SchedulerMessageFiredHandlerAsync)
        .GetMethod(nameof(ExecuteAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly ConcurrentDictionary<Type,
            Func<IAmACommandProcessor, string, bool, SchedulerFireType, CancellationToken, ValueTask>>
        s_executeAsync = new();

    public override async Task<SchedulerMessageFired> HandleAsync(SchedulerMessageFired command,
        CancellationToken cancellationToken = default)
    {
        var type = s_types.GetOrAdd(command.MessageType, CreateType);

        var execute = s_executeAsync.GetOrAdd(type, CreateExecuteAsync);
        await execute(processor, command.MessageData, command.UseAsync, command.FireType, cancellationToken);

        return await base.HandleAsync(command, cancellationToken);
    }

    private static Type CreateType(string messageType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var type = assembly.GetType(messageType);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"The message type could not be found: '{messageType}'");
    }

    private static ValueTask ExecuteAsync<TRequest>(IAmACommandProcessor commandProcessor,
        string data,
        bool async,
        SchedulerFireType fireType,
        CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
        var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
        switch (fireType)
        {
            case SchedulerFireType.Send when async:
                return new ValueTask(commandProcessor.SendAsync(request, cancellationToken: cancellationToken));
            case SchedulerFireType.Send:
                commandProcessor.Send(request);
                return new ValueTask();
            case SchedulerFireType.Publish when async:
                return new ValueTask(commandProcessor.PublishAsync(request, cancellationToken: cancellationToken));
            case SchedulerFireType.Publish:
                commandProcessor.Publish(request);
                return new ValueTask();
            case SchedulerFireType.Post when async:
                return new ValueTask(commandProcessor.PostAsync(request, cancellationToken: cancellationToken));
            default:
                commandProcessor.Post(request);
                return new ValueTask();
        }
    }

    private static Func<IAmACommandProcessor, string, bool, SchedulerFireType, CancellationToken, ValueTask>
        CreateExecuteAsync(Type type)
    {
        var method = s_executeAsyncMethod.MakeGenericMethod(type);
        var func = (Func<IAmACommandProcessor, string, bool, SchedulerFireType, CancellationToken, ValueTask>)method
            .CreateDelegate(
                typeof(Func<IAmACommandProcessor, string, bool, SchedulerFireType, CancellationToken, ValueTask>));
        return func;
    }
}
