using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner.BoxProvisioning;

/// <summary>
/// Serializes Spanner box provisioning tests to avoid concurrent DDL operations,
/// which the Spanner emulator rejects.
/// </summary>
[CollectionDefinition("SpannerBoxProvisioning", DisableParallelization = true)]
public class SpannerBoxProvisioningCollection;
