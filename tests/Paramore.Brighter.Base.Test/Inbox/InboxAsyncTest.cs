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

    public async ValueTask InitializeAsync()
    {
        await BeforeEachTestAsync();
    }

    public async ValueTask DisposeAsync()
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
    public async Task When_Adding_A_Command_To_The_Inbox_It_Can_Be_Retrieved()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        
        // Act 
        await Inbox.AddAsync(command, contextKey, null);
        var loadedCommand = await Inbox.GetAsync<MyCommand>(command.Id, contextKey, null);
        
        // Assert
        Assert.NotNull(loadedCommand);
        Assert.Equal(command.Value, loadedCommand.Value);
        Assert.Equal(command.Id, loadedCommand.Id);
    }
    
    [Fact]
    public async Task When_Adding_A_Duplicate_Command_With_Same_Context_Key_It_Should_Not_Throw()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        await Inbox.AddAsync(command, contextKey, null);

        // Act 
        await Inbox.AddAsync(command, contextKey, null);
        
        // Assert
        var exists = await Inbox.ExistsAsync<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public async Task When_Adding_A_Duplicate_Command_With_Different_Context_Key_It_Should_Not_Throw()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        await Inbox.AddAsync(command, contextKey, null);

        // Act 
        await Inbox.AddAsync(command, Uuid.NewAsString(), null);
        
        // Assert
        var exists = await Inbox.ExistsAsync<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public async Task When_Getting_A_Non_Existent_Command_It_Should_Throw_RequestNotFoundException()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var commandId = Uuid.NewAsString();
        
        // Act & Assert
        await Assert.ThrowsAsync<RequestNotFoundException<MyCommand>>(async () => await Inbox.GetAsync<MyCommand>(commandId, contextKey, null));
    }
    
    [Fact]
    public async Task When_Getting_A_Command_With_Wrong_Context_Key_It_Should_Throw_RequestNotFoundException()
    {
        // Arrange
        var command = CreateCommand();
        await Inbox.AddAsync(command, Uuid.NewAsString(), null);
        
        // Act & Assert
        await Assert.ThrowsAsync<RequestNotFoundException<MyCommand>>(async () => await Inbox.GetAsync<MyCommand>(command.Id, Uuid.NewAsString(), null));
    }

    [Fact]
    public async Task When_Checking_If_A_Non_Existent_Command_Exists_It_Should_Return_False()
    {
        // Act
        var exists = await Inbox.ExistsAsync<MyCommand>(Uuid.NewAsString(), Uuid.NewAsString(), null);
        
        // Assert
        Assert.False(exists, "A command should not exists");
    }
}
