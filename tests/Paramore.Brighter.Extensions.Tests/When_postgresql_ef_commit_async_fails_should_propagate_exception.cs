using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.PostgreSql.EntityFrameworkCore;


namespace Paramore.Brighter.Extensions.Tests;

public class PostgreSqlEntityFrameworkTransactionProviderCommitAsyncTests
{
    [Test]
    public async Task When_postgresql_ef_commit_async_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.CommitAsync(A<CancellationToken>.Ignored))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        var provider = new PostgreSqlEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.That(() => provider.CommitAsync(CancellationToken.None)).ThrowsExactly<InvalidOperationException>();
    }
}
