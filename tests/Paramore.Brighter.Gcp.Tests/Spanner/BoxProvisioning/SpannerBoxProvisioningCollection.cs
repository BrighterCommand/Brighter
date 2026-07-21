
namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// Serializes Spanner box provisioning tests to avoid concurrent DDL operations,
/// which the Spanner emulator rejects.
/// </summary>
[System.Obsolete]
public class SpannerBoxProvisioningCollection;