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
using Xunit;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Outbox;

/// <summary>
/// Verifies DataRef round trips and legacy table compatibility using Spanner.
/// </summary>
[Trait("Category", "Spanner")]
[Collection("Spanner")]
public class SpannerOutboxDataRefTests : RelationalOutboxDataRefTests
{
    /// <inheritdoc />
    protected override string ConnectionString => Spanner.Const.ConnectionString;
    /// <inheritdoc />
    protected override string AddDataRefSql => $"ALTER TABLE {TableName} ADD COLUMN DataRef STRING(255)";
    /// <inheritdoc />
    protected override IEnumerable<string> RemoveCausationSql =>
    [
        $"ALTER TABLE {TableName} DROP COLUMN CausationId"
    ];

    /// <inheritdoc />
    protected override DbConnection CreateConnection() => new SpannerConnection(ConnectionString);
    /// <inheritdoc />
    protected override string CreateTableSql(bool binary) => SpannerOutboxBuilder.GetDDL(TableName, binary);
    /// <inheritdoc />
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration) =>
        new SpannerOutbox(configuration);
}
