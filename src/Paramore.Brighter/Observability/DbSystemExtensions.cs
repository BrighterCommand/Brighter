using System;

namespace Paramore.Brighter.Observability;

public static class DbSystemExtensions
{
    ///<summary>
    /// Provide a string representation of the span
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
