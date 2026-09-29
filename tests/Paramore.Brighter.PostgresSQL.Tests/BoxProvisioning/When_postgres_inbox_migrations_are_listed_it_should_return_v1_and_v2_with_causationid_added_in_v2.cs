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

public class PostgreSqlInboxMigrationsTests
{
    private static readonly string[] s_v1Columns =
        ["commandid", "commandtype", "commandbody", "timestamp", "contextkey"];

    private static readonly string[] s_v2Columns =
        ["commandid", "commandtype", "commandbody", "timestamp", "contextkey", "causationid"];

    [Test]
    public async System.Threading.Tasks.Task When_postgres_inbox_migrations_are_listed_it_should_return_v1_and_v2_with_causationid_added_in_v2()
    {
        //Arrange — Postgres inbox was born with ContextKey + composite PK in PR #1401 (Feb 2021),
        //so its V1 baseline already includes contextkey (no pre-ContextKey Postgres inbox ever
        //shipped — ADR "Alternatives → E"). Spec 0027 (#2541) adds a V2 that introduces
        //causationid via ADD COLUMN IF NOT EXISTS — so the catalog now lists V1 + V2.
        const string tableName = "inbox_test";
        var config = new RelationalDatabaseConfiguration(
            "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            inboxTableName: tableName);

        var expectedV1 = new HashSet<string>(s_v1Columns, StringComparer.Ordinal);
        var expectedV2 = new HashSet<string>(s_v2Columns, StringComparer.Ordinal);

        //Act
        var migrations = new PostgreSqlInboxMigrationCatalog().All(config);

        //Assert — exactly two migrations numbered 1..2 in order.
        await Assert.That(migrations.Count).IsEqualTo(2);
        await Assert.That(migrations[0].Version.Value).IsEqualTo(1);
        await Assert.That(migrations[1].Version.Value).IsEqualTo(2);

        //Assert — V1 has no SourceReference (baseline); V2 carries the issue pointer (#2541).
        await Assert.That(migrations[0].SourceReference).IsNull();
        await Assert.That(migrations[1].SourceReference).IsEqualTo("#2541");

        //Assert — V1 LogicalColumns are lowercase (ADR §1) and contextkey is part of V1.
        await Assert.That(expectedV1.SetEquals(migrations[0].LogicalColumns)).IsTrue();

        //Assert — V2 LogicalColumns are V1 + causationid (lowercase per ADR §1).
        await Assert.That(expectedV2.SetEquals(migrations[1].LogicalColumns)).IsTrue().Because($"V2 LogicalColumns mismatch — expected: [{string.Join(", ", expectedV2)}], " +
            $"got: [{string.Join(", ", migrations[1].LogicalColumns)}]");

        //Assert — V2 UpScript is an idempotent ADD COLUMN IF NOT EXISTS for causationid.
        await Assert.That(migrations[1].UpScript.Value).Contains("ADD COLUMN IF NOT EXISTS");
        await Assert.That(migrations[1].UpScript.Value).Contains("causationid");

        //Assert — V1 UpScript is the literal historical Postgres inbox DDL (Spec 0027 R1),
        //not the live builder DDL. The fresh-install fast path takes its DDL from
        //FreshInstallDdl now; V1.UpScript.Value carries the honest first-shipped baseline. The
        //composite PRIMARY KEY (CommandId, ContextKey) that PR #2560 (Nov 2023) shipped is
        //asserted structurally — the historical literal is enclosed in the catalog and
        //should not be duplicated here.
        await Assert.That(migrations[0].UpScript.Value).Contains("CommandId");
        await Assert.That(migrations[0].UpScript.Value).Contains("ContextKey");
        await Assert.That(migrations[0].UpScript.Value).Contains("PRIMARY KEY (CommandId, ContextKey)");

        //Assert — FreshInstallDdl now sources the live builder DDL (Spec 0027 R1 part 1
        //added this hook). This is the post-Part-1 contract: V1.UpScript.Value is historical;
        //FreshInstallDdl is current.
        var expectedFreshInstallDdl =
            PostgreSqlInboxBuilder.GetDDL(tableName, config.BinaryMessagePayload);
        await Assert.That(new PostgreSqlInboxMigrationCatalog().FreshInstallDdl(config)).IsEqualTo(expectedFreshInstallDdl);
    }
}
