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

using Paramore.Brighter.BoxProvisioning.Sqlite;
using System.Threading.Tasks;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

// Item Q-sqlite (spec 0027 PR #4039 third review). Pins the factory-entry wiring of
// Identifiers.AssertSafe at the SQLite *MigrationCatalog.All(...) entry: any unsafe table name must be
// rejected before the factory builds migration up-scripts and idempotency-check SQL. The
// injection vector sits in two places per migration: the bracket-quoted ALTER TABLE [{table}]
// and the single-quoted pragma_table_info('{table}') predicate at SqliteOutboxMigrationCatalog.cs:150
// (and the inbox mirror at :73). The helper guarantees no quote ever reaches either path.
//
// SQLite has no schema concept (single attached database), so only the table identifier is
// validated at the factory. ConfigurationException is the documented contract; no SQLite
// connection is required because the rejection happens before any DDL is rendered (connection
// string is "Data Source=ignored;").

public class SqliteMigrationsUnsafeIdentifierTests
{
    [Test]
    [Arguments("O'Brien")]    // single quote — exact injection vector at pragma_table_info('{table}')
    [Arguments("1Outbox")]    // leading digit — invalid as bare identifier
    [Arguments("my-outbox")]  // hyphen — would need bracket-quoting to be legal
    public async Task When_sqlite_outbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=ignored;",
            outBoxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => new SqliteOutboxMigrationCatalog().All(config));
        await Assert.That(ex.Message).Contains(unsafeTable);
    }

    [Test]
    [Arguments("O'Brien")]
    [Arguments("1Inbox")]
    [Arguments("my-inbox")]
    public async Task When_sqlite_inbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=ignored;",
            inboxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => new SqliteInboxMigrationCatalog().All(config));
        await Assert.That(ex.Message).Contains(unsafeTable);
    }
}