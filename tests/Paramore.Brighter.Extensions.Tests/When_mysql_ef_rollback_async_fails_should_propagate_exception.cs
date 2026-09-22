#if NET9_0
using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.MySql.EntityFrameworkCore;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MySqlEntityFrameworkTransactionProviderRollbackAsyncTests
{
    [Fact]
    public async Task When_mysql_ef_rollback_async_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.RollbackAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        var provider = new MySqlEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.RollbackAsync(CancellationToken.None));
    }
}
#endif
