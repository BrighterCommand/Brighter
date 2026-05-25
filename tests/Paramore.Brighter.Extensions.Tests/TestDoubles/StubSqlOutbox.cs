using System;
using System.Data;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public partial class TestBrighterExtension
{
    public class StubSqlOutbox : RelationDatabaseOutbox
    {
        public StubSqlOutbox(DbSystem dbSystem,
            IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider,
            IRelationDatabaseOutboxQueries queries,
            ILogger logger,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
            : base(dbSystem, configuration, connectionProvider, queries, logger, instrumentationOptions)
        {
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
        {
            throw new NotImplementedException();
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
        {
            throw new NotImplementedException();
        }

        protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
        {
            throw new NotImplementedException();
        }
    }
}