#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Inbox.Postgres;
using System.Threading.Tasks;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// The Postgres inbox catalog lists V1, which already carries contextkey and the composite primary key, and V2, which adds
/// causationid with ADD COLUMN IF NOT EXISTS. V1.UpScript is the historical DDL; FreshInstallDdl is the live builder's.
/// </summary>
public class PostgreSqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["commandid", "commandtype", "commandbody", "timestamp", "contextkey"];

    private static readonly string[] s_v2Columns =
        ["commandid", "commandtype", "commandbody", "timestamp", "contextkey", "causationid"];

    [Test]
    public async System.Threading.Tasks.Task When_postgres_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_causationid_added_in_v2()
    {
        //Arrange
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var expectedV1 = new HashSet<string>(s_v1Columns, StringComparer.Ordinal);
        var expectedV2 = new HashSet<string>(s_v2Columns, StringComparer.Ordinal);

        //Act
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);

        //Assert
        await Assert.That(migrations.Count).IsEqualTo(2);
        await Assert.That(migrations[0].Version.Value).IsEqualTo(1);
        await Assert.That(migrations[1].Version.Value).IsEqualTo(2);

        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(migrations[1].SourceReference).IsEqualTo("#2541");

        await Assert.That(expectedV1.SetEquals(migrations[0].LogicalColumns)).IsTrue();

        await Assert.That(expectedV2.SetEquals(migrations[1].LogicalColumns)).IsTrue();

        await Assert.That((migrations[1].UpScript).Value).Contains("ADD COLUMN IF NOT EXISTS");
        await Assert.That((migrations[1].UpScript).Value).Contains("causationid");

        await Assert.That((migrations[0].UpScript).Value).Contains("CommandId");
        await Assert.That((migrations[0].UpScript).Value).Contains("ContextKey");
        await Assert.That((migrations[0].UpScript).Value).Contains("PRIMARY KEY (CommandId, ContextKey)");

        var expectedFreshInstallDdl =
            PostgreSqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        await Assert.That(new PostgreSqlInboxMigrationCatalog().FreshInstallDdl(config)).IsEqualTo(expectedFreshInstallDdl);
    }
}
