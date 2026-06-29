using System;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Requests;
using Xunit;

namespace Paramore.Brighter.Base.Test.Inbox;

/// <summary>
/// Base test class that defines the causation-tracking scenarios every <see cref="IAmACausationTrackingInbox"/>
/// implementation must satisfy. Persistent store test projects inherit this and supply their store via
/// <see cref="Inbox"/> plus the <see cref="CreateStore"/>/<see cref="DeleteStore"/> hooks.
/// </summary>
public abstract class CausationTrackingInboxBaseTests : IDisposable
{
    protected const string CausationId = "causation-123";

    /// <summary>
    /// The store under test. Must also implement <see cref="IAmAnInboxAsync"/> and
    /// <see cref="IAmACausationTrackingInbox"/>.
    /// </summary>
    protected abstract IAmAnInboxSync Inbox { get; }

    private IAmAnInboxAsync InboxAsync => (IAmAnInboxAsync)Inbox;
    private IAmACausationTrackingInbox TrackingInbox => (IAmACausationTrackingInbox)Inbox;

    protected CausationTrackingInboxBaseTests()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        BeforeEachTest();
    }

    protected virtual void BeforeEachTest()
    {
        CreateStore();
    }

    protected virtual void CreateStore()
    {
    }

    public void Dispose()
    {
        AfterEachTest();
    }

    protected virtual void AfterEachTest()
    {
        DeleteStore();
    }

    protected virtual void DeleteStore()
    {
    }

    protected virtual MyCommand CreateCommand()
    {
        return new MyCommand { Value = Uuid.NewAsString() };
    }

    private static RequestContext ContextWithCausation(string causationId)
    {
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        return context;
    }

    [Fact]
    public void When_adding_to_inbox_with_causation_id_should_store_and_retrieve()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        var context = ContextWithCausation(CausationId);

        // Act
        Inbox.Add(command, contextKey, context);
        var storedCausationId = TrackingInbox.GetCausationId(command.Id, contextKey, context);

        // Assert
        Assert.Equal(CausationId, storedCausationId);
    }

    [Fact]
    public async Task When_adding_to_inbox_with_causation_id_should_store_and_retrieve_async()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        var context = ContextWithCausation(CausationId);

        // Act
        await InboxAsync.AddAsync(command, contextKey, context);
        var storedCausationId = await TrackingInbox.GetCausationIdAsync(command.Id, contextKey, context);

        // Assert
        Assert.Equal(CausationId, storedCausationId);
    }

    [Fact]
    public void When_adding_to_inbox_without_causation_id_should_return_null()
    {
        // Arrange — no CausationId placed in the context bag
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        var context = new RequestContext();

        // Act
        Inbox.Add(command, contextKey, context);
        var storedCausationId = TrackingInbox.GetCausationId(command.Id, contextKey, context);

        // Assert
        Assert.Null(storedCausationId);
    }

    [Fact]
    public void When_asking_inbox_if_it_supports_causation_tracking_should_be_true()
    {
        // Act
        var supportsCausationTracking = TrackingInbox.SupportsCausationTracking();

        // Assert
        Assert.True(supportsCausationTracking);
    }
}
