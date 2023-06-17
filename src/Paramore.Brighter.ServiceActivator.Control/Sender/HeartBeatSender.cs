using Paramore.Brighter.ServiceActivator.Control.Extensions;

namespace Paramore.Brighter.ServiceActivator.Control.Sender;

public static class HeartBeatSender
{
    public static void Send(IAmACommandProcessor commandProcessor, IDispatcher dispatcher)
    {
        commandProcessor.Post(dispatcher.GetNodeStatusEvent());
    }
}
