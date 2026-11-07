#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System.Collections.Generic;
using System.Data.Common;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

/// <summary>
/// Verifies DataRef round trips and legacy table compatibility using MySql.
/// </summary>
public class MySqlOutboxDataRefTests : RelationalOutboxDataRefTests
{
    /// <inheritdoc />
    protected override string ConnectionString => Const.DefaultConnectingString;
    /// <inheritdoc />
    protected override string AddDataRefSql => $"ALTER TABLE {TableName} ADD DataRef VARCHAR(255) NULL";
    /// <inheritdoc />
    protected override IEnumerable<string> RemoveCausationSql =>
    [
        $"DROP INDEX idx_CausationId ON {TableName}",
        $"ALTER TABLE {TableName} DROP COLUMN CausationId"
    ];

    /// <inheritdoc />
    protected override DbConnection CreateConnection() => new MySqlConnection(ConnectionString);
    /// <inheritdoc />
    protected override string CreateTableSql(bool binary) => MySqlOutboxBuilder.GetDDL(TableName, binary);
    /// <inheritdoc />
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration) =>
        new MySqlOutbox(configuration);
}
