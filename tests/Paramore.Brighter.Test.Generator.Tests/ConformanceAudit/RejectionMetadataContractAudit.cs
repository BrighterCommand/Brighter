using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Paramore.Brighter.Test.Generator;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A single rejection-metadata contract violation.
/// </summary>
/// <param name="Kind">
/// <c>"MixedRejectionKeys"</c> — a provider filled some rejection keys and left others empty; or
/// <c>"UndeclaredRoutingOnly"</c> — a provider stamps no metadata yet its FR-8 cell claims
/// conformance, without being a declared relaxation; or
/// <c>"StaleRoutingOnlyDeclaration"</c> — a declared relaxation whose provider now stamps metadata,
/// so the declaration and the ledger prose that goes with it are out of date.
/// </param>
/// <param name="LedgerKey">The configuration row, e.g. "RMQ.Async / Classic".</param>
/// <param name="Detail">Human-readable description of the specific violation.</param>
public sealed record RejectionKeysViolation(string Kind, string LedgerKey, string Detail);

/// <summary>
/// The aggregate result of a rejection-metadata contract audit.
/// </summary>
/// <param name="ProvidersScanned">Configured gateways whose provider file was found and read.</param>
/// <param name="RoutingOnlyProvidersFound">Providers returning an empty string for every key.</param>
/// <param name="Violations">Every contract violation found.</param>
public sealed record RejectionContractResult(
    int ProvidersScanned,
    int RoutingOnlyProvidersFound,
    IReadOnlyList<RejectionKeysViolation> Violations);

/// <summary>
/// Read-only, network-free audit of the rejection-metadata contract (ADR 0067).
///
/// <para>The contract asserts that rejection metadata is stamped onto a rejected message. A transport that
/// dead-letters through a native broker mechanism routes the untouched original and stamps nothing,
/// and declares that by returning <c>string.Empty</c> for every key in
/// <c>RejectionMetadataKeys</c>. The generated FR-8 test then skips its metadata block, leaving it
/// asserting only arrival at the dead-letter destination — which is what FR-4 already asserts. FR-8
/// is a duplicate of FR-4 for those transports, by maintainer-approved relaxation.</para>
///
/// <para>Two things follow, and this audit enforces both. The key set has to be all-empty or
/// all-filled, because the generated code decides whether to assert metadata by reading a single
/// key and would look up an empty Bag key for any other combination. And a transport that stamps
/// nothing while claiming FR-8 has to be one of the declared relaxations, or a conformance claim
/// nothing checks can arrive unnoticed.</para>
///
/// <para>Reads provider source as text rather than reflecting over it: the audit lives in the
/// generator's test project, which does not reference the transport test projects, and a text scan
/// keeps it that way.</para>
/// </summary>
public static class RejectionMetadataContractAudit
{
    /// <summary>
    /// Configurations whose gateway dead-letters natively, stamps no Brighter metadata, and is
    /// allowed to claim FR-8 on routing alone.
    /// </summary>
    /// <remarks>
    /// <para>Each entry has prose in <c>conformance-status.md</c> saying so. This set is the
    /// executable half of that prose: add a transport here only alongside the ledger note, and the
    /// audit reports a stale entry if its provider later starts stamping metadata.</para>
    /// <para>A configuration that stamps nothing and leaves FR-8 <c>Deferred</c> does not belong
    /// here — it is claiming nothing, so there is nothing to relax.</para>
    /// </remarks>
    private static readonly HashSet<string> DECLARED_ROUTING_ONLY_RELAXATIONS =
        new(System.StringComparer.Ordinal)
        {
            "RMQ.Async / Classic",
            "RMQ.Async / Quorum",
            "RMQ.Sync / RmqSyncMessagingGateway",
            "AzureServiceBus / AzureServiceBusMessagingGateway"
        };

    // The arguments of `new RejectionMetadataKeys( … )`, up to the closing parenthesis. Non-greedy
    // so a file holding more than one construction yields them separately rather than as one blob.
    private static readonly Regex REJECTION_KEYS_CONSTRUCTION =
        new(@"new\s+RejectionMetadataKeys\s*\((?<args>[^)]*)\)", RegexOptions.Compiled | RegexOptions.Singleline);

    // Cell values that assert FR-8 conformance. "Deferred" and "Unknown" claim nothing.
    private static readonly HashSet<string> CLAIMING_CELL_PREFIXES =
        new(System.StringComparer.Ordinal) { "Pass", "Fixed" };

    private const string FR8_COLUMN = "FR-8";

    /// <summary>
    /// Audits every configured gateway under <paramref name="repoRoot"/> against the ledger at
    /// <paramref name="ledgerPath"/>.
    /// </summary>
    /// <param name="repoRoot">Repository root holding the <c>tests/</c> tree.</param>
    /// <param name="ledgerPath">Path to <c>conformance-status.md</c>.</param>
    /// <returns>The providers scanned, how many stamp nothing, and every violation found.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown by <see cref="ConformanceLedger"/> when the ledger's matrix cannot be read.
    /// </exception>
    public static RejectionContractResult Audit(string repoRoot, string ledgerPath)
    {
        var ledger     = new ConformanceLedger(ledgerPath);
        var violations = new List<RejectionKeysViolation>();
        var scanned    = 0;
        var routingOnly = 0;

        foreach (var (ledgerKey, keys) in EnumerateProviderKeys(repoRoot))
        {
            scanned++;

            var filled = keys.Count(k => !IsEmptyKeyLiteral(k));

            if (filled > 0 && filled < keys.Count)
            {
                violations.Add(new RejectionKeysViolation(
                    "MixedRejectionKeys", ledgerKey,
                    $"{filled} of {keys.Count} rejection keys are filled. The contract is all or "
                    + "none: StampsRejectionMetadata reads only RejectionReason, so a partly-filled "
                    + "set is reported as stamping and then looks up an empty Bag key."));
                continue;
            }

            if (filled > 0)
            {
                if (DECLARED_ROUTING_ONLY_RELAXATIONS.Contains(ledgerKey))
                {
                    violations.Add(new RejectionKeysViolation(
                        "StaleRoutingOnlyDeclaration", ledgerKey,
                        "Declared as a routing-only FR-8 relaxation, but its provider now stamps "
                        + "every rejection key. Drop it from the declared set and update the "
                        + "ledger prose that records the relaxation."));
                }

                continue;
            }

            routingOnly++;

            if (!ClaimsFr8(ledger, ledgerKey))
                continue;

            if (DECLARED_ROUTING_ONLY_RELAXATIONS.Contains(ledgerKey))
                continue;

            violations.Add(new RejectionKeysViolation(
                "UndeclaredRoutingOnly", ledgerKey,
                "Stamps no rejection metadata, so its generated FR-8 test asserts only that the "
                + "message reached the dead-letter destination - which is what FR-4 asserts. The "
                + "FR-8 cell nevertheless claims conformance. Either record the relaxation (ledger "
                + "prose plus the declared set in this audit) or defer the cell."));
        }

        return new RejectionContractResult(scanned, routingOnly, violations);
    }

    /// <summary>
    /// Whether the configuration's FR-8 cell asserts conformance rather than deferring it.
    /// </summary>
    private static bool ClaimsFr8(ConformanceLedger ledger, string ledgerKey) =>
        ledger.TryGetCell(ledgerKey, FR8_COLUMN, out var cellValue)
        && CLAIMING_CELL_PREFIXES.Any(
            prefix => cellValue.TrimStart().StartsWith(prefix, System.StringComparison.Ordinal));

    /// <summary>
    /// Whether a constructor argument is an empty-string literal in either of the two spellings
    /// the providers use.
    /// </summary>
    private static bool IsEmptyKeyLiteral(string argument) =>
        argument is "string.Empty" or "\"\"";

    /// <summary>
    /// Yields each configured gateway's ledger key paired with the rejection-key arguments its
    /// provider passes to <c>RejectionMetadataKeys</c>.
    /// </summary>
    /// <remarks>
    /// A gateway with no LedgerKey, or whose provider file or key construction cannot be found, is
    /// skipped: those are other audits' findings, and the live-tree fact asserts a non-zero scan
    /// count so a discovery path that silently matches nothing fails there.
    /// </remarks>
    private static IEnumerable<(string LedgerKey, IReadOnlyList<string> Keys)>
        EnumerateProviderKeys(string repoRoot)
    {
        var testsRoot = Path.Combine(repoRoot, "tests");
        if (!Directory.Exists(testsRoot))
            yield break;

        foreach (var projectDir in Directory.EnumerateDirectories(testsRoot, "Paramore.Brighter.*.Tests"))
        {
            var configPath = Path.Combine(projectDir, "test-configuration.json");
            if (!File.Exists(configPath))
                continue;

            TestConfiguration? configuration;
            try
            {
                configuration = JsonSerializer.Deserialize<TestConfiguration>(File.ReadAllText(configPath));
            }
            catch (JsonException)
            {
                continue; // a malformed configuration is the generator's problem to report
            }

            if (configuration == null)
                continue;

            foreach (var gateway in EnumerateGateways(configuration))
            {
                if (string.IsNullOrWhiteSpace(gateway.LedgerKey)
                    || string.IsNullOrWhiteSpace(gateway.MessageGatewayProvider))
                    continue;

                var keys = ReadRejectionKeys(projectDir, gateway.MessageGatewayProvider!);
                if (keys != null)
                    yield return (gateway.LedgerKey!, keys);
            }
        }
    }

    /// <summary>
    /// Reads the arguments of the first <c>new RejectionMetadataKeys(…)</c> in the provider's
    /// source file, or <c>null</c> when the file or the construction is not found.
    /// </summary>
    private static IReadOnlyList<string>? ReadRejectionKeys(string projectDir, string providerTypeName)
    {
        var simpleName = providerTypeName[(providerTypeName.LastIndexOf('.') + 1)..];

        var providerFile = Directory
            .EnumerateFiles(projectDir, $"{simpleName}.cs", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (providerFile == null)
            return null;

        var match = REJECTION_KEYS_CONSTRUCTION.Match(File.ReadAllText(providerFile));
        if (!match.Success)
            return null;

        var keys = match.Groups["args"].Value
            .Split(',')
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .ToArray();

        return keys.Length > 0 ? keys : null;
    }

    /// <summary>
    /// Yields the gateway configurations a test configuration declares, whether it carries a
    /// single <c>MessagingGateway</c> or a keyed set of them.
    /// </summary>
    private static IEnumerable<MessagingGatewayConfiguration> EnumerateGateways(
        TestConfiguration configuration)
    {
        if (configuration.MessagingGateway != null)
            yield return configuration.MessagingGateway;

        if (configuration.MessagingGateways == null)
            yield break;

        foreach (var gateway in configuration.MessagingGateways.Values)
            yield return gateway;
    }
}
