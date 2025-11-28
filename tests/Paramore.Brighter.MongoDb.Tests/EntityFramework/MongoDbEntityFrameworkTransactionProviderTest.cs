using System;
using System.Data.Common;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MongoDB.Driver;
using Paramore.Brighter.MongoDb.EntityFramework;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.EntityFramework;

[Trait("Category", "MongoDb")]
[Trait("Feature", "EntityFramework.TransactionProvider")]
public class MongoDbEntityFrameworkTransactionProviderTest
{
    [Fact]
    public void When_Creating_Provider_With_DbContext_Should_Initialize_Correctly()
    {
        // Arrange
        var context = A.Fake<DbContext>();

        // Act
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void When_Getting_IsSharedConnection_Should_Return_True()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var isShared = provider.IsSharedConnection;

        // Assert
        Assert.True(isShared);
    }

    [Fact]
    public void When_HasOpenTransaction_Initially_Should_Return_False()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var hasOpenTransaction = provider.HasOpenTransaction;

        // Assert
        Assert.False(hasOpenTransaction);
    }

    [Fact]
    public void When_HasOpenTransaction_With_Active_Transaction_Should_Return_True()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);
        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        var hasOpenTransaction = provider.HasOpenTransaction;

        // Assert
        Assert.True(hasOpenTransaction);
    }

    [Fact]
    public void When_Getting_Transaction_With_Non_MongoTransaction_Throws()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var nonMongoTransaction = A.Fake<IDbContextTransaction>();

        // Don't setup as MongoTransaction - just a regular mock
        A.CallTo(() => context.Database.CurrentTransaction).Returns(nonMongoTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetTransaction());
        Assert.Contains("not a MongoTransaction", ex.Message);
    }

    [Fact]
    public void When_Committing_Active_Transaction_Should_Call_Commit()
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

    [Fact]
    public void When_Committing_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        provider.Commit();
    }

    [Fact]
    public void When_Rolling_Back_Active_Transaction_Should_Call_Rollback()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(mockTransaction);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        provider.Rollback();

        // Assert
        A.CallTo(() => mockTransaction.Rollback()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void When_Rolling_Back_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        provider.Rollback();
    }

    [Fact]
    public void When_Closing_Active_Transaction_Should_Call_Dispose()
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

    [Fact]
    public void When_Closing_Null_Transaction_Does_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert - Should not throw
        provider.Close();
    }

    [Fact]
    public void When_Committing_And_Then_Checking_HasOpenTransaction()
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
        Assert.True(provider.HasOpenTransaction);
        provider.Commit();
        var hasTransactionAfterCommit = provider.HasOpenTransaction;

        // Assert
        Assert.False(hasTransactionAfterCommit);
    }

    [Fact]
    public void When_Rolling_Back_And_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        Assert.True(provider.HasOpenTransaction);
        provider.Rollback();
        var hasTransactionAfterRollback = provider.HasOpenTransaction;

        // Assert
        Assert.False(hasTransactionAfterRollback);
    }

    [Fact]
    public void When_Closing_And_Then_Checking_HasOpenTransaction()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act
        Assert.True(provider.HasOpenTransaction);
        provider.Close();
        var hasTransactionAfterClose = provider.HasOpenTransaction;

        // Assert
        Assert.False(hasTransactionAfterClose);
    }

    [Fact]
    public void When_Commit_Called_Multiple_Times_Should_Not_Throw()
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

    [Fact]
    public void When_Rollback_Called_Multiple_Times_Should_Not_Throw()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var mockTransaction = A.Fake<IDbContextTransaction>();

        var callCount = 0;
        A.CallTo(() => context.Database.CurrentTransaction)
            .ReturnsLazily(_ => callCount++ == 0 ? mockTransaction : null);

        var provider = new MongoDbEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        provider.Rollback(); // First rollback
        provider.Rollback(); // Second rollback with null transaction - should not throw
    }
}
