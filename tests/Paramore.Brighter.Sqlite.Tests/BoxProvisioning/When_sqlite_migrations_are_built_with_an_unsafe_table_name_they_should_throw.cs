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
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning;

/// <summary>
/// The SQLite catalogs reject an unsafe table name with a ConfigurationException before rendering any DDL,
/// so no quote reaches <c>ALTER TABLE [{table}]</c> or <c>pragma_table_info('{table}')</c>.
/// </summary>
public class SqliteMigrationsUnsafeIdentifierTests
{
    [Theory]
    [InlineData("O'Brien")]    // single quote
    [InlineData("1Outbox")]    // leading digit — invalid as bare identifier
    [InlineData("my-outbox")]  // hyphen — would need bracket-quoting to be legal
    public void When_sqlite_outbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=ignored;",
            outBoxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => new SqliteOutboxMigrationCatalog().All(config));
        Assert.Contains(unsafeTable, ex.Message);
    }

    [Theory]
    [InlineData("O'Brien")]
    [InlineData("1Inbox")]
    [InlineData("my-inbox")]
    public void When_sqlite_inbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Data Source=ignored;",
            inboxTableName: unsafeTable);

        //Act + Assert
        var ex = Assert.Throws<ConfigurationException>(() => new SqliteInboxMigrationCatalog().All(config));
        Assert.Contains(unsafeTable, ex.Message);
    }
}
