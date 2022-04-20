namespace Paramore.Brighter.PostgreSql
{
    public interface IPostgreSqlTransactionConnectionProvider : IPostgreSqlConnectionProvider, IAmABoxTransactionConnectionProvider { }
}
