#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorTransactionProviderSyncTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly Id _singleId;
    private readonly Id[] _batchIds;
    private readonly TestCommand _singleCommand;
    private readonly TestCommand _batchCommand1;
    private readonly TestCommand _batchCommand2;

    public SpyCommandProcessorTransactionProviderSyncTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        _singleCommand = new TestCommand();
        _batchCommand1 = new TestCommand();
        _batchCommand2 = new TestCommand();
        var transactionProvider = new StubTransactionProvider();

        //Act
        _singleId = _spy.DepositPost<TestCommand, object>(_singleCommand, transactionProvider);
        _batchIds = _spy.DepositPost<TestCommand, object>(
            new[] { _batchCommand1, _batchCommand2 }, transactionProvider);
    }

    [Fact]
    public void Then_single_deposit_should_record_deposit_type()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.Deposit);
    }

    [Fact]
    public void Then_single_deposit_should_return_request_id()
    {
        //Assert
        _singleId.ShouldBe(_singleCommand.Id);
    }

    [Fact]
    public void Then_single_deposit_should_be_in_deposited_requests()
    {
        //Assert
        _spy.DepositedRequests[_singleId].ShouldBeSameAs(_singleCommand);
    }

    [Fact]
    public void Then_batch_deposit_should_record_deposit_types()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.Deposit);
        _spy.Commands[2].ShouldBe(CommandType.Deposit);
    }

    [Fact]
    public void Then_batch_deposit_should_return_ids_for_each_request()
    {
        //Assert
        _batchIds.Length.ShouldBe(2);
        _batchIds[0].ShouldBe(_batchCommand1.Id);
        _batchIds[1].ShouldBe(_batchCommand2.Id);
    }

    [Fact]
    public void Then_batch_deposits_should_be_in_deposited_requests()
    {
        //Assert
        _spy.DepositedRequests[_batchIds[0]].ShouldBeSameAs(_batchCommand1);
        _spy.DepositedRequests[_batchIds[1]].ShouldBeSameAs(_batchCommand2);
    }

    private sealed class TestCommand() : Command(Id.Random());
}

public class SpyCommandProcessorTransactionProviderAsyncTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly Id _singleId;
    private readonly Id[] _batchIds;
    private readonly TestCommand _singleCommand;
    private readonly TestCommand _batchCommand1;
    private readonly TestCommand _batchCommand2;

    public SpyCommandProcessorTransactionProviderAsyncTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        _singleCommand = new TestCommand();
        _batchCommand1 = new TestCommand();
        _batchCommand2 = new TestCommand();
        var transactionProvider = new StubTransactionProvider();

        //Act
        _singleId = _spy.DepositPostAsync<TestCommand, object>(_singleCommand, transactionProvider)
            .GetAwaiter().GetResult();
        _batchIds = _spy.DepositPostAsync<TestCommand, object>(
            new[] { _batchCommand1, _batchCommand2 }, transactionProvider)
            .GetAwaiter().GetResult();
    }

    [Fact]
    public void Then_single_async_deposit_should_record_deposit_async_type()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.DepositAsync);
    }

    [Fact]
    public void Then_single_async_deposit_should_return_request_id()
    {
        //Assert
        _singleId.ShouldBe(_singleCommand.Id);
    }

    [Fact]
    public void Then_single_async_deposit_should_be_in_deposited_requests()
    {
        //Assert
        _spy.DepositedRequests[_singleId].ShouldBeSameAs(_singleCommand);
    }

    [Fact]
    public void Then_batch_async_deposit_should_record_deposit_async_types()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.DepositAsync);
        _spy.Commands[2].ShouldBe(CommandType.DepositAsync);
    }

    [Fact]
    public void Then_batch_async_deposit_should_return_ids_for_each_request()
    {
        //Assert
        _batchIds.Length.ShouldBe(2);
        _batchIds[0].ShouldBe(_batchCommand1.Id);
        _batchIds[1].ShouldBe(_batchCommand2.Id);
    }

    [Fact]
    public void Then_total_call_count_should_be_three()
    {
        //Assert
        _spy.CallCount(CommandType.DepositAsync).ShouldBe(3);
    }

    private sealed class TestCommand() : Command(Id.Random());
}

internal sealed class StubTransactionProvider : IAmABoxTransactionProvider<object>
{
    public object GetTransaction() => new();
    public Task<object> GetTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new object());
    public void Close() { }
    public void Commit() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Rollback() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public bool HasOpenTransaction => false;
    public bool IsSharedConnection => false;
}
