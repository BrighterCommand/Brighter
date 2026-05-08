using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

/// <summary>
/// Validates that the payload column type in an existing Spanner box table matches the
/// configured binary/text mode. Throws <see cref="ConfigurationException"/> on mismatch
/// and returns quietly on match.
/// </summary>
/// <remarks>
/// Stateless service; safe to register as a DI singleton.
/// <para>
/// Spanner has no schema concept, so the <c>schemaName</c> parameter is accepted and
/// ignored — including <c>null</c>. The parameter exists only to satisfy the
/// role-interface signature shared with the relational backends that DO partition by
/// schema (MSSQL, Postgres, MySQL).
/// </para>
/// <para>
/// Spanner column types are reported by <c>INFORMATION_SCHEMA.COLUMNS.SPANNER_TYPE</c>
/// as type literals such as <c>BYTES(MAX)</c>, <c>BYTES(1024)</c>, <c>STRING(MAX)</c>,
/// or <c>STRING(255)</c>. Per ADR 0057 §A.3 we match the prefix using
/// <c>STARTS_WITH</c> so that any size-parameterised variant compares correctly to the
/// configured payload mode.
/// </para>
/// </remarks>
public class SpannerPayloadModeValidator : IAmABoxPayloadModeValidator<SpannerConnection>
{
    /// <summary>
    /// Validates the payload mode of an existing table column against
    /// <c>INFORMATION_SCHEMA.COLUMNS</c>.
    /// </summary>
    /// <param name="connection">An open Spanner connection.</param>
    /// <param name="tableName">The unqualified box table name.</param>
    /// <param name="schemaName">Accepted and ignored — Spanner has no schema concept.</param>
    /// <param name="columnName">The unqualified payload column name (e.g. "Body" or "CommandBody").</param>
    /// <param name="binaryMessagePayload">Whether binary payload mode is configured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ValidateAsync(
        SpannerConnection connection, string tableName, string? schemaName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken = default)
    {
        using var command = connection.CreateSelectCommand(
            @"SELECT SPANNER_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName",
            new SpannerParameterCollection
            {
                { "TableName", SpannerDbType.String, tableName },
                { "ColumnName", SpannerDbType.String, columnName }
            });

        var spannerType = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (spannerType == null) return;

        var isBinary = spannerType.StartsWith("BYTES", StringComparison.OrdinalIgnoreCase);
        var isText = spannerType.StartsWith("STRING", StringComparison.OrdinalIgnoreCase);

        if (binaryMessagePayload && isText)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{tableName}`. " +
                $"Column `{columnName}` is {spannerType} (text) but binary mode was configured. " +
                "Either change the configuration or alter the column type.");
        }

        if (!binaryMessagePayload && isBinary)
        {
            throw new ConfigurationException(
                $"Payload mode mismatch for table `{tableName}`. " +
                $"Column `{columnName}` is {spannerType} (binary) but text mode was configured. " +
                "Either change the configuration or alter the column type.");
        }
    }
}
