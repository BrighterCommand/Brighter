using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;

namespace Paramore.Brighter.BoxProvisioning.Spanner;

// Bridging shim — Phase 4.5 of spec 0028. Pure delegation onto a singleton
// SpannerPayloadModeValidator instance, passing null for the new schemaName slot
// (Spanner has no schema concept; the parameter is accepted and ignored).
// Removed in Phase 8 when call-sites rewire to instance dispatch.
public static class SpannerPayloadModeValidators
{
    private static readonly SpannerPayloadModeValidator s_instance = new();

    public static Task ValidateAsync(
        SpannerConnection connection, string tableName,
        string columnName, bool binaryMessagePayload,
        CancellationToken cancellationToken)
        => s_instance.ValidateAsync(
            connection, tableName, null, columnName, binaryMessagePayload, cancellationToken);
}
