using System;

namespace Paramore.Brighter.Extensions;

public static class MessageTypeExtensions
{
    public static MessageType RequestToMessageType(this IRequest request)
    {
        MessageType messageType;
        if (request is ICommand)
            messageType = MessageType.MT_COMMAND; 
        else if (request is IEvent)
            messageType = MessageType.MT_EVENT;
        else
        {
            throw new ArgumentException("This message mapper can only map Commands and Events", nameof(request));
        }

        return messageType;
    }
}
