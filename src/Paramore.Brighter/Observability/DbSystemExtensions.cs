#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Provide a helper method to turn span attributes into strings (lowercase)
/// </summary>
public static class DbSystemExtensions
{
    ///<summary>
    /// Provide a string representation of the database system
    /// </summary>
    public static string ToDbName(this DbSystem dbSystem) => dbSystem switch
    {
        DbSystem.AdaBas=> "adabas",
        DbSystem.Brighter=> "brighter",
        DbSystem.Cache=> "cache",
        DbSystem.Cassandra=> "cassandra",
        DbSystem.Clickhouse=> "clickhouse",
        DbSystem.Cloudscape=> "cloudscape",
        DbSystem.Cockroachdb=> "cockroachdb",
        DbSystem.Coldfusion=> "coldfusion",
        DbSystem.Cosmosdb=> "cosmosdb",
        DbSystem.Couchbase=> "couchbase",
        DbSystem.Couchdb=> "couchdb",
        DbSystem.Db2=> "db2",
        DbSystem.Derby=> "derby",
        DbSystem.Dynamodb=> "dynamodb",
        DbSystem.Edb=> "edb",
        DbSystem.Elasticsearch=> "elasticsearch",
        DbSystem.FileMaker=> "filemaker",
        DbSystem.Firebird=> "firebird",
        DbSystem.Firestore=> "firestore",
        DbSystem.FirstSql=> "firstsql",
        DbSystem.Geode=> "geode",
        DbSystem.H2=> "h2",
        DbSystem.HanaDb=> "hanadb",
        DbSystem.Hbase=> "hbase",
        DbSystem.Hive=> "hive",
        DbSystem.Hsqldb=> "hsqldb",
        DbSystem.Informix=> "informix",
        DbSystem.Ingres=> "ingres",
        DbSystem.InstantDb=> "instantdb",
        DbSystem.Interbase=> "interbase",
        DbSystem.Mariadb=> "mariadb",
        DbSystem.MaxDb=> "maxdb",
        DbSystem.Memcached=> "memcached",
        DbSystem.Mongodb=> "mongodb",
        DbSystem.MsSql=> "mssql",
        DbSystem.MsSqlCompact=> "mssqlcompact",
        DbSystem.MySql=> "mysql",
        DbSystem.Neo4J=> "neo4j",
        DbSystem.Netezza=> "netezza",
        DbSystem.OpenSearch=> "opensearch",
        DbSystem.Oracle=> "oracle",
        DbSystem.OtherSql=> "other_sql",
        DbSystem.Pervasive=> "pervasive",
        DbSystem.PointBase=> "pointbase",
        DbSystem.Postgresql=> "postgresql",
        DbSystem.Progress=> "progress",
        DbSystem.Redis=> "redis",
        DbSystem.Redshift=> "redshift",
        DbSystem.Spanner=> "spanner",
        DbSystem.Sqlite=> "sqlite",
        DbSystem.Sybase=> "sybase",
        DbSystem.Teradata=> "teradata",
        DbSystem.Trino=> "trino",
        DbSystem.Vertica=> "vertica",
        _ => throw new ArgumentOutOfRangeException(nameof(dbSystem), dbSystem, null)
    };

}
