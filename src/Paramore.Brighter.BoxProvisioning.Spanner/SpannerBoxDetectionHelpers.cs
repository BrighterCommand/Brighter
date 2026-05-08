using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

// Bridging shim — Phase 2.5 of spec 0028. Pure delegation onto a singleton
// SpannerBoxDetectionHelper instance, passing null for the new schemaName slot
// and the unused transaction slot (Spanner has no schema concept and DDL is
// single-statement; both are accepted-and-ignored on the instance method).
// Removed in Phase 8 when call-sites rewire to instance dispatch.
internal static class SpannerBoxDetectionHelpers
{
    private static readonly SpannerBoxDetectionHelper s_instance = new();

    public static Task<bool> DoesTableExistAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
        => s_instance.DoesTableExistAsync(
            connection, tableName, null, cancellationToken);

    public static Task<bool> DoesHistoryExistAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
        => s_instance.DoesHistoryExistAsync(
            connection, tableName, null, cancellationToken);

    public static Task<int> GetMaxVersionAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
        => s_instance.GetMaxVersionAsync(
            connection, tableName, null, cancellationToken);

    public static Task<HashSet<string>> GetTableColumnsAsync(
        SpannerConnection connection, string tableName,
        CancellationToken cancellationToken)
        => s_instance.GetTableColumnsAsHashSetAsync(
            connection, tableName, null, cancellationToken);

    public static string DiscriminatorFor(BoxType boxType)
        => s_instance.DiscriminatorFor(boxType);
}
