using System;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox.Exceptions;
using Xunit;

namespace Paramore.Brighter.Base.Test.Inbox;

public abstract class InboxTests : IDisposable
{
    protected abstract IAmAnInboxSync Inbox { get; }

    protected InboxTests()
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

    [Fact]
    public void WriteToInbox()
    {
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        
        // act 
        Inbox.Add(command, contextKey, null);
        var loadedCommand = Inbox.Get<MyCommand>(command.Id, contextKey, null);
        
        Assert.NotNull(loadedCommand);
        Assert.Equal(command.Value, loadedCommand.Value);
        Assert.Equal(command.Id, loadedCommand.Id);
    }
    
    [Fact]
    public void NotThrowExceptionWhenTryToAddMessageThatAlreadyExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        Inbox.Add(command, contextKey, null);

        // act 
        Inbox.Add(command, contextKey, null);
        
        // asserts
        var exists = Inbox.Exists<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public void NotThrowExceptionWhenTryToAddMessageWithDifferentContextKeyThatAlreadyExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        Inbox.Add(command, contextKey, null);

        // act 
        Inbox.Add(command, Uuid.NewAsString(), null);
        
        // asserts
        var exists = Inbox.Exists<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public void ThrowRequestNotFoundExceptionWhenTryToGetAMessageThatNotExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var commandId = Uuid.NewAsString();
        
        // act & asserts
        Assert.Throws<RequestNotFoundException<MyCommand>>(() => Inbox.Get<MyCommand>(commandId, contextKey, null));
    }
    
    [Fact]
    public void ThrowRequestNotFoundExceptionWhenTryToGetAMessageThatExistsWithDifferentContextKey()
    {
        var command = new MyCommand { Value = Uuid.NewAsString() };
        Inbox.Add(command, Uuid.NewAsString(), null);
        
        // act & asserts
        Assert.Throws<RequestNotFoundException<MyCommand>>(() => Inbox.Get<MyCommand>(command.Id, Uuid.NewAsString(), null));
    }

    [Fact]
    public void ReturnFalseWhenCheckIfAMessageExistsAndMessageNotExists()
    {
        var exists = Inbox.Exists<MyCommand>(Uuid.NewAsString(), Uuid.NewAsString(), null);
        Assert.False(exists, "A command should not exists");
    }
}
