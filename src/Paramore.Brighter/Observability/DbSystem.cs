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

namespace Paramore.Brighter.Observability;

/// <summary>
/// What is the identifier for the database system in use
/// Conforms to: https://opentelemetry.io/docs/specs/semconv/database/database-spans/
/// </summary>
public enum DbSystem
{
    AdaBas,
    Brighter,
    Cache,
    Cassandra,
    Clickhouse,
    Cloudscape,
    Cockroachdb,
    Coldfusion,
    Cosmosdb,
    Couchbase,
    Couchdb,
    Db2,
    Derby,
    Dynamodb,
    Edb,
    Elasticsearch,
    FileMaker,
    Firebird,
    FirstSql,
    Geode,
    H2,
    HanaDb,
    Hbase,
    Hive,
    Hsqldb,
    Informix,
    Ingres,
    InstantDb,
    Interbase,
    Mariadb,
    MaxDb,
    Memcached,
    Mongodb,
    MsSql,
    MsSqlCompact,
    MySql,
    Neo4J,
    Netezza,
    OpenSearch,
    Oracle,
    OtherSql, 
    Pervasive,
    PointBase,
    Postgresql,
    Progress,
    Redis,
    Redshift,
    Spanner,
    Sqlite,
    Sybase,
    Teradata,
    Trino,
    Vertica,
    Firestore,
}
