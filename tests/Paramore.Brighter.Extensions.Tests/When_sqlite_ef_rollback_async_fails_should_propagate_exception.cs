using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class SqliteEntityFrameworkTransactionProviderRollbackAsyncTests
{
    [Fact]
    public async Task When_sqlite_ef_rollback_async_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.RollbackAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        var provider = new SqliteEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RollbackAsync(CancellationToken.None));
    }
}
