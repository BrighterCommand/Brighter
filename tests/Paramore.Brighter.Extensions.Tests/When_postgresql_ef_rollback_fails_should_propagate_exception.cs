using System;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.PostgreSql.EntityFrameworkCore;


namespace Paramore.Brighter.Extensions.Tests;

public class PostgreSqlEntityFrameworkTransactionProviderRollbackTests
{
    [Test]
    public async System.Threading.Tasks.Task When_postgresql_ef_rollback_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.Rollback())
            .Throws(new InvalidOperationException("rollback failed"));

        var provider = new PostgreSqlEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.That(() => provider.Rollback()).ThrowsExactly<InvalidOperationException>();
    }
}
