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
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["commandid", "commandtype", "commandbody", "timestamp", "contextkey"];

    [Fact]
    public void When_postgres_inbox_migrations_are_listed_it_should_return_only_v1_with_contextkey_and_composite_pk_as_baseline()
    {
        //Arrange — Postgres inbox is V1-only by design (born with ContextKey + composite PK
        //in PR #1401, Feb 2021). No pre-ContextKey Postgres inbox ever shipped (ADR
        //"Alternatives → E"); so V1's LogicalColumns include contextkey.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var expectedV1 = new HashSet<string>(s_v1Columns, StringComparer.Ordinal);

        //Act
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);

        //Assert — exactly one migration, version 1, no SourceReference (no archaeology pointer
        //for V1 baseline — same convention as outbox V1).
        Assert.Single(migrations);
        Assert.Equal(1, migrations[0].Version);
        Assert.Null(migrations[0].SourceReference);

        //Assert — V1 LogicalColumns are lowercase (ADR §1) and contextkey is part of V1.
        Assert.True(
            expectedV1.SetEquals(migrations[0].LogicalColumns),
            $"V1 LogicalColumns mismatch — expected: [{string.Join(", ", expectedV1)}], " +
            $"got: [{string.Join(", ", migrations[0].LogicalColumns)}]");

        //Assert — V1 UpScript is the live builder DDL with composite PRIMARY KEY (CommandId, ContextKey).
        var expectedUpScript = PostgreSqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        Assert.Equal(expectedUpScript, migrations[0].UpScript);
        Assert.Contains(
            "PRIMARY KEY",
            migrations[0].UpScript,
            StringComparison.OrdinalIgnoreCase);
    }
}
