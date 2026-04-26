using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MongoDB.Driver;
using Paramore.Brighter.MongoDb.EntityFramework;

namespace Paramore.Brighter.MongoDb.Tests.EntityFramework;

[Category("MongoDb")]
[Property("Feature", "EntityFramework.TransactionProvider")]
public class MongoDbEntityFrameworkTransactionProviderAsyncTest
{
    [Test]
    public async Task When_Committing_Async_Active_Transaction_Should_Call_CommitAsync()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.CommitAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.CommitAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => mockTransaction.CommitAsync(A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Committing_Async_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        await provider.CommitAsync(CancellationToken.None);
    }

    [Test]
    public async Task When_Rolling_Back_Async_Active_Transaction_Should_Call_RollbackAsync()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.RollbackAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Rolling_Back_Async_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        await provider.RollbackAsync(CancellationToken.None);
    }

    [Test]
    public async Task When_Rolling_Back_Async_With_Default_CancellationToken()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.RollbackAsync(); // Using default CancellationToken

        // Assert
        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Committing_Async_With_Specific_CancellationToken()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        var cts = new CancellationTokenSource();

        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.CommitAsync(cts.Token))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.CommitAsync(cts.Token);

        // Assert
        A.CallTo(() => mockTransaction.CommitAsync(cts.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Rolling_Back_Async_With_Specific_CancellationToken()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        var cts = new CancellationTokenSource();

        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.RollbackAsync(cts.Token))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.RollbackAsync(cts.Token);

        // Assert
        A.CallTo(() => mockTransaction.RollbackAsync(cts.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Committing_Async_With_Cancelled_Token_Should_Propagate()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.CommitAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.CommitAsync(cts.Token));
    }

    [Test]
    public async Task When_Rolling_Back_Async_With_Cancelled_Token_Should_Propagate()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        A.CallTo(() => mockTransaction.RollbackAsync(cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.RollbackAsync(cts.Token));
    }

    [Test]
    public async Task When_Multiple_Operations_In_Sequence_Should_Maintain_State()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        // Setup to return transaction on first call, then null after rollback
        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var hasTransactionBefore = provider.HasOpenTransaction;
        await provider.RollbackAsync();
        var hasTransactionAfter = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionBefore).IsTrue();
        await Assert.That(hasTransactionAfter).IsFalse();
    }

    [Test]
    public async Task When_Committing_Async_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        A.CallTo(() => mockTransaction.CommitAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await Assert.That(provider.HasOpenTransaction).IsTrue();
        await provider.CommitAsync(CancellationToken.None);
        var hasTransactionAfterCommit = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionAfterCommit).IsFalse();
    }

    [Test]
    public async Task When_Rolling_Back_Async_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await Assert.That(provider.HasOpenTransaction).IsTrue();
        await provider.RollbackAsync();
        var hasTransactionAfterRollback = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionAfterRollback).IsFalse();
    }

    [Test]
    public async Task When_Commit_Called_Async_Multiple_Times_Should_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        A.CallTo(() => mockTransaction.CommitAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await provider.CommitAsync(CancellationToken.None); // First commit
        await provider.CommitAsync(CancellationToken.None); // Second commit with null transaction - should not throw
    }

    [Test]
    public async Task When_Rollback_Called_Async_Multiple_Times_Should_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await provider.RollbackAsync(); // First rollback
        await provider.RollbackAsync(); // Second rollback with null transaction - should not throw
    }
}
