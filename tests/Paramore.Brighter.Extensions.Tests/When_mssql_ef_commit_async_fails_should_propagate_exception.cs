using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MsSqlEntityFrameworkCoreTransactionProviderCommitAsyncTests
{
    [Fact]
    public async Task When_mssql_ef_commit_async_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.CommitAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        var provider = new MsSqlEntityFrameworkCoreTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CommitAsync(CancellationToken.None));
    }
}
