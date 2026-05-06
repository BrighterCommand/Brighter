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

using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

// Item Q-mysql (spec 0027 PR #4039 third review). Pins the factory-entry wiring of
// Identifiers.AssertSafe at the MySQL *Migrations.All(...) entry: any unsafe table name must be
// rejected before the factory builds migration up-scripts. The injection vector called out in the
// reviewer comment sits at MySqlOutboxMigrations.cs:165 where a single quote in tableName breaks
// the inlined information_schema.columns predicate ('{table}'); the helper guarantees no quote
// ever reaches that interpolation path.
//
// MySQL has no schema concept — V2+ ALTER scripts run against the runtime DATABASE() function,
// not a configured schema name — so only the table identifier is validated at the factory.
// ConfigurationException is the documented contract; no MySQL connection is required because the
// rejection happens before any DDL is rendered against a live server (connection string is
// "Server=ignored;Database=ignored;").

public class MySqlMigrationsUnsafeIdentifierTests
{
    [Theory]
    [InlineData("O'Brien")]    // single quote — exact injection vector at MySqlOutboxMigrations.cs:165
    [InlineData("1Outbox")]    // leading digit — invalid as bare identifier
    [InlineData("my-outbox")]  // hyphen — would need backtick-quoting to be legal
    public void When_mysql_outbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => MySqlOutboxMigrations.All(config));
        Assert.Contains(unsafeTable, ex.Message);
    }

    [Theory]
    [InlineData("O'Brien")]
    [InlineData("1Inbox")]
    [InlineData("my-inbox")]
    public void When_mysql_inbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => MySqlInboxMigrations.All(config));
        Assert.Contains(unsafeTable, ex.Message);
    }
}
