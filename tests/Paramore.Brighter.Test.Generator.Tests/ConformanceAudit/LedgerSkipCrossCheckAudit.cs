using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Paramore.Brighter.Test.Generator;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A single violation found during the two-direction ledger–Skip cross-check.
/// </summary>
/// <param name="Kind">
/// <c>"SkipWithoutLedgerRow"</c> — a Skip issue number has no matching Deferred ledger cell; or
/// <c>"LedgerMissingField"</c> — a Deferred ledger cell is missing its issue link or sign-off.
/// </param>
/// <param name="Detail">Human-readable description of the specific violation.</param>
public sealed record LedgerCrossCheckViolation(string Kind, string Detail);

/// <summary>
/// The aggregate result of a ledger–Skip cross-check run.
/// </summary>
/// <param name="LedgerDataRowsFound">Total matrix data rows parsed from the conformance ledger.</param>
/// <param name="DeferredCellsFound">Ledger cells whose trimmed value starts with "Deferred".</param>
/// <param name="DistinctSkipIssueNumbers">
/// Distinct issue numbers found in in-tree <c>Deferred: #&lt;n&gt;</c> Skip markers.
/// </param>
/// <param name="Violations">All violations found across both cross-check directions.</param>
public sealed record CrossCheckResult(
    int LedgerDataRowsFound,
    int DeferredCellsFound,
    int DistinctSkipIssueNumbers,
    IReadOnlyList<LedgerCrossCheckViolation> Violations);

/// <summary>
/// A generated test whose Skip disagrees with its own (LedgerKey × FR column) ledger cell.
/// </summary>
/// <param name="FilePath">The generated test file.</param>
/// <param name="LedgerKey">The configuration row the file belongs to, e.g. "AWS / SqsFifo".</param>
/// <param name="FrColumn">The behaviour column the file is judged against, e.g. "FR-16".</param>
/// <param name="ExpectedSkip">What the generator would emit for that cell ("" means no Skip).</param>
/// <param name="ActualSkip">What the file actually carries ("" means no Skip).</param>
public sealed record CellAgreementViolation(
    string FilePath,
    string LedgerKey,
    string FrColumn,
    string ExpectedSkip,
    string ActualSkip);

/// <summary>
/// The aggregate result of a cell-agreement run.
/// </summary>
/// <param name="LedgerKeysResolved">Configurations whose Generated directory was found on disk.</param>
/// <param name="FilesChecked">Canonical generated test files compared against a ledger cell.</param>
/// <param name="ExpectedSkipCount">Files whose cell required a Skip (Deferred).</param>
/// <param name="ExpectedNoSkipCount">Files whose cell required no Skip (Pass/Fixed).</param>
/// <param name="Violations">Every file that disagreed with its cell.</param>
public sealed record CellAgreementResult(
    int LedgerKeysResolved,
    int FilesChecked,
    int ExpectedSkipCount,
    int ExpectedNoSkipCount,
    IReadOnlyList<CellAgreementViolation> Violations);

/// <summary>
/// Read-only, network-free cross-check audit between in-tree Deferred Skip markers and the
/// conformance ledger (ADR 0067, FR-13, FR-21).
///
/// Two directions are enforced:
/// <list type="number">
///   <item><description>
///     <b>Skip → Ledger</b> — every distinct issue number in a generated/template
///     <c>Skip = "Deferred: #&lt;n&gt; …"</c> must appear in at least one ledger cell as
///     <c>Deferred -&gt; #&lt;n&gt;</c>. A Skip whose number has no such cell is a violation.
///   </description></item>
///   <item><description>
///     <b>Ledger → Trail</b> — every ledger cell whose value starts with "Deferred" must carry
///     both a real issue link (<c>#&lt;digits&gt;</c>) and a sign-off token
///     (<c>sign-off: @&lt;name&gt;</c>). A Deferred cell missing either is a violation.
///   </description></item>
/// </list>
///
/// The build deliberately does <em>not</em> query the live issue tracker — issue-open state and
/// sign-off provenance are the maintainer-review gate's responsibility (ADR 0067, step 7).
///
/// Scanned artifact paths (in-tree only):
/// <list type="bullet">
///   <item><c>tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/**/*.cs.liquid</c></item>
///   <item><c>tests/Paramore.Brighter.*.Tests/**/Generated/**/*.cs</c></item>
/// </list>
/// </summary>
public static class LedgerSkipCrossCheckAudit
{
    // #<digits> — present in both Deferred ledger cells and conforming Skip values
    private static readonly Regex ISSUE_LINK_PATTERN =
        new(@"#(\d+)", RegexOptions.Compiled);

    // sign-off: @<non-whitespace> — required in every Deferred ledger cell
    private static readonly Regex SIGN_OFF_PATTERN =
        new(@"sign-off:\s*@\S+", RegexOptions.Compiled);

    // Deferred: #<digits> — the only valid Skip prefix; captures the issue number
    private static readonly Regex SKIP_DEFERRED_PREFIX =
        new(@"^Deferred:\s*#(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="cellValue"/> starts with "Deferred" and carries
    /// both a real issue link (<c>#&lt;digits&gt;</c>) and a sign-off token
    /// (<c>sign-off: @&lt;name&gt;</c>).
    /// </summary>
    public static bool IsValidDeferredCell(string cellValue) =>
        cellValue.TrimStart().StartsWith("Deferred", System.StringComparison.Ordinal)
        && ISSUE_LINK_PATTERN.IsMatch(cellValue)
        && SIGN_OFF_PATTERN.IsMatch(cellValue);

    /// <summary>
    /// Runs the two-direction cross-check over the in-tree artifacts rooted at
    /// <paramref name="repoRoot"/> and the conformance ledger at <paramref name="ledgerPath"/>.
    /// Returns the aggregate result. Never makes network calls or spawns subprocesses.
    /// </summary>
    public static CrossCheckResult CrossCheck(string repoRoot, string ledgerPath)
    {
        var violations = new List<LedgerCrossCheckViolation>();

        // ── Parse the conformance ledger ──────────────────────────────────────
        var (dataRowsFound, deferredCells, ledgerIssueNumbers) = ParseLedger(ledgerPath);

        // ── Direction 2: Ledger → Trail ───────────────────────────────────────
        // Every Deferred cell must carry a real issue link AND a sign-off token.
        foreach (var cell in deferredCells)
        {
            if (IsValidDeferredCell(cell))
                continue;

            var hasIssue   = ISSUE_LINK_PATTERN.IsMatch(cell);
            var hasSignOff = SIGN_OFF_PATTERN.IsMatch(cell);
            var missing    = (!hasIssue && !hasSignOff) ? "issue link and sign-off"
                           : !hasIssue                  ? "issue link (#<digits>)"
                                                        : "sign-off (sign-off: @<name>)";
            violations.Add(new LedgerCrossCheckViolation(
                "LedgerMissingField",
                $"Deferred ledger cell is missing {missing}: \"{cell}\""));
        }

        // ── Scan in-tree artifacts for Deferred Skip issue numbers ────────────
        var skipIssueNumbers = ScanTreeForDeferredIssueNumbers(repoRoot);

        // ── Direction 1: Skip → Ledger ────────────────────────────────────────
        // Every distinct issue number from a Skip must appear in at least one Deferred ledger cell.
        foreach (var number in skipIssueNumbers.OrderBy(n => n))
        {
            if (!ledgerIssueNumbers.Contains(number))
                violations.Add(new LedgerCrossCheckViolation(
                    "SkipWithoutLedgerRow",
                    $"Skip marker references issue #{number} but no Deferred ledger cell carries '#{number}'"));
        }

        return new CrossCheckResult(
            dataRowsFound,
            deferredCells.Count,
            skipIssueNumbers.Count,
            violations);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses the conformance-status.md matrix and returns the total data rows found, the list of
    /// Deferred cell values, and the set of issue numbers extracted from those Deferred cells.
    ///
    /// The matrix header is identified by "| Configuration |" so that the two-column Cell
    /// Vocabulary table (whose Deferred row contains backtick-formatted text) is never mistaken
    /// for matrix data.
    /// </summary>
    private static (int dataRowsFound, List<string> deferredCells, HashSet<string> ledgerIssueNumbers)
        ParseLedger(string ledgerPath)
    {
        var deferredCells      = new List<string>();
        var ledgerIssueNumbers = new HashSet<string>();
        var dataRowsFound      = 0;

        var lines              = File.ReadAllLines(ledgerPath);
        var matrixHeaderIndex  = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("| Configuration |", System.StringComparison.Ordinal))
            {
                matrixHeaderIndex = i;
                break;
            }
        }

        if (matrixHeaderIndex < 0)
            return (dataRowsFound, deferredCells, ledgerIssueNumbers);

        // matrixHeaderIndex + 1 is the separator row (|---|---|…); data starts at + 2.
        var dataStart = matrixHeaderIndex + 2;

        for (var i = dataStart; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("|"))
                break; // end of the markdown table

            var cells = line.Split('|')
                            .Select(c => c.Trim())
                            .Where(c => c.Length > 0)
                            .ToArray();

            // A data row has at least one configuration cell and one behaviour cell.
            if (cells.Length < 2)
                continue;

            dataRowsFound++;

            // cells[0] is the configuration name; cells[1..] are the behaviour columns.
            foreach (var cell in cells.Skip(1))
            {
                if (!cell.StartsWith("Deferred", System.StringComparison.Ordinal))
                    continue;

                deferredCells.Add(cell);

                var issueMatch = ISSUE_LINK_PATTERN.Match(cell);
                if (issueMatch.Success)
                    ledgerIssueNumbers.Add(issueMatch.Groups[1].Value);
            }
        }

        return (dataRowsFound, deferredCells, ledgerIssueNumbers);
    }

    /// <summary>
    /// Returns the distinct issue numbers extracted from conforming <c>Deferred: #&lt;n&gt;</c> Skip
    /// markers. The artifact set and the Skip extraction are borrowed from
    /// <see cref="GatewaySkipConventionAudit"/> so both audits provably scan the same files.
    /// </summary>
    private static HashSet<string> ScanTreeForDeferredIssueNumbers(string repoRoot)
    {
        var numbers = new HashSet<string>();

        foreach (var file in GatewaySkipConventionAudit.EnumerateGatewayArtifacts(repoRoot))
        {
            foreach (var (_, value) in GatewaySkipConventionAudit.ExtractSkipValues(file))
            {
                var issueMatch = SKIP_DEFERRED_PREFIX.Match(value);
                if (issueMatch.Success)
                    numbers.Add(issueMatch.Groups[1].Value);
            }
        }

        return numbers;
    }

    /// <summary>
    /// Checks every canonical generated test against ITS OWN ledger cell — the exact
    /// (LedgerKey × FR column) intersection — in both directions: a cell that defers must have a
    /// Skip, a cell that passes must not, and a Skip that exists must be character-for-character
    /// what the generator would emit for that cell.
    ///
    /// The expected value comes from <see cref="ConformanceLedger.GetSkip"/> — the generator's own
    /// emitter — so this cannot drift from what generation produces. It makes the audit a
    /// regeneration-drift detector for Skip attributes: a cell flipped without re-running
    /// ./generate-test.sh fails here.
    /// </summary>
    public static CellAgreementResult CheckCellAgreement(string repoRoot, string ledgerPath)
    {
        var violations         = new List<CellAgreementViolation>();
        var ledger             = new ConformanceLedger(ledgerPath);
        var ledgerKeysResolved = 0;
        var filesChecked       = 0;
        var expectedSkip       = 0;
        var expectedNoSkip     = 0;

        foreach (var (generatedDir, ledgerKey) in EnumerateGeneratedDirectories(repoRoot))
        {
            ledgerKeysResolved++;

            foreach (var file in Directory.EnumerateFiles(generatedDir, "*.cs", SearchOption.AllDirectories))
            {
                var frColumn = CanonicalBehaviours.FrColumnFor(Path.GetFileNameWithoutExtension(file));
                if (frColumn == null)
                    continue; // not a canonical behaviour — never ledger-gated

                var expected = ledger.GetSkip(ledgerKey, frColumn, CanonicalBehaviours.BehaviourFor(frColumn));

                // A generated file carries the same Skip on every fact it declares (Reactor files
                // declare two). Collapse to the distinct values so a file whose facts disagree with
                // each other — one skipped, one not — is reported rather than judged on its first.
                var distinctSkips = GatewaySkipConventionAudit.ExtractSkipValues(file)
                                                              .Select(s => s.Value)
                                                              .Distinct()
                                                              .ToArray();
                var actual = distinctSkips.Length switch
                {
                    0 => string.Empty,
                    1 => distinctSkips[0],
                    _ => string.Join(" / ", distinctSkips)
                };

                filesChecked++;
                if (expected.Length == 0) expectedNoSkip++; else expectedSkip++;

                if (expected != actual)
                    violations.Add(new CellAgreementViolation(file, ledgerKey, frColumn, expected, actual));
            }
        }

        return new CellAgreementResult(
            ledgerKeysResolved, filesChecked, expectedSkip, expectedNoSkip, violations);
    }

    /// <summary>
    /// Yields each generated-test directory paired with the LedgerKey of the configuration that
    /// produced it, by reading every test project's test-configuration.json.
    ///
    /// This mirrors the generator's output-path convention
    /// (<c>MessagingGateway/{prefix}/Generated</c>, where prefix is the gateway's declared Prefix
    /// or, for a multi-gateway configuration, its key). The live-tree fact asserts a non-zero
    /// resolved count, so if that convention ever changes this fails loudly rather than silently
    /// checking nothing.
    /// </summary>
    private static IEnumerable<(string GeneratedDir, string LedgerKey)> EnumerateGeneratedDirectories(string repoRoot)
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
                continue; // a malformed configuration is the generator's problem to report, not ours
            }

            if (configuration == null)
                continue;

            foreach (var (prefix, gateway) in EnumerateGateways(configuration))
            {
                if (string.IsNullOrEmpty(gateway.LedgerKey))
                    continue; // no ledger row declared — nothing to check against

                var generatedDir = Path.Combine(projectDir, "MessagingGateway", prefix, "Generated");
                if (Directory.Exists(generatedDir))
                    yield return (generatedDir, gateway.LedgerKey);
            }
        }
    }

    private static IEnumerable<(string Prefix, MessagingGatewayConfiguration Gateway)> EnumerateGateways(
        TestConfiguration configuration)
    {
        if (configuration.MessagingGateway != null)
            yield return (configuration.MessagingGateway.Prefix, configuration.MessagingGateway);

        if (configuration.MessagingGateways == null)
            yield break;

        foreach (var (key, gateway) in configuration.MessagingGateways)
            yield return (string.IsNullOrEmpty(gateway.Prefix) ? key : gateway.Prefix, gateway);
    }
}
