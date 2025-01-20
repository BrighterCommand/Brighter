using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

// public class SchedulerMessageFiredHandler(IAmACommandProcessor processor) : RequestHandler<SchedulerMessageFired>
// {
//     private static readonly ConcurrentDictionary<string, Type> s_types = new();
//
//     private static readonly MethodInfo s_sendMethod = typeof(SchedulerMessageFiredHandler)
//         .GetMethod(nameof(Send), BindingFlags.Static | BindingFlags.NonPublic)!;
//
//     private static readonly MethodInfo s_publishMethod = typeof(SchedulerMessageFiredHandler)
//         .GetMethod(nameof(Publish), BindingFlags.Static | BindingFlags.NonPublic)!;
//     
//     private static readonly MethodInfo s_postMethod = typeof(SchedulerMessageFiredHandler)
//         .GetMethod(nameof(Post), BindingFlags.Static | BindingFlags.NonPublic)!;
//
//     private static readonly ConcurrentDictionary<Type, Action<IAmACommandProcessor, string>> s_send = new();
//     private static readonly ConcurrentDictionary<Type, Action<IAmACommandProcessor, string>> s_publish = new();
//     private static readonly ConcurrentDictionary<Type, Action<IAmACommandProcessor, string>> s_post = new();
//
//     public override SchedulerMessageFired Handle(SchedulerMessageFired command)
//     {
//         var type = s_types.GetOrAdd(command.MessageType, CreateType);
//         if (command.FireType == SchedulerFireType.Send)
//         {
//             var send = s_send.GetOrAdd(type, CreateSend);
//             send(processor, command.MessageData);
//         }
//         else if (command.FireType == SchedulerFireType.Publish)
//         {
//             var publish = s_publish.GetOrAdd(type, CreatePublish);
//             publish(processor, command.MessageData);
//         }
//         else
//         {
//             var publish = s_publish.GetOrAdd(type, CreatePost);
//             publish(processor, command.MessageData);
//         }
//
//         return base.Handle(command);
//     }
//
//     private static Type CreateType(string messageType)
//     {
//         var type = Type.GetType(messageType);
//         if (type == null)
//         {
//             throw new InvalidOperationException($"The message type doesn't exits: '{messageType}'");
//         }
//
//         return type;
//     }
//
//     private static void Send<TRequest>(IAmACommandProcessor commandProcessor, string data)
//         where TRequest : class, IRequest
//     {
//         var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
//         commandProcessor.Send(request);
//     }
//
//     private static Action<IAmACommandProcessor, string> CreateSend(Type type)
//     {
//         var method = s_sendMethod.MakeGenericMethod(type);
//         var action = (Action<IAmACommandProcessor, string>)method
//             .CreateDelegate(typeof(Action<IAmACommandProcessor, string>));
//         return action;
//     }
//
//     private static void Publish<TRequest>(IAmACommandProcessor commandProcessor, string data)
//         where TRequest : class, IRequest
//     {
//         var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
//         commandProcessor.Publish(request);
//     }
//
//     private static Action<IAmACommandProcessor, string> CreatePublish(Type type)
//     {
//         var method = s_publishMethod.MakeGenericMethod(type);
//         var action = (Action<IAmACommandProcessor, string>)method
//             .CreateDelegate(typeof(Action<IAmACommandProcessor, string>));
//         return action;
//     }
//
//
//     private static void Post<TRequest>(IAmACommandProcessor commandProcessor, string data)
//         where TRequest : class, IRequest
//     {
//         var request = JsonSerializer.Deserialize<TRequest>(data, JsonSerialisationOptions.Options)!;
//         commandProcessor.Post(request);
//     }
//
//     private static Action<IAmACommandProcessor, string> CreatePost(Type type)
//     {
//         var method = s_postMethod.MakeGenericMethod(type);
//         var action = (Action<IAmACommandProcessor, string>)method
//             .CreateDelegate(typeof(Action<IAmACommandProcessor, string>));
//         return action;
//     }
// }
