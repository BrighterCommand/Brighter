using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning.Tests.Drift;

public class DdlColumnExtractorTests
{
    [Test]
    public async Task When_mssql_ddl_with_constraint_is_parsed_should_return_six_logical_columns()
    {
        //Arrange — six bracketed columns plus a CONSTRAINT line that must be filtered out.
        //Schema-qualified table name ([dbo].[outbox_test]) must not contribute a spurious column.
        const string ddl = @"
CREATE TABLE [dbo].[outbox_test] (
    [MessageId] NVARCHAR(255) NOT NULL,
    [Topic] NVARCHAR(255) NULL,
    [MessageType] NVARCHAR(32) NULL,
    [Timestamp] DATETIME2(7) NOT NULL,
    [HeaderBag] NVARCHAR(MAX) NULL,
    [Body] NVARCHAR(MAX) NULL,
    CONSTRAINT PK_outbox_test PRIMARY KEY ([MessageId])
)";
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
        };

        //Act
        var columns = DdlColumnExtractor.GetExpectedColumns(ddl, QuoteStyle.MsSql);

        //Assert
        await Assert.That(columns.Count).IsEqualTo(6);
        await Assert.That(expected.SetEquals(columns)).IsTrue().Because($"Expected: [{string.Join(", ", expected)}], got: [{string.Join(", ", columns)}]");
    }

    [Test]
    public async Task When_postgres_ddl_with_unquoted_identifiers_is_parsed_should_return_six_logical_columns()
    {
        //Arrange — Postgres convention is unquoted PascalCase identifiers; the engine folds them
        //to lowercase but the DDL source text preserves the original case. The extractor reads
        //the leading identifier on each column-declaration line regardless of quoting.
        const string ddl = @"
CREATE TABLE outbox_test (
    MessageId character varying(255) UNIQUE NOT NULL,
    Topic character varying(255) NULL,
    MessageType character varying(32) NULL,
    Timestamp timestamp NOT NULL,
    HeaderBag text NULL,
    Body text NULL,
    PRIMARY KEY (MessageId)
)";
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
        };

        //Act
        var columns = DdlColumnExtractor.GetExpectedColumns(ddl, QuoteStyle.Postgres);

        //Assert
        await Assert.That(columns.Count).IsEqualTo(6);
        await Assert.That(expected.SetEquals(columns)).IsTrue().Because($"Expected: [{string.Join(", ", expected)}], got: [{string.Join(", ", columns)}]");
    }

    [Test]
    public async Task When_mysql_ddl_with_table_level_primary_key_is_parsed_should_ignore_pk_line()
    {
        //Arrange — MySQL backticked identifiers; the trailing PRIMARY KEY (`col`) is a
        //table-level constraint that must not contribute a column name.
        const string ddl = @"
CREATE TABLE `outbox_test` (
    `MessageId` VARCHAR(255) NOT NULL,
    `Topic` VARCHAR(255) NULL,
    `MessageType` VARCHAR(32) NULL,
    `Timestamp` TIMESTAMP NOT NULL,
    `HeaderBag` TEXT NULL,
    `Body` TEXT NULL,
    PRIMARY KEY (`MessageId`)
)";
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
        };

        //Act
        var columns = DdlColumnExtractor.GetExpectedColumns(ddl, QuoteStyle.MySql);

        //Assert
        await Assert.That(columns.Count).IsEqualTo(6);
        await Assert.That(expected.SetEquals(columns)).IsTrue().Because($"Expected: [{string.Join(", ", expected)}], got: [{string.Join(", ", columns)}]");
    }

    [Test]
    public async Task When_sqlite_ddl_with_collate_nocase_is_parsed_should_strip_collate_clause()
    {
        //Arrange — SQLite frequently emits `COLLATE NOCASE` after the type specifier; the
        //extractor must read only the leading identifier and discard the trailing modifiers
        //(no truncation, no "MessageId COLLATE NOCASE" combinations).
        const string ddl = @"
CREATE TABLE [outbox_test] (
    [MessageId] TEXT NOT NULL COLLATE NOCASE,
    [Topic] TEXT NULL,
    [MessageType] TEXT NULL,
    [Timestamp] TEXT NOT NULL,
    [HeaderBag] TEXT NULL,
    [Body] TEXT NULL
)";
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MessageId", "Topic", "MessageType", "Timestamp", "HeaderBag", "Body"
        };

        //Act
        var columns = DdlColumnExtractor.GetExpectedColumns(ddl, QuoteStyle.Sqlite);

        //Assert
        await Assert.That(columns.Count).IsEqualTo(6);
        await Assert.That(expected.SetEquals(columns)).IsTrue().Because($"Expected: [{string.Join(", ", expected)}], got: [{string.Join(", ", columns)}]");
    }
}