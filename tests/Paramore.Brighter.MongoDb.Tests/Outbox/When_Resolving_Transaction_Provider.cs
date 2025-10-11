using System;
using MongoDB.Driver;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbValidateTransactionProvider
{
    [Fact]
    public void When_Resolving_Transaction_Provider()
    {
        Type transactionProviderInterface = typeof(IAmABoxTransactionProvider<>);
        Type? transactionType = null;
        foreach (Type i in typeof(MongoDbUnitOfWork).GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == transactionProviderInterface)
            {
                transactionType = i.GetGenericArguments()[0];
            }
        }
        
        Assert.NotNull(transactionType);
        Assert.Equal(typeof(IClientSessionHandle) , transactionType);
    }
}
