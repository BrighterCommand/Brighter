using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox.Exceptions;
using Xunit;

namespace Paramore.Brighter.Base.Test.Inbox;

public abstract class InboxAsyncTest : IAsyncLifetime
{
    protected abstract IAmAnInboxAsync Inbox { get; }
    protected List<MyCommand> CreatedCommands { get; } = [];

    public async Task InitializeAsync()
    {
        await BeforeEachTestAsync();
    }

    public async Task DisposeAsync()
    {
        await AfterEachTestAsync();
    }

    protected virtual async Task BeforeEachTestAsync()
    {
        await CreateStoreAsync();
    }
    
    protected virtual Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }
    
    protected virtual async Task AfterEachTestAsync()
    {
        await DeleteStoreAsync();
    }

    protected virtual Task DeleteStoreAsync()
    {
        return Task.CompletedTask;
    }
    
    protected virtual MyCommand CreateCommand()
    {
        var command = new MyCommand { Value = Uuid.NewAsString() };
        
        CreatedCommands.Add(command);

        return command;
    }

    [Fact]
    public async Task WriteToInbox()
    {
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        
        // act 
        await Inbox.AddAsync(command, contextKey, null);
        var loadedCommand = await Inbox.GetAsync<MyCommand>(command.Id, contextKey, null);
        
        Assert.NotNull(loadedCommand);
        Assert.Equal(command.Value, loadedCommand.Value);
        Assert.Equal(command.Id, loadedCommand.Id);
    }
    
    [Fact]
    public async Task NotThrowExceptionWhenTryToAddMessageThatAlreadyExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        await Inbox.AddAsync(command, contextKey, null);

        // act 
        await Inbox.AddAsync(command, contextKey, null);
        
        // asserts
        var exists = await Inbox.ExistsAsync<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public async Task NotThrowExceptionWhenTryToAddMessageWithDifferentContextKeyThatAlreadyExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        await Inbox.AddAsync(command, contextKey, null);

        // act 
        await Inbox.AddAsync(command, Uuid.NewAsString(), null);
        
        // asserts
        var exists = await Inbox.ExistsAsync<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public async Task ThrowRequestNotFoundExceptionWhenTryToGetAMessageThatNotExists()
    {
        // setup
        var contextKey = Uuid.NewAsString();
        var commandId = Uuid.NewAsString();
        
        // act & asserts
        await Assert.ThrowsAsync<RequestNotFoundException<MyCommand>>(async () => await Inbox.GetAsync<MyCommand>(commandId, contextKey, null));
    }
    
    [Fact]
    public async Task ThrowRequestNotFoundExceptionWhenTryToGetAMessageThatExistsWithDifferentContextKey()
    {
        var command = CreateCommand();
        await Inbox.AddAsync(command, Uuid.NewAsString(), null);
        
        // act & asserts
        await Assert.ThrowsAsync<RequestNotFoundException<MyCommand>>(async () => await Inbox.GetAsync<MyCommand>(command.Id, Uuid.NewAsString(), null));
    }

    [Fact]
    public async Task ReturnFalseWhenCheckIfAMessageExistsAndMessageNotExists()
    {
        var exists = await Inbox.ExistsAsync<MyCommand>(Uuid.NewAsString(), Uuid.NewAsString(), null);
        Assert.False(exists, "A command should not exists");
    }
}
