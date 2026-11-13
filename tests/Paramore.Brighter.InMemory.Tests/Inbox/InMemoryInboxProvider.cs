using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Inbox.InMemory.Async;
using Paramore.Brighter.InMemory.Tests.Inbox.InMemory.Sync;

namespace Paramore.Brighter.InMemory.Tests.Inbox;

public class InMemoryInboxProvider : IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    private InMemoryInbox? _inbox;

    public IAmAnInboxSync CreateInbox() => _inbox ??= CreateInMemoryInbox();

    public IAmAnInboxAsync CreateInboxAsync() => _inbox ??= CreateInMemoryInbox();

    public void CreateStore() => _inbox = CreateInMemoryInbox();

    public Task CreateStoreAsync()
    {
        CreateStore();
        return Task.CompletedTask;
    }

    public void DeleteStore() => _inbox = null;

    public Task DeleteStoreAsync()
    {
        DeleteStore();
        return Task.CompletedTask;
    }

    private static InMemoryInbox CreateInMemoryInbox() => new(new FakeTimeProvider());
}
