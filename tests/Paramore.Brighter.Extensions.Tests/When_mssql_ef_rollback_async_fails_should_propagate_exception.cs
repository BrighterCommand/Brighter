using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.MsSql.EntityFrameworkCore;


namespace Paramore.Brighter.Extensions.Tests;

public class MsSqlEntityFrameworkCoreTransactionProviderRollbackAsyncTests
{
    [Test]
    public async Task When_mssql_ef_rollback_async_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.RollbackAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        var provider = new MsSqlEntityFrameworkCoreTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.That(() => provider.RollbackAsync(CancellationToken.None)).ThrowsExactly<InvalidOperationException>();
    }
}
