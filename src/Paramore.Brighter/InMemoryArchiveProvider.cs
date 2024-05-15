using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter;

/// <summary>
/// Use this archiver will result in messages being stored in memory. Mainly useful for tests.
/// Use the <see cref="NullOutboxArchiveProvider"/> if you just want to discard and not archive 
/// </summary>
public class InMemoryArchiveProvider: IAmAnArchiveProvider
{
    public Dictionary<string, Message> ArchivedMessages { get; set; } = new();
    
    public void ArchiveMessage(Message message)
    {
        ArchivedMessages.Add(message.Id, message);
    }

    public async Task ArchiveMessageAsync(Message message, CancellationToken cancellationToken)
    {
        ArchivedMessages.Add(message.Id, message);
    }

    public async Task<string[]> ArchiveMessagesAsync(Message[] messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            ArchivedMessages.Add(message.Id, message);
        }

        return messages.Select(m => m.Id).ToArray();
    }
}
