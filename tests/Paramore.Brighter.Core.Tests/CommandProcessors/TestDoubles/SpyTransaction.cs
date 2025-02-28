using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

public class SpyTransaction
{
    private readonly List<Message> _messages = new List<Message>();

    public void Add(Message message)
    {
        _messages.Add(message);
    }

    public Message? Get(string messageId)
    {
        return _messages.FirstOrDefault(m => m.Id == messageId);
    }
}
