#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Data;
using System.Data.Common;
using MySqlConnector;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store  
    /// </summary>
    public class MySqlOutbox : RelationDatabaseOutbox
    {
        private const int MySqlDuplicateKeyError = 1062;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider)
            : base(DbSystem.MySql, configuration, connectionProvider, 
                new MySqlQueries(), ApplicationLogging.CreateLogger<MySqlOutbox>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration)
            : this(configuration, new MySqlConnectionProvider(configuration))
        {
        }

        /// <inheritdoc />
        protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
        }

        /// <inheritdoc />
        protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value, DbType = dbType };
        }

        /// <inheritdoc />
        protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
        {
            return ex is MySqlException { Number: MySqlDuplicateKeyError };
        }
        
        /// <inheritdoc />
        protected override DateTimeOffset GetTimeStamp(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TimestampColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return DateTimeOffset.MinValue;
            }

            var reader = (MySqlDataReader)dr;
            var dataTime = reader.GetDateTimeOffset(ordinal);
            return dataTime; 
        }
    }
}
