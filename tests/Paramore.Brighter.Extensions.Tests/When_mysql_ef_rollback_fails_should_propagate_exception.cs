#if NET9_0
using System;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paramore.Brighter.MySql.EntityFrameworkCore;


namespace Paramore.Brighter.Extensions.Tests;

public class MySqlEntityFrameworkTransactionProviderRollbackTests
{
    [Test]
    public async System.Threading.Tasks.Task When_mysql_ef_rollback_fails_should_propagate_exception()
    {
        // Arrange
        var context = A.Fake<DbContext>();
        var transaction = A.Fake<IDbContextTransaction>();
        A.CallTo(() => context.Database.CurrentTransaction).Returns(transaction);
        A.CallTo(() => transaction.Rollback())
            .Throws(new InvalidOperationException("rollback failed"));

        var provider = new MySqlEntityFrameworkTransactionProvider<DbContext>(context);

        // Act & Assert
        await Assert.That(() => provider.Rollback()).ThrowsExactly<InvalidOperationException>();
    }
}
#endif
