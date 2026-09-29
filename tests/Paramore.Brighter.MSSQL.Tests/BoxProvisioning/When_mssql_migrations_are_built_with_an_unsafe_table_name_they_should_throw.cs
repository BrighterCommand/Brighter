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

using Paramore.Brighter.BoxProvisioning.MsSql;
using System.Threading.Tasks;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The MSSQL catalogs' <c>All</c> rejects an unsafe table or schema name with a
/// <see cref="ConfigurationException"/> before building any up-script that would interpolate it
/// (ADR 0058, Guards and contracts). No connection is opened.
/// </summary>
public class MsSqlMigrationsUnsafeIdentifierTests
{
    [Test]
    [Arguments("O'Brien")]    // single quote — would break inlined string predicates
    [Arguments("1Outbox")]    // leading digit — invalid as bare identifier
    [Arguments("my-outbox")]  // hyphen — would need bracket-quoting to be legal
    public async Task When_mssql_outbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: unsafeTable);

        //Act + Assert
        var ex = await Assert.That(() => new MsSqlOutboxMigrationCatalog().All(config)).ThrowsExactly<ConfigurationException>();
        await Assert.That(ex.Message).Contains(unsafeTable);
    }

    [Test]
    [Arguments("O'Brien")]
    [Arguments("1Inbox")]
    [Arguments("my-inbox")]
    public async Task When_mssql_inbox_migrations_are_built_with_an_unsafe_table_name_it_should_throw(string unsafeTable)
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            inboxTableName: unsafeTable);

        //Act + Assert
        var ex = await Assert.That(() => new MsSqlInboxMigrationCatalog().All(config)).ThrowsExactly<ConfigurationException>();
        await Assert.That(ex.Message).Contains(unsafeTable);
    }

    [Test]
    public async Task When_mssql_outbox_migrations_are_built_with_an_unsafe_schema_name_it_should_throw()
    {
        //Arrange
        var config = new RelationalDatabaseConfiguration(
            "Server=ignored;Database=ignored;",
            outBoxTableName: "Outbox",
            schemaName: "bad-schema");

        //Act + Assert
        var ex = await Assert.That(() => new MsSqlOutboxMigrationCatalog().All(config)).ThrowsExactly<ConfigurationException>();
        await Assert.That(ex.Message).Contains("bad-schema");
    }
}
