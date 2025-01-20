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

    private static readonly MethodInfo s_sendMethod = typeof(SchedulerMessageFiredHandlerAsync)
        .GetMethod(nameof(SendAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_publishMethod = typeof(SchedulerMessageFiredHandlerAsync)
        .GetMethod(nameof(PublishAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_postMethod = typeof(SchedulerMessageFiredHandlerAsync)
        .GetMethod(nameof(PostAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly ConcurrentDictionary<Type, Func<IAmACommandProcessor, string, CancellationToken, Task>>
        s_send = new();

    private static readonly ConcurrentDictionary<Type, Func<IAmACommandProcessor, string, CancellationToken, Task>>
        s_publish = new();

    private static readonly ConcurrentDictionary<Type, Func<IAmACommandProcessor, string, CancellationToken, Task>>
        s_post = new();

    public override async Task<SchedulerMessageFired> HandleAsync(SchedulerMessageFired command,
        CancellationToken cancellationToken = default)
    {
        var type = s_types.GetOrAdd(command.MessageType, CreateType);
        if (command.FireType == SchedulerFireType.Send)
        {
            var send = s_send.GetOrAdd(type, CreateSend);
            await send(processor, command.MessageData, cancellationToken);
        }
        else if (command.FireType == SchedulerFireType.Publish)
        {
            var publish = s_publish.GetOrAdd(type, CreatePublish);
            await publish(processor, command.MessageData, cancellationToken);
        }
        else
        {
            var publish = s_post.GetOrAdd(type, CreatePost);
            await publish(processor, command.MessageData, cancellationToken);
        }

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

    private static async Task SendAsync<TRequest>(IAmACommandProcessor commandProcessor,
        string data,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
        await commandProcessor.SendAsync(request, cancellationToken: cancellationToken);
    }

    private static Func<IAmACommandProcessor, string, CancellationToken, Task> CreateSend(Type type)
    {
        var method = s_sendMethod.MakeGenericMethod(type);
        var action = (Func<IAmACommandProcessor, string, CancellationToken, Task>)method
            .CreateDelegate(typeof(Func<IAmACommandProcessor, string, CancellationToken, Task>));
        return action;
    }

    private static async Task PublishAsync<TRequest>(IAmACommandProcessor commandProcessor,
        string data,
        CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
        var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
        await commandProcessor.PublishAsync(request, cancellationToken: cancellationToken);
    }

    private static Func<IAmACommandProcessor, string, CancellationToken, Task> CreatePublish(Type type)
    {
        var method = s_publishMethod.MakeGenericMethod(type);
        var action = (Func<IAmACommandProcessor, string, CancellationToken, Task>)method
            .CreateDelegate(typeof(Func<IAmACommandProcessor, string, CancellationToken, Task>));
        return action;
    }


    private static async Task PostAsync<TRequest>(IAmACommandProcessor commandProcessor,
        string data,
        CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
        var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
        await commandProcessor.PostAsync(request, requestContext: new RequestContext(), cancellationToken: cancellationToken);
    }

    private static Func<IAmACommandProcessor, string, CancellationToken, Task> CreatePost(Type type)
    {
        var method = s_postMethod.MakeGenericMethod(type);
        var action = (Func<IAmACommandProcessor, string, CancellationToken, Task>)method
            .CreateDelegate(typeof(Func<IAmACommandProcessor, string, CancellationToken, Task>));
        return action;
    }
}
