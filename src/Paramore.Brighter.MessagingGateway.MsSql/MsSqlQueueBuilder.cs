#region Licence
/* The MIT License (MIT)
Copyright © 2025 Rafael Andrade

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.MessagingGateway.MsSql;

/// <summary>
/// Provide SQL statement helpers for creation of a Queue table in MS SQL Server
/// </summary>
public class MsSqlQueueBuilder
{
    private const string QUEUE_TABLE_DDL = 
        """
        CREATE TABLE [{0}] 
        (
            [Id] [BIGINT] IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [Topic] [NVARCHAR](255) NOT NULL,
            [MessageType] [NVARCHAR](1024) NOT NULL,
            [Payload] [NVARCHAR](MAX) NOT NULL
        )
        """;

    private const string QUEUE_TABLE_INDEX_DDL =
        """
        CREATE NONCLUSTERED INDEX [IX_{0}_Topic] ON [{0}] ([Topic] ASC)
        """;
    
    private const string QUEUE_EXISTS_SQL = 
        """
        IF EXISTS (
                    SELECT 1
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE t.name = '{0}' AND s.name = '{1}'
                ) SELECT 1 AS TableExists; ELSE SELECT 0 AS TableExists;
        """;
    
    /// <summary>
    /// Get the DDL statements to create a Queue table in MS SQL Server
    /// </summary>
    /// <param name="queueTableName">The name you want to use for the queue table</param>
    /// <returns>The required DDL as a <see cref="string"/></returns>
    public static string GetDDL(string queueTableName)
    {
        return string.Format(QUEUE_TABLE_DDL, queueTableName);
    }
    
    /// <summary>
    /// Get the DDL statement to create an index on the Topic column for the Queue table
    /// </summary>
    /// <param name="queueTableName">The name of the queue table to create the index for</param>
    /// <returns>The required DDL as a <see cref="string"/></returns>
    public static string GetIndexDDL(string queueTableName)
    {
        return string.Format(QUEUE_TABLE_INDEX_DDL, queueTableName);
    }

    /// <summary>
    /// Get the SQL statements required to test for the existence of a Queue table in MS SQL Server
    /// </summary>
    /// <param name="queueTableName">The name that was used for the Queue table</param>
    /// <param name="schemaName">The schema name for the Queue table. Defaults to 'dbo'</param>
    /// <returns>The required SQL as a <see cref="string"/></returns>
    public static string GetExistsQuery(string queueTableName, string schemaName = "dbo") =>
        string.Format(QUEUE_EXISTS_SQL, queueTableName, schemaName);
}
