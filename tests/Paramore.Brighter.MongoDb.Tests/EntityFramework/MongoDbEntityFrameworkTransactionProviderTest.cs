using System;
using System.Data.Common;
using System.Threading;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MongoDB.Driver;
using Paramore.Brighter.MongoDb.EntityFramework;

namespace Paramore.Brighter.MongoDb.Tests.EntityFramework;

[Category("MongoDb")]
[Property("Feature", "EntityFramework.TransactionProvider")]
public class MongoDbEntityFrameworkTransactionProviderTest
{
    [Test]
    public async Task When_Creating_Provider_With_DbContext_Should_Initialize_Correctly()
    {
        // Arrange
        var context = A.Fake<DbContext>();

        // Act
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Assert
        await Assert.That(provider).IsNotNull();
    }

    [Test]
    public async Task When_Getting_IsSharedConnection_Should_Return_True()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var isShared = provider.IsSharedConnection;

        // Assert
        await Assert.That(isShared).IsTrue();
    }

    [Test]
    public async Task When_HasOpenTransaction_Initially_Should_Return_False()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var hasOpenTransaction = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasOpenTransaction).IsFalse();
    }

    [Test]
    public async Task When_HasOpenTransaction_With_Active_Transaction_Should_Return_True()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var hasOpenTransaction = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasOpenTransaction).IsTrue();
    }

    [Test]
    public async Task When_Getting_Transaction_With_Non_MongoTransaction_Throws()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var nonMongoTransaction = A.Fake<IDbContextTransaction>();

        // Don't setup as MongoTransaction - just a regular mock
        A.CallTo(() => context.Database.CurrentTransaction).Returns(nonMongoTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        var ex = await Assert.That(() => provider.GetTransaction()).ThrowsExactly<InvalidOperationException>();
        await Assert.That(ex.Message).Contains("not a MongoTransaction");
    }

    [Test]
    public async Task When_Committing_Active_Transaction_Should_Call_Commit()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        provider.Commit();

        // Assert
        A.CallTo(() => mockTransaction.Commit()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Committing_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        provider.Commit();
    }

    [Test]
    public async Task When_Rolling_Back_Active_Transaction_Should_Call_Rollback()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await provider.RollbackAsync();

        // Assert
        A.CallTo(() => mockTransaction.RollbackAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Rolling_Back_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        await provider.RollbackAsync();
    }

    [Test]
    public async Task When_Closing_Active_Transaction_Should_Call_Dispose()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        provider.Close();

        // Assert
        A.CallTo(() => mockTransaction.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task When_Closing_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        provider.Close();
    }

    [Test]
    public async Task When_Committing_And_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        // First call returns transaction, second returns null after commit
        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await Assert.That(provider.HasOpenTransaction).IsTrue();
        provider.Commit();
        var hasTransactionAfterCommit = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionAfterCommit).IsFalse();
    }

    [Test]
    public async Task When_Rolling_Back_And_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await Assert.That(provider.HasOpenTransaction).IsTrue();
        await provider.RollbackAsync();
        var hasTransactionAfterRollback = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionAfterRollback).IsFalse();
    }

    [Test]
    public async Task When_Closing_And_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        await Assert.That(provider.HasOpenTransaction).IsTrue();
        provider.Close();
        var hasTransactionAfterClose = provider.HasOpenTransaction;

        // Assert
        await Assert.That(hasTransactionAfterClose).IsFalse();
    }

    [Test]
    public async Task When_Commit_Called_Multiple_Times_Should_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        // First call returns transaction, subsequent calls return null
        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        provider.Commit(); // First commit
        provider.Commit(); // Second commit with null transaction - should not throw
    }

    [Test]
    public async Task When_Rollback_Called_Multiple_Times_Should_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await provider.RollbackAsync(); // First rollback
        await provider.RollbackAsync(); // Second rollback with null transaction - should not throw
    }
}
