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

using MySqlConnector;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning.Legacy;

// Creates a MySQL inbox with hand-written legacy DDL — V1 before ContextKey (787c31c52), V2 before
// CausationId — so bootstrap tests meet the real legacy schema. It writes no history rows.
internal static class MySqlInboxLegacySeeder
{
    private const string V1Ddl = """
        CREATE TABLE `{0}`
        (
            `CommandId` VARCHAR(255) NOT NULL,
            `CommandType` VARCHAR(256) NOT NULL,
            `CommandBody` TEXT NOT NULL,
            `Timestamp` TIMESTAMP(4) NOT NULL,
            PRIMARY KEY (`CommandId`)
        ) ENGINE = InnoDB;
        """;

    private const string V2Ddl = """
        CREATE TABLE `{0}`
        (
            `CommandId` VARCHAR(255) NOT NULL,
            `CommandType` VARCHAR(256) NOT NULL,
            `CommandBody` TEXT NOT NULL,
            `Timestamp` TIMESTAMP(4) NOT NULL,
            `ContextKey` VARCHAR(256) NULL,
            PRIMARY KEY (`CommandId`)
        ) ENGINE = InnoDB;
        """;

    public static void SeedAtV1(string connectionString, string tableName)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V1Ddl, tableName);
        command.ExecuteNonQuery();
    }

    public static void SeedAtV2(string connectionString, string tableName)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(V2Ddl, tableName);
        command.ExecuteNonQuery();
    }
}
