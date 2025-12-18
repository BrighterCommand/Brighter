using System;
using System.Collections.Generic;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox.Exceptions;
using Xunit;

namespace Paramore.Brighter.Base.Test.Inbox;

public abstract class InboxTests : IDisposable
{
    protected abstract IAmAnInboxSync Inbox { get; }
    protected virtual List<MyCommand> CreatedCommands { get; } = [];

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
    
    protected virtual MyCommand CreateCommand()
    {
        var command = new MyCommand { Value = Uuid.NewAsString() };
        
        CreatedCommands.Add(command);

        return command;
    }

    [Fact]
    public void When_Adding_A_Command_To_The_Inbox_It_Can_Be_Retrieved()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        
        // Act 
        Inbox.Add(command, contextKey, null);
        var loadedCommand = Inbox.Get<MyCommand>(command.Id, contextKey, null);
        
        // Assert
        Assert.NotNull(loadedCommand);
        Assert.Equal(command.Value, loadedCommand.Value);
        Assert.Equal(command.Id, loadedCommand.Id);
    }
    
    [Fact]
    public void When_Adding_A_Duplicate_Command_With_Same_Context_Key_It_Should_Not_Throw()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        
        Inbox.Add(command, contextKey, null);

        // Act 
        Inbox.Add(command, contextKey, null);
        
        // Assert
        var exists = Inbox.Exists<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public void When_Adding_A_Duplicate_Command_With_Different_Context_Key_It_Should_Not_Throw()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var command = CreateCommand();
        
        Inbox.Add(command, contextKey, null);

        // Act 
        Inbox.Add(command, Uuid.NewAsString(), null);
        
        // Assert
        var exists = Inbox.Exists<MyCommand>(command.Id, contextKey, null);
        Assert.True(exists, $"A command with '{command.Id.Value}' Id should exists");
    }
    
    [Fact]
    public void When_Getting_A_Non_Existent_Command_It_Should_Throw_RequestNotFoundException()
    {
        // Arrange
        var contextKey = Uuid.NewAsString();
        var commandId = Uuid.NewAsString();
        
        // Act & Assert
        Assert.Throws<RequestNotFoundException<MyCommand>>(() => Inbox.Get<MyCommand>(commandId, contextKey, null));
    }
    
    [Fact]
    public void When_Getting_A_Command_With_Wrong_Context_Key_It_Should_Throw_RequestNotFoundException()
    {
        // Arrange
        var command = CreateCommand();
        Inbox.Add(command, Uuid.NewAsString(), null);
        
        // Act & Assert
        Assert.Throws<RequestNotFoundException<MyCommand>>(() => Inbox.Get<MyCommand>(command.Id, Uuid.NewAsString(), null));
    }

    [Fact]
    public void When_Checking_If_A_Non_Existent_Command_Exists_It_Should_Return_False()
    {
        // Act
        var exists = Inbox.Exists<MyCommand>(Uuid.NewAsString(), Uuid.NewAsString(), null);
        
        // Assert
        Assert.False(exists, "A command should not exists");
    }
}
