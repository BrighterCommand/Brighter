#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

// The sibling test script for show_me_facts.cs (NFR-9). No test framework: it is a plain
// file-based app that shells out to the measurement script for each row below, checks the
// row's assertions against the process exit code and the ledger it wrote, and reports.

var repositoryRoot = Directory.GetCurrentDirectory();

const string DeclaredTarget = ".claude/test-fixtures/show-me/declared";
const string ZeroIdTarget = ".claude/test-fixtures/show-me/zero-id";
const string NoTasksTarget = ".claude/test-fixtures/show-me/no-tasks";
const string AdrUnresolvedTarget = ".claude/test-fixtures/show-me/adr-unresolved";
const string UnfinishedTarget = ".claude/test-fixtures/show-me/unfinished";
const string ReleaseNotesFixture = ".claude/test-fixtures/show-me/release-notes.md";
const string DefaultReleaseNotesPath = "release_notes.md";
const string CalibrationTarget = "specs/0036-scoped-lifetime-per-pipeline";
const string CalibrationMergeBase = "6145913a0";
const string CalibrationMeasuredHead = "91d549be6";

// The declared fixture's file fields (tasks, declared ids, .adr-list) — read from the working
// tree, so they hold the same values on the plain, unpinned row below and on a pinned run
// against the same target (T5.1: "its file fields equal those of its unpinned row").
var DeclaredFileFieldAssertions = Combine(
    TasksFieldAssertions(
        repositoryRoot,
        DeclaredTarget,
        total: 2,
        uncheckedCount: 0,
        byTag: new Dictionary<string, int>
        {
            ["TEST + IMPLEMENT"] = 0,
            ["STRUCTURAL"] = 0,
            ["PROJECT"] = 0,
            ["DOC"] = 0,
            ["untagged"] = 2,
        }),
    DeclaredIdAssertions(
        repositoryRoot,
        DeclaredTarget,
        expectedDeclaredTotal: 3,
        expectedDeclaredIds: ["FR-1", "FR-2", "NFR-1"],
        expectedDeclarations:
        [
            // Hand-computed from the fixture under KC1's paragraph rule: a declaration's
            // paragraph runs to the line before the next declaration, or to EOF.
            new DeclaredIdExpectation("FR-1", FirstLine: 5, LastLine: 7, Bytes: 196),
            new DeclaredIdExpectation("FR-2", FirstLine: 8, LastLine: 9, Bytes: 101),
            new DeclaredIdExpectation("NFR-1", FirstLine: 10, LastLine: 10, Bytes: 55),
        ]),
    AdrListAssertions(
        expectedEntries:
        [
            new AdrListEntryExpectation(
                "0062-pg-advisory-lock-sha256.md",
                Path: "docs/adr/0062-pg-advisory-lock-sha256.md",
                Reason: null,
                Matches: null,
                ExpectExtract: true),
            new AdrListEntryExpectation(
                "docs/adr/0072-show-me-command-resolution-and-output.md",
                Path: "docs/adr/0072-show-me-command-resolution-and-output.md",
                Reason: null,
                Matches: null,
                ExpectExtract: true),
        ],
        expectedResolvedCount: 2));

var declaredRow = new Row(
    Name: "declared",
    Args: [DeclaredTarget],
    LedgerTarget: DeclaredTarget,
    Assertions: Combine(DeclaredAssertions, DeclaredFileFieldAssertions));

// T4.1: the release-notes fixture has exactly one section marked for the declared fixture
// (<!-- spec: declared -->), a ### heading at line 3 running to line 28 (the line before the
// next ### heading, "### Some other change"). m = 2: the sub-bullet, both fenced lines and the
// #### Usage bullets are not top-level Breaking-changes bullets, and the unmarked section is
// not counted at all. Bytes hand-computed from the fixture's byte offsets.
var DeclaredReleaseNotesAssertions = ReleaseNotesAssertions(
    repositoryRoot,
    ReleaseNotesFixture,
    expectedPresent: true,
    expectedCount: 1,
    expectedM: 2,
    expectedSections: [new ReleaseNotesSectionExpectation(FirstLine: 3, LastLine: 28, Bytes: 603)]);

// T5.1: the calibration pair's diff fields, independently measured 2026-09-26 —
// `git diff --numstat 6145913a0..91d549be6` bucketed by first path segment,
// `git diff --name-only 6145913a0..91d549be6 -- src/` for the subdirectory count,
// `git diff 6145913a0..91d549be6 -- src/ | grep -cE '^[+-][[:space:]]*(public|protected)[[:space:]]'`
// for the public-API count, and `git rev-list --count 6145913a0..91d549be6` for the commit
// count. These fields depend only on the pinned pair, not the target, so the same expected
// values apply whether the pinned target is the calibration spec or the declared fixture.
var CalibrationPinnedDiffAssertions = PinnedDiffAssertions(
    CalibrationMergeBase,
    CalibrationMeasuredHead,
    expectedBucketFiles: new Dictionary<string, int>
    {
        ["src"] = 76,
        ["tests"] = 393,
        ["docs"] = 14,
        ["specs"] = 24,
        ["github"] = 0,
        ["other"] = 10,
        ["total"] = 517,
    },
    expectedTotalAdded: 45_284,
    expectedTotalRemoved: 515,
    expectedSrcAdded: 3_641,
    expectedSrcRemoved: 264,
    expectedSrcSubdirectoryCount: 6,
    expectedPublicApiLines: 131,
    expectedCommits: 363,
    expectedSrcDiffBytes: 303_715);

// T5.2: F1 boundary pairs, each measured independently 2026-09-26 with
// `git diff --name-only {a} {b} -- src/ | wc -l` and confirmed `git merge-base --is-ancestor {a} {b}`.
// `master`'s history is never rewritten, so these pairs stay reachable (R1, "How to choose a
// boundary pair"). The n=10 pair's base happens to be spec 0036's own merge base
// (6145913a0) — a real commit in master's first-parent history at that point, not a coincidence
// worth avoiding.
const string F1At10Base = "6145913a0ae63c638bd5267c90b45c0b1f32ecfe";
const string F1At10Head = "a5b1e9ae6fe7faa1d89a6a5bf00e338ee6dfce1f";
const string F1At11Base = "bd5c938409d596912145276326080827d5be2fc4";
const string F1At11Head = "8bcc9a0a9c217f4e6ad61a47958c625d1bbbc5a4";
const string F1At50Base = "2e40648d9cbb24bd9058c8e19c536ac0e175de7e";
const string F1At50Head = "4582deb37a5931fa60d347ddec4dce10cd856da3";
const string F1At51Base = "44b804294a51642620643053f99679b15412b012";
const string F1At51Head = "28d370de51f223eb5188da0df455714b9a26ecb5";

var rows = new[]
{
    declaredRow,
    new Row("usage: no argument", [], null, UsageErrorAssertions),
    new Row("usage: --bogus", ["--bogus"], null, UsageErrorAssertions),
    new Row("usage: --pinned with one sha", [DeclaredTarget, "--pinned", "deadbeef"], DeclaredTarget, UsageErrorAssertions),
    new Row(
        "usage: -- probe (SDK re-parse tripwire)",
        ["specs/x", "--file", ".claude/test-fixtures/show-me/probe.cs"],
        "specs/x",
        ProbeAssertions),
    new Row(
        "usage: pinned to an all-zero sha",
        [DeclaredTarget, "--pinned", "0000000000000000000000000000000000000000", "0000000000000000000000000000000000000000"],
        DeclaredTarget,
        ToolingFaultAssertions),
    new Row(
        "declared pinned + release-notes (pinned first)",
        [DeclaredTarget, "--pinned", CalibrationMergeBase, CalibrationMeasuredHead, "--release-notes", ReleaseNotesFixture],
        DeclaredTarget,
        Combine(
            PinnedAssertions(DeclaredTarget, CalibrationMergeBase, CalibrationMeasuredHead),
            DeclaredReleaseNotesAssertions,
            DeclaredFileFieldAssertions,
            CalibrationPinnedDiffAssertions)),
    new Row(
        "declared pinned + release-notes (release-notes first)",
        [DeclaredTarget, "--release-notes", ReleaseNotesFixture, "--pinned", CalibrationMergeBase, CalibrationMeasuredHead],
        DeclaredTarget,
        Combine(
            PinnedAssertions(DeclaredTarget, CalibrationMergeBase, CalibrationMeasuredHead),
            DeclaredReleaseNotesAssertions)),
    new Row(
        "declared release-notes only, unpinned",
        [DeclaredTarget, "--release-notes", ReleaseNotesFixture],
        DeclaredTarget,
        Combine(UnpinnedReleaseNotesAssertions, DeclaredReleaseNotesAssertions)),
    new Row(
        "calibration pinned",
        [CalibrationTarget, "--pinned", CalibrationMergeBase, CalibrationMeasuredHead],
        CalibrationTarget,
        Combine(
            PinnedAssertions(CalibrationTarget, CalibrationMergeBase, CalibrationMeasuredHead),
            TasksFieldAssertions(
                repositoryRoot,
                CalibrationTarget,
                total: 82,
                uncheckedCount: 0,
                byTag: new Dictionary<string, int>
                {
                    ["TEST + IMPLEMENT"] = 62,
                    ["STRUCTURAL"] = 12,
                    ["PROJECT"] = 2,
                    ["DOC"] = 6,
                    ["untagged"] = 0,
                },
                assertLiteralTestPlusImplement: true),
            // Escaped-pipe regression (NFR-9): the declared-id count must be non-zero for every
            // fixture that declares ids. This count was once 0 where the answer was 28 — an
            // escaped `\|` from a table cell, verified live (0037 requirements.md).
            DeclaredIdAssertions(repositoryRoot, CalibrationTarget, expectedDeclaredTotal: 37),
            // 7 entries, all full filenames, all resolve — including names whose numbers are
            // duplicated elsewhere in docs/adr/ (0070-0076 also exist as other spec's ADRs).
            AdrResolvedCountAssertions(expectedResolvedCount: 7),
            CalibrationPinnedDiffAssertions,
            LedgerSizeAssertion("calibration pinned", maxBytes: 65_536),
            F1LevelAssertion(expectedLevel: "High", expectedSrcFiles: 76))),
    new Row(
        "F1 boundary: 10 src files (Low)",
        [DeclaredTarget, "--pinned", F1At10Base, F1At10Head],
        DeclaredTarget,
        F1LevelAssertion(expectedLevel: "Low", expectedSrcFiles: 10)),
    new Row(
        "F1 boundary: 11 src files (Medium)",
        [DeclaredTarget, "--pinned", F1At11Base, F1At11Head],
        DeclaredTarget,
        F1LevelAssertion(expectedLevel: "Medium", expectedSrcFiles: 11)),
    new Row(
        "F1 boundary: 50 src files (Medium)",
        [DeclaredTarget, "--pinned", F1At50Base, F1At50Head],
        DeclaredTarget,
        F1LevelAssertion(expectedLevel: "Medium", expectedSrcFiles: 50)),
    new Row(
        "F1 boundary: 51 src files (High)",
        [DeclaredTarget, "--pinned", F1At51Base, F1At51Head],
        DeclaredTarget,
        F1LevelAssertion(expectedLevel: "High", expectedSrcFiles: 51)),
    new Row(
        "zero-id",
        [ZeroIdTarget],
        ZeroIdTarget,
        Combine(
            TasksFieldAssertions(
                repositoryRoot,
                ZeroIdTarget,
                total: 3,
                uncheckedCount: 0,
                byTag: new Dictionary<string, int>
                {
                    ["TEST + IMPLEMENT"] = 1,
                    ["STRUCTURAL"] = 0,
                    ["PROJECT"] = 0,
                    ["DOC"] = 0,
                    ["untagged"] = 2,
                }),
            // FR-16 row 9: a requirements.md with no anchored declaration reports zero, not a
            // failure, and the empty set is reported as an empty list rather than null.
            DeclaredIdAssertions(repositoryRoot, ZeroIdTarget, expectedDeclaredTotal: 0, expectedDeclaredIds: []),
            // FR-16 row 6: no .adr-list — a measured, empty zero, not a missing value.
            AdrListAssertions(expectedEntries: [], expectedResolvedCount: 0),
            // T4.1: no --release-notes given, so the repository-root release_notes.md is read.
            // It exists but carries zero marker lines naming "zero-id" (or any spec), so count
            // is 0 and m is null — none of its real, unmarked sections are counted.
            ReleaseNotesAssertions(
                repositoryRoot,
                DefaultReleaseNotesPath,
                expectedPresent: true,
                expectedCount: 0,
                expectedM: null))),
    new Row(
        "adr-unresolved",
        [AdrUnresolvedTarget],
        AdrUnresolvedTarget,
        AdrListAssertions(
            expectedEntries:
            [
                new AdrListEntryExpectation(
                    "0000-no-such-adr.md", Path: null, Reason: "ADR file not found", Matches: null, ExpectExtract: false),
                new AdrListEntryExpectation(
                    "0037",
                    Path: null,
                    Reason: "ambiguous ADR number",
                    Matches:
                    [
                        "0037-add-messaging-gateway-generated-test.md",
                        "0037-aws-test-resource-cleanup.md",
                        "0037-provide-roslyn-analyzers-for-brighter.md",
                        "0037-reject-message-on-error-handler.md",
                        "0037-universal-scheduler-delay.md",
                    ],
                    ExpectExtract: false),
                // A bare number matching exactly one file still does not resolve (FR-16 row 7):
                // only a full filename or a docs/adr/{filename} path resolves.
                new AdrListEntryExpectation(
                    "0062", Path: null, Reason: "ADR file not found", Matches: null, ExpectExtract: false),
                // A path prefix other than docs/adr/ does not resolve either.
                new AdrListEntryExpectation(
                    "adr/0062-pg-advisory-lock-sha256.md", Path: null, Reason: "ADR file not found", Matches: null, ExpectExtract: false),
                new AdrListEntryExpectation(
                    "0062-pg-advisory-lock-sha256.md",
                    Path: "docs/adr/0062-pg-advisory-lock-sha256.md",
                    Reason: null,
                    Matches: null,
                    ExpectExtract: true),
            ],
            expectedResolvedCount: 1)),
    new Row(
        "no-tasks",
        [NoTasksTarget],
        NoTasksTarget,
        GateFailureAssertions((record, failures) =>
        {
            if (!record.TryGetProperty("case", out var caseValue) || caseValue.GetString() != "tasks.md absent")
            {
                failures.Add("expected the gate record's case to be \"tasks.md absent\"");
            }
        })),
    new Row(
        "unfinished",
        [UnfinishedTarget],
        UnfinishedTarget,
        GateFailureAssertions((record, failures) =>
        {
            if (!record.TryGetProperty("case", out var caseValue) || caseValue.GetString() != "unchecked")
            {
                failures.Add("expected the gate record's case to be \"unchecked\"");
            }

            if (!record.TryGetProperty("unchecked", out var uncheckedValue) || uncheckedValue.GetInt32() != 2)
            {
                failures.Add("expected the gate record's unchecked to be 2");
            }

            if (!record.TryGetProperty("total", out var totalValue) || totalValue.GetInt32() != 3)
            {
                failures.Add("expected the gate record's total to be 3");
            }

            if (!record.TryGetProperty("first_unchecked", out var titles) || titles.ValueKind != JsonValueKind.Array)
            {
                failures.Add("expected the gate record's first_unchecked to be an array");
            }
            else
            {
                var titleStrings = titles.EnumerateArray().Select(t => t.GetString()).ToArray();
                foreach (var expectedTitle in new[] { "**DOC: Alpha**", "**DOC: Beta**" })
                {
                    if (!titleStrings.Contains(expectedTitle))
                    {
                        failures.Add($"expected the gate record's first_unchecked to contain \"{expectedTitle}\"");
                    }
                }
            }
        })),
};

var failedAssertions = 0;
foreach (var row in rows)
{
    failedAssertions += RunRow(row, repositoryRoot);
}

failedAssertions += CheckLedgerLeftAsFound(declaredRow, repositoryRoot);
failedAssertions += CheckPlantedLedgerUntouchedOnGateFailure(repositoryRoot, UnfinishedTarget);

Console.WriteLine(failedAssertions == 0
    ? $"{rows.Length} row(s) passed."
    : $"{failedAssertions} failed assertion(s) across {rows.Length} row(s).");

return failedAssertions == 0 ? 0 : 1;

static int RunRow(Row row, string repositoryRoot)
{
    var ledgerFullPath = row.LedgerTarget is null
        ? null
        : Path.Combine(repositoryRoot, row.LedgerTarget, ".show-me-ledger.json");

    // NFR-9: the test script leaves every target's ledger as it found it.
    var preExisting = ledgerFullPath is not null && File.Exists(ledgerFullPath)
        ? File.ReadAllBytes(ledgerFullPath)
        : null;

    var (exitCode, stderr) = InvokeScript(repositoryRoot, row.Args);

    var ledgerBytes = ledgerFullPath is not null && File.Exists(ledgerFullPath)
        ? File.ReadAllBytes(ledgerFullPath)
        : null;
    JsonDocument? ledger = null;
    if (ledgerBytes is not null)
    {
        try
        {
            ledger = JsonDocument.Parse(ledgerBytes);
        }
        catch (JsonException)
        {
            ledger = null;
        }
    }

    var gateRecord = ParseLastGateRecord(stderr);

    var failures = 0;
    foreach (var message in row.Assertions(new RunResult(exitCode, ledgerBytes, ledger, gateRecord)))
    {
        Console.WriteLine($"FAIL {row.Name}: {message}");
        failures++;
    }

    ledger?.Dispose();
    gateRecord?.Dispose();

    if (ledgerFullPath is not null)
    {
        if (preExisting is not null)
        {
            File.WriteAllBytes(ledgerFullPath, preExisting);
        }
        else if (File.Exists(ledgerFullPath))
        {
            File.Delete(ledgerFullPath);
        }
    }

    return failures;
}

static (int ExitCode, string Stderr) InvokeScript(string repositoryRoot, string[] scriptArgs)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = repositoryRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add(".claude/commands/spec/show_me_facts.cs");
    startInfo.ArgumentList.Add("--");
    foreach (var arg in scriptArgs)
    {
        startInfo.ArgumentList.Add(arg);
    }

    // Both streams are captured so the child process never blocks on a full pipe buffer.
    // stdout is never parsed — only the process exit code, stderr's last gate/word-count
    // record, and the ledger file are asserted on.
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("could not start dotnet");
    process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, stderr);
}

static JsonDocument? ParseLastGateRecord(string stderr)
{
    const string Prefix = "show-me-gate: ";
    string? lastRecordJson = null;

    foreach (var rawLine in stderr.Split('\n'))
    {
        var line = rawLine.TrimEnd('\r');
        if (line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            lastRecordJson = line[Prefix.Length..];
        }
    }

    if (lastRecordJson is null)
    {
        return null;
    }

    try
    {
        return JsonDocument.Parse(lastRecordJson);
    }
    catch (JsonException)
    {
        return null;
    }
}

static IEnumerable<string> DeclaredAssertions(RunResult result)
{
    if (result.ExitCode != 0)
    {
        yield return $"expected exit code 0, got {result.ExitCode}";
    }

    if (result.Ledger is null)
    {
        yield return "expected the ledger to parse as one JSON object, but no valid ledger was found";
        yield break;
    }

    var root = result.Ledger.RootElement;
    if (root.ValueKind != JsonValueKind.Object)
    {
        yield return $"expected the ledger to parse as one JSON object, got {root.ValueKind}";
        yield break;
    }

    if (!root.TryGetProperty("schema_version", out var schemaVersion) || schemaVersion.GetInt32() != 1)
    {
        yield return "expected schema_version 1";
    }

    if (!root.TryGetProperty("target", out var target) || target.GetString() != ".claude/test-fixtures/show-me/declared")
    {
        yield return "expected target to equal the given target";
    }

    if (!root.TryGetProperty("pinned", out var pinned) || pinned.ValueKind != JsonValueKind.False)
    {
        yield return "expected pinned false";
    }

    string[] refAndDiffFields =
    [
        "spec_branch", "rules_tried", "local_divergence", "base",
        "pr", "pr_count", "measured_head", "merge_base",
        "buckets", "src_subdirectory_count", "public_api_lines",
        "commits", "src_diff", "f1_level", "triggers",
    ];

    foreach (var message in AssertAllNull(root, refAndDiffFields, "not a spec directory"))
    {
        yield return message;
    }

    if (!root.TryGetProperty("gh_commands", out var ghCommands)
        || ghCommands.ValueKind != JsonValueKind.Array
        || ghCommands.GetArrayLength() != 0)
    {
        yield return "expected gh_commands to be an empty array";
    }

    if (result.LedgerBytes is { Length: > 65_536 } tooLarge)
    {
        yield return $"expected the ledger to be at most 65,536 B, got {tooLarge.Length}";
    }

    var lineCount = Encoding.UTF8.GetString(result.LedgerBytes ?? []).Split('\n').Length;
    if (lineCount <= 1)
    {
        yield return "expected the ledger to be indented over more than one line";
    }
}

static IEnumerable<string> UsageErrorAssertions(RunResult result)
{
    if (result.ExitCode is 0 or 2 or 77)
    {
        yield return $"expected an exit code other than 0, 2 or 77, got {result.ExitCode}";
    }

    if (result.LedgerBytes is not null)
    {
        yield return "expected no ledger to be written";
    }
}

static IEnumerable<string> ToolingFaultAssertions(RunResult result) => UsageErrorAssertions(result);

static IEnumerable<string> ProbeAssertions(RunResult result)
{
    // The probe fixture exits 77, a status no other row uses. If dotnet run ever re-parses
    // options after "--" and runs probe.cs instead of show_me_facts.cs, this row observes 77
    // here and fails — the tripwire for that SDK regression.
    if (result.ExitCode == 77)
    {
        yield return "expected the probe fixture NOT to have run (got its exit code, 77) — dotnet run re-parsed an option after --";
    }

    foreach (var message in UsageErrorAssertions(result))
    {
        yield return message;
    }

    Console.WriteLine($"INFO probe row: dotnet --version reports {RunDotnetVersion()}");
}

static string RunDotnetVersion()
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("--version");
    using var process = Process.Start(startInfo)!;
    var stdout = process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return stdout.Trim();
}

static Func<RunResult, IEnumerable<string>> PinnedAssertions(string target, string pinnedBase, string pinnedHead) => result =>
{
    var failures = new List<string>();

    if (result.ExitCode != 0)
    {
        failures.Add($"expected exit code 0, got {result.ExitCode}");
    }

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("pinned", out var pinned) || pinned.ValueKind != JsonValueKind.True)
    {
        failures.Add("expected pinned true");
    }

    if (!root.TryGetProperty("merge_base", out var mergeBase) || mergeBase.GetString() != pinnedBase)
    {
        failures.Add($"expected merge_base {pinnedBase}");
    }

    if (!root.TryGetProperty("measured_head", out var measuredHead)
        || measuredHead.ValueKind != JsonValueKind.Object
        || !measuredHead.TryGetProperty("sha", out var sha) || sha.GetString() != pinnedHead
        || !measuredHead.TryGetProperty("source", out var source) || source.GetString() != "pinned")
    {
        failures.Add($"expected measured_head {{ sha: {pinnedHead}, source: pinned }}");
    }

    string[] otherRefFields = ["spec_branch", "rules_tried", "local_divergence", "base", "pr", "pr_count"];
    if (root.TryGetProperty("null_reasons", out var nullReasons) && nullReasons.ValueKind == JsonValueKind.Object)
    {
        foreach (var field in otherRefFields)
        {
            if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"expected {field} to be null");
            }

            if (!nullReasons.TryGetProperty(field, out var reason) || reason.GetString() != "pinned")
            {
                failures.Add($"expected null_reasons.{field} to read \"pinned\"");
            }
        }

        foreach (var property in nullReasons.EnumerateObject())
        {
            if (property.Value.GetString() == "not a spec directory")
            {
                failures.Add($"expected no field to carry \"not a spec directory\" on a pinned run, but {property.Name} did");
            }
        }
    }
    else
    {
        failures.Add("expected a null_reasons object");
    }

    return failures;
};

static IEnumerable<string> UnpinnedReleaseNotesAssertions(RunResult result)
{
    if (result.ExitCode != 0)
    {
        yield return $"expected exit code 0, got {result.ExitCode}";
    }

    if (result.Ledger is null)
    {
        yield return "expected the ledger to parse as one JSON object, but no valid ledger was found";
        yield break;
    }

    var root = result.Ledger.RootElement;
    if (!root.TryGetProperty("pinned", out var pinned) || pinned.ValueKind != JsonValueKind.False)
    {
        yield return "expected pinned false";
    }
}

static Func<RunResult, IEnumerable<string>> Combine(params Func<RunResult, IEnumerable<string>>[] checks) =>
    result => checks.SelectMany(check => check(result));

// T3.5: requirements.md's declared ids and each declaration's paragraph windows.
static Func<RunResult, IEnumerable<string>> DeclaredIdAssertions(
    string repositoryRoot,
    string target,
    int expectedDeclaredTotal,
    string[]? expectedDeclaredIds = null,
    IReadOnlyList<DeclaredIdExpectation>? expectedDeclarations = null) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("requirements", out var requirements) || requirements.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a requirements object");
        return failures;
    }

    if (!requirements.TryGetProperty("present", out var present) || present.ValueKind != JsonValueKind.True)
    {
        failures.Add("expected requirements.present true");
    }

    var requirementsPath = Path.Combine(repositoryRoot, target, "requirements.md");
    var expectedBytes = new FileInfo(requirementsPath).Length;

    if (!requirements.TryGetProperty("bytes", out var reqBytes) || reqBytes.GetInt64() != expectedBytes)
    {
        failures.Add($"expected requirements.bytes {expectedBytes}");
    }

    if (!requirements.TryGetProperty("windows", out var reqWindows) || reqWindows.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a requirements.windows array");
    }
    else
    {
        failures.AddRange(AssertContiguousWindows(reqWindows, expectedBytes));
    }

    if (!root.TryGetProperty("declared_total", out var declaredTotal) || declaredTotal.GetInt32() != expectedDeclaredTotal)
    {
        failures.Add($"expected declared_total {expectedDeclaredTotal}");
    }

    if (!root.TryGetProperty("declared_ids", out var declaredIdsEl) || declaredIdsEl.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a declared_ids array");
    }
    else
    {
        if (declaredIdsEl.GetArrayLength() != expectedDeclaredTotal)
        {
            failures.Add($"expected declared_ids to have {expectedDeclaredTotal} entries, got {declaredIdsEl.GetArrayLength()}");
        }

        if (expectedDeclaredIds is not null)
        {
            var actualIds = declaredIdsEl.EnumerateArray().Select(e => e.GetString()).ToArray();
            if (!actualIds.SequenceEqual(expectedDeclaredIds))
            {
                failures.Add($"expected declared_ids [{string.Join(", ", expectedDeclaredIds)}], got [{string.Join(", ", actualIds)}]");
            }
        }

        failures.AddRange(AssertFrThenNfrOrder(declaredIdsEl));
    }

    if (!root.TryGetProperty("declarations", out var declarations) || declarations.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a declarations array");
    }
    else
    {
        if (declarations.GetArrayLength() != expectedDeclaredTotal)
        {
            failures.Add($"expected {expectedDeclaredTotal} declarations entries, got {declarations.GetArrayLength()}");
        }

        if (expectedDeclarations is not null)
        {
            foreach (var expected in expectedDeclarations)
            {
                var entry = declarations.EnumerateArray()
                    .FirstOrDefault(e => e.TryGetProperty("id", out var idEl) && idEl.GetString() == expected.Id);

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    failures.Add($"expected a declarations entry for {expected.Id}");
                    continue;
                }

                if (!entry.TryGetProperty("bytes", out var declBytes) || declBytes.GetInt64() != expected.Bytes)
                {
                    failures.Add($"expected declarations[{expected.Id}].bytes {expected.Bytes}");
                }

                if (!entry.TryGetProperty("windows", out var declWindows) || declWindows.ValueKind != JsonValueKind.Array)
                {
                    failures.Add($"expected a declarations[{expected.Id}].windows array");
                    continue;
                }

                if (declWindows.GetArrayLength() != 1)
                {
                    failures.Add($"expected declarations[{expected.Id}] to be exactly one window, got {declWindows.GetArrayLength()}");
                }
                else
                {
                    var window = declWindows[0];
                    if (window.GetProperty("first_line").GetInt64() != expected.FirstLine
                        || window.GetProperty("last_line").GetInt64() != expected.LastLine)
                    {
                        failures.Add(
                            $"expected declarations[{expected.Id}] to run lines {expected.FirstLine}-{expected.LastLine}, got "
                            + $"{window.GetProperty("first_line").GetInt64()}-{window.GetProperty("last_line").GetInt64()}");
                    }
                }

                failures.AddRange(AssertContiguousWindows(declWindows, expected.Bytes, startLine: expected.FirstLine));
            }
        }
    }

    return failures;
};

// A window list must be contiguous from its first line, each non-oversize window at most
// 25,000 B, and its bytes must sum to the expected total (ADR 0072 IA 4's shared window helper).
static IEnumerable<string> AssertContiguousWindows(JsonElement windows, long expectedTotalBytes, long startLine = 1)
{
    long sum = 0;
    long expectedNextFirstLine = startLine;
    foreach (var window in windows.EnumerateArray())
    {
        var firstLine = window.GetProperty("first_line").GetInt64();
        var lastLine = window.GetProperty("last_line").GetInt64();
        var windowBytes = window.GetProperty("bytes").GetInt64();
        var oversize = window.GetProperty("oversize").GetBoolean();

        if (firstLine != expectedNextFirstLine)
        {
            yield return $"expected a window to start at line {expectedNextFirstLine}, got {firstLine}";
        }

        if (!oversize && windowBytes > 25_000)
        {
            yield return $"expected a non-oversize window to be at most 25,000 B, got {windowBytes}";
        }

        sum += windowBytes;
        expectedNextFirstLine = lastLine + 1;
    }

    if (sum != expectedTotalBytes)
    {
        yield return $"expected windows' bytes to sum to {expectedTotalBytes}, got {sum}";
    }
}

// FR-then-NFR order: every FR id before any NFR id, ascending numerically within each prefix.
static IEnumerable<string> AssertFrThenNfrOrder(JsonElement declaredIds)
{
    var ids = declaredIds.EnumerateArray().Select(e => e.GetString()!).ToArray();
    var seenNfr = false;
    var previousNumber = -1;
    var previousIsNfr = false;

    foreach (var id in ids)
    {
        var isNfr = id.StartsWith("NFR-", StringComparison.Ordinal);
        if (isNfr)
        {
            seenNfr = true;
        }
        else if (seenNfr)
        {
            yield return $"expected every FR id before any NFR id in declared_ids, but found {id} after an NFR id";
        }

        var numberPart = id[(isNfr ? 4 : 3)..];
        var number = int.Parse(numberPart);

        if (isNfr == previousIsNfr && number <= previousNumber)
        {
            yield return $"expected declared_ids to be in ascending numeric order within each prefix, but {id} did not increase past {previousNumber}";
        }

        previousNumber = number;
        previousIsNfr = isNfr;
    }
}

// T4.1: release_notes' present/count/m and each marked section's windows.
static Func<RunResult, IEnumerable<string>> ReleaseNotesAssertions(
    string repositoryRoot,
    string releaseNotesPath,
    bool expectedPresent,
    int expectedCount,
    int? expectedM,
    IReadOnlyList<ReleaseNotesSectionExpectation>? expectedSections = null) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("release_notes", out var releaseNotes) || releaseNotes.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a release_notes object");
        return failures;
    }

    if (!releaseNotes.TryGetProperty("path", out var pathEl) || pathEl.GetString() != releaseNotesPath)
    {
        failures.Add($"expected release_notes.path {releaseNotesPath}");
    }

    var expectedPresentKind = expectedPresent ? JsonValueKind.True : JsonValueKind.False;
    if (!releaseNotes.TryGetProperty("present", out var presentEl) || presentEl.ValueKind != expectedPresentKind)
    {
        failures.Add($"expected release_notes.present {expectedPresent}");
    }

    if (!releaseNotes.TryGetProperty("count", out var countEl) || countEl.GetInt32() != expectedCount)
    {
        failures.Add($"expected release_notes.count {expectedCount}");
    }

    if (expectedM is null)
    {
        if (!releaseNotes.TryGetProperty("m", out var mNullEl) || mNullEl.ValueKind != JsonValueKind.Null)
        {
            failures.Add("expected release_notes.m to be null");
        }
    }
    else if (!releaseNotes.TryGetProperty("m", out var mEl) || mEl.GetInt32() != expectedM.Value)
    {
        failures.Add($"expected release_notes.m {expectedM.Value}");
    }

    if (!releaseNotes.TryGetProperty("sections", out var sectionsEl) || sectionsEl.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a release_notes.sections array");
        return failures;
    }

    if (sectionsEl.GetArrayLength() != expectedCount)
    {
        failures.Add($"expected release_notes.sections to have {expectedCount} entries, got {sectionsEl.GetArrayLength()}");
    }

    if (expectedSections is not null)
    {
        var actualSections = sectionsEl.EnumerateArray().ToArray();
        for (var i = 0; i < expectedSections.Count && i < actualSections.Length; i++)
        {
            var expected = expectedSections[i];
            var actual = actualSections[i];

            if (!actual.TryGetProperty("bytes", out var bytesEl) || bytesEl.GetInt64() != expected.Bytes)
            {
                failures.Add($"expected release_notes.sections[{i}].bytes {expected.Bytes}");
            }

            if (!actual.TryGetProperty("windows", out var windowsEl) || windowsEl.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"expected a release_notes.sections[{i}].windows array");
                continue;
            }

            if (windowsEl.GetArrayLength() != 1)
            {
                failures.Add($"expected release_notes.sections[{i}] to be exactly one window, got {windowsEl.GetArrayLength()}");
            }
            else
            {
                var window = windowsEl[0];
                if (window.GetProperty("first_line").GetInt64() != expected.FirstLine
                    || window.GetProperty("last_line").GetInt64() != expected.LastLine)
                {
                    failures.Add(
                        $"expected release_notes.sections[{i}] to run lines {expected.FirstLine}-{expected.LastLine}, got "
                        + $"{window.GetProperty("first_line").GetInt64()}-{window.GetProperty("last_line").GetInt64()}");
                }
            }

            failures.AddRange(AssertContiguousWindows(windowsEl, expected.Bytes, startLine: expected.FirstLine));
        }
    }

    return failures;
};

// T5.1: pinned diff fields — buckets, net lines, src_subdirectory_count, public_api_lines,
// commits and src_diff's command/bytes/windows. These depend only on the pinned pair, so the
// same expected values apply whatever the target is.
static Func<RunResult, IEnumerable<string>> PinnedDiffAssertions(
    string mergeBase,
    string measuredHead,
    IReadOnlyDictionary<string, int> expectedBucketFiles,
    long expectedTotalAdded,
    long expectedTotalRemoved,
    long expectedSrcAdded,
    long expectedSrcRemoved,
    int expectedSrcSubdirectoryCount,
    int expectedPublicApiLines,
    int expectedCommits,
    long expectedSrcDiffBytes) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a buckets object");
        return failures;
    }

    foreach (var (name, expectedFiles) in expectedBucketFiles)
    {
        if (!buckets.TryGetProperty(name, out var bucket) || bucket.ValueKind != JsonValueKind.Object)
        {
            failures.Add($"expected a buckets.{name} object");
            continue;
        }

        if (!bucket.TryGetProperty("files", out var filesEl) || filesEl.GetInt32() != expectedFiles)
        {
            failures.Add($"expected buckets.{name}.files {expectedFiles}");
        }
    }

    if (!buckets.TryGetProperty("total", out var total) || total.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a buckets.total object");
    }
    else
    {
        if (!total.TryGetProperty("added", out var totalAdded) || totalAdded.GetInt64() != expectedTotalAdded)
        {
            failures.Add($"expected buckets.total.added {expectedTotalAdded}");
        }

        if (!total.TryGetProperty("removed", out var totalRemoved) || totalRemoved.GetInt64() != expectedTotalRemoved)
        {
            failures.Add($"expected buckets.total.removed {expectedTotalRemoved}");
        }
    }

    if (!buckets.TryGetProperty("src", out var src) || src.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a buckets.src object");
    }
    else
    {
        if (!src.TryGetProperty("added", out var srcAdded) || srcAdded.GetInt64() != expectedSrcAdded)
        {
            failures.Add($"expected buckets.src.added {expectedSrcAdded}");
        }

        if (!src.TryGetProperty("removed", out var srcRemoved) || srcRemoved.GetInt64() != expectedSrcRemoved)
        {
            failures.Add($"expected buckets.src.removed {expectedSrcRemoved}");
        }
    }

    if (!root.TryGetProperty("src_subdirectory_count", out var subdirEl) || subdirEl.GetInt32() != expectedSrcSubdirectoryCount)
    {
        failures.Add($"expected src_subdirectory_count {expectedSrcSubdirectoryCount}");
    }

    if (!root.TryGetProperty("public_api_lines", out var apiEl) || apiEl.GetInt32() != expectedPublicApiLines)
    {
        failures.Add($"expected public_api_lines {expectedPublicApiLines}");
    }

    if (!root.TryGetProperty("commits", out var commitsEl) || commitsEl.GetInt32() != expectedCommits)
    {
        failures.Add($"expected commits {expectedCommits}");
    }

    if (!root.TryGetProperty("src_diff", out var srcDiff) || srcDiff.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a src_diff object");
        return failures;
    }

    var command = srcDiff.TryGetProperty("command", out var commandEl) && commandEl.ValueKind == JsonValueKind.String
        ? commandEl.GetString()
        : null;
    if (command is null
        || !command.Contains(mergeBase, StringComparison.Ordinal)
        || !command.Contains(measuredHead, StringComparison.Ordinal)
        || !command.Contains("src/", StringComparison.Ordinal))
    {
        failures.Add($"expected src_diff.command to name {mergeBase}, {measuredHead} and the src/ pathspec, got \"{command}\"");
    }

    if (!srcDiff.TryGetProperty("bytes", out var srcDiffBytesEl) || srcDiffBytesEl.GetInt64() != expectedSrcDiffBytes)
    {
        failures.Add($"expected src_diff.bytes {expectedSrcDiffBytes}");
    }

    if (!srcDiff.TryGetProperty("windows", out var srcDiffWindows) || srcDiffWindows.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a src_diff.windows array");
    }
    else
    {
        failures.AddRange(AssertContiguousWindows(srcDiffWindows, expectedSrcDiffBytes));
    }

    return failures;
};

// T5.1: the calibration ledger's size is a first measurement of ADR 0072's under-20 KB estimate
// and a risk check on the 65,536 B cap's headroom — printed, not just asserted.
static Func<RunResult, IEnumerable<string>> LedgerSizeAssertion(string label, long maxBytes) => result =>
{
    var size = result.LedgerBytes?.Length ?? -1;
    Console.WriteLine($"INFO {label}: ledger is {size} B (cap {maxBytes} B)");

    return size >= 0 && size <= maxBytes
        ? []
        : [$"expected the {label} ledger to be at most {maxBytes} B, got {size}"];
};

// T5.2: f1_level applies FR-11's thresholds (Low <=10, Medium 11-50, High >50) to buckets.src.files.
static Func<RunResult, IEnumerable<string>> F1LevelAssertion(string expectedLevel, int expectedSrcFiles) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("f1_level", out var levelEl) || levelEl.GetString() != expectedLevel)
    {
        failures.Add($"expected f1_level {expectedLevel}");
    }

    if (!root.TryGetProperty("buckets", out var buckets)
        || !buckets.TryGetProperty("src", out var src)
        || !src.TryGetProperty("files", out var filesEl)
        || filesEl.GetInt32() != expectedSrcFiles)
    {
        failures.Add($"expected buckets.src.files {expectedSrcFiles}");
    }

    return failures;
};

// T3.6: .adr-list entry resolution, each resolved entry's extract, and adr_resolved_count.
static Func<RunResult, IEnumerable<string>> AdrListAssertions(
    IReadOnlyList<AdrListEntryExpectation> expectedEntries,
    int expectedResolvedCount) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("adr_resolved_count", out var resolvedCountEl) || resolvedCountEl.GetInt32() != expectedResolvedCount)
    {
        failures.Add($"expected adr_resolved_count {expectedResolvedCount}");
    }

    if (!root.TryGetProperty("adr_list", out var adrList) || adrList.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected an adr_list array");
        return failures;
    }

    if (adrList.GetArrayLength() != expectedEntries.Count)
    {
        failures.Add($"expected adr_list to have {expectedEntries.Count} entries, got {adrList.GetArrayLength()}");
    }

    var actualEntries = adrList.EnumerateArray().ToArray();
    for (var i = 0; i < expectedEntries.Count && i < actualEntries.Length; i++)
    {
        var expected = expectedEntries[i];
        var actual = actualEntries[i];

        var actualEntry = actual.TryGetProperty("entry", out var entryEl) && entryEl.ValueKind == JsonValueKind.String
            ? entryEl.GetString()
            : null;
        if (actualEntry != expected.Entry)
        {
            failures.Add($"expected adr_list[{i}].entry \"{expected.Entry}\", got \"{actualEntry}\"");
        }

        var actualPath = actual.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String
            ? pathEl.GetString()
            : null;
        if (actualPath != expected.Path)
        {
            failures.Add($"expected adr_list[{expected.Entry}].path {expected.Path ?? "null"}, got {actualPath ?? "null"}");
        }

        var actualReason = actual.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
            ? reasonEl.GetString()
            : null;
        if (actualReason != expected.Reason)
        {
            failures.Add($"expected adr_list[{expected.Entry}].reason {expected.Reason ?? "null"}, got {actualReason ?? "null"}");
        }

        if (expected.Matches is null)
        {
            if (actual.TryGetProperty("matches", out var matchesEl) && matchesEl.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"expected adr_list[{expected.Entry}].matches to be null");
            }
        }
        else if (!actual.TryGetProperty("matches", out var matchesEl) || matchesEl.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"expected adr_list[{expected.Entry}].matches to be an array");
        }
        else
        {
            var actualMatches = matchesEl.EnumerateArray().Select(e => e.GetString()).ToArray();
            if (!actualMatches.SequenceEqual(expected.Matches))
            {
                failures.Add(
                    $"expected adr_list[{expected.Entry}].matches [{string.Join(", ", expected.Matches)}], "
                    + $"got [{string.Join(", ", actualMatches)}]");
            }
        }

        if (!expected.ExpectExtract)
        {
            if (!actual.TryGetProperty("extract", out var noExtractEl) || noExtractEl.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"expected adr_list[{expected.Entry}].extract to be null (entry did not resolve)");
            }

            continue;
        }

        if (!actual.TryGetProperty("extract", out var extractEl) || extractEl.ValueKind != JsonValueKind.Array
            || extractEl.GetArrayLength() != 3)
        {
            failures.Add($"expected adr_list[{expected.Entry}].extract to hold 3 parts (front matter, Status, Consequences)");
            continue;
        }

        foreach (var part in extractEl.EnumerateArray())
        {
            if (!part.TryGetProperty("bytes", out var bytesEl) || bytesEl.GetInt64() <= 0)
            {
                failures.Add($"expected every adr_list[{expected.Entry}].extract part to have bytes > 0");
            }

            if (!part.TryGetProperty("windows", out var windowsEl) || windowsEl.ValueKind != JsonValueKind.Array
                || windowsEl.GetArrayLength() == 0)
            {
                failures.Add($"expected every adr_list[{expected.Entry}].extract part to have a non-empty windows array");
            }
        }
    }

    return failures;
};

// Used where only the resolved count (not each entry's detail) is asserted, e.g. the calibration
// row's 7-entry .adr-list.
static Func<RunResult, IEnumerable<string>> AdrResolvedCountAssertions(int expectedResolvedCount) => result =>
{
    var failures = new List<string>();

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("adr_resolved_count", out var resolvedCountEl) || resolvedCountEl.GetInt32() != expectedResolvedCount)
    {
        failures.Add($"expected adr_resolved_count {expectedResolvedCount}");
    }

    if (!root.TryGetProperty("adr_list", out var adrList) || adrList.ValueKind != JsonValueKind.Array
        || adrList.GetArrayLength() != expectedResolvedCount)
    {
        failures.Add($"expected adr_list to have {expectedResolvedCount} entries, all resolved");
    }

    return failures;
};

static Func<RunResult, IEnumerable<string>> TasksFieldAssertions(
    string repositoryRoot,
    string target,
    int total,
    int uncheckedCount,
    IReadOnlyDictionary<string, int> byTag,
    bool assertLiteralTestPlusImplement = false) => result =>
{
    var failures = new List<string>();

    if (result.ExitCode != 0)
    {
        failures.Add($"expected exit code 0, got {result.ExitCode}");
    }

    if (result.Ledger is null)
    {
        failures.Add("expected the ledger to parse as one JSON object, but no valid ledger was found");
        return failures;
    }

    var root = result.Ledger.RootElement;

    if (!root.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a tasks object");
        return failures;
    }

    if (!tasks.TryGetProperty("total", out var totalEl) || totalEl.GetInt32() != total)
    {
        failures.Add($"expected tasks.total {total}");
    }

    if (!tasks.TryGetProperty("unchecked", out var uncheckedEl) || uncheckedEl.GetInt32() != uncheckedCount)
    {
        failures.Add($"expected tasks.unchecked {uncheckedCount}");
    }

    if (!tasks.TryGetProperty("by_tag", out var byTagEl) || byTagEl.ValueKind != JsonValueKind.Object)
    {
        failures.Add("expected a tasks.by_tag object");
    }
    else
    {
        foreach (var (tag, expectedCount) in byTag)
        {
            if (!byTagEl.TryGetProperty(tag, out var v) || v.GetInt32() != expectedCount)
            {
                failures.Add($"expected tasks.by_tag[{tag}] = {expectedCount}");
            }
        }

        var sum = byTagEl.EnumerateObject().Sum(p => p.Value.GetInt32());
        if (sum != total)
        {
            failures.Add($"expected tasks.by_tag counts to sum to {total}, got {sum}");
        }
    }

    var tasksPath = Path.Combine(repositoryRoot, target, "tasks.md");
    var expectedBytes = new FileInfo(tasksPath).Length;

    if (!tasks.TryGetProperty("bytes", out var bytesEl) || bytesEl.GetInt64() != expectedBytes)
    {
        failures.Add($"expected tasks.bytes {expectedBytes}");
    }

    if (!tasks.TryGetProperty("windows", out var windows) || windows.ValueKind != JsonValueKind.Array)
    {
        failures.Add("expected a tasks.windows array");
    }
    else
    {
        long sum = 0;
        long expectedNextFirstLine = 1;
        foreach (var window in windows.EnumerateArray())
        {
            var firstLine = window.GetProperty("first_line").GetInt64();
            var lastLine = window.GetProperty("last_line").GetInt64();
            var windowBytes = window.GetProperty("bytes").GetInt64();
            var oversize = window.GetProperty("oversize").GetBoolean();

            if (firstLine != expectedNextFirstLine)
            {
                failures.Add($"expected a window to start at line {expectedNextFirstLine}, got {firstLine}");
            }

            if (!oversize && windowBytes > 25_000)
            {
                failures.Add($"expected a non-oversize window to be at most 25,000 B, got {windowBytes}");
            }

            sum += windowBytes;
            expectedNextFirstLine = lastLine + 1;
        }

        if (sum != expectedBytes)
        {
            failures.Add($"expected tasks.windows' bytes to sum to {expectedBytes}, got {sum}");
        }
    }

    if (assertLiteralTestPlusImplement
        && (result.LedgerBytes is null
            || !Encoding.UTF8.GetString(result.LedgerBytes).Contains("TEST + IMPLEMENT", StringComparison.Ordinal)))
    {
        failures.Add("expected the raw ledger text to contain \"TEST + IMPLEMENT\" literally");
    }

    return failures;
};

static IEnumerable<string> AssertAllNull(JsonElement root, string[] fields, string reason)
{
    if (!root.TryGetProperty("null_reasons", out var nullReasons) || nullReasons.ValueKind != JsonValueKind.Object)
    {
        yield return "expected a null_reasons object";
        yield break;
    }

    foreach (var field in fields)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Null)
        {
            yield return $"expected {field} to be null";
        }

        if (!nullReasons.TryGetProperty(field, out var reasonValue) || reasonValue.GetString() != reason)
        {
            yield return $"expected null_reasons.{field} to read \"{reason}\"";
        }
    }
}

static Func<RunResult, IEnumerable<string>> GateFailureAssertions(Action<JsonElement, List<string>> checkRecord) => result =>
{
    var failures = new List<string>();

    if (result.ExitCode != 2)
    {
        failures.Add($"expected exit code 2, got {result.ExitCode}");
    }

    if (result.LedgerBytes is not null)
    {
        failures.Add("expected no ledger to be written");
    }

    if (result.GateRecord is null)
    {
        failures.Add("expected a show-me-gate: stderr record that parses as one JSON object");
        return failures;
    }

    checkRecord(result.GateRecord.RootElement, failures);

    return failures;
};

static int CheckPlantedLedgerUntouchedOnGateFailure(string repositoryRoot, string target)
{
    var ledgerFullPath = Path.Combine(repositoryRoot, target, ".show-me-ledger.json");
    var dummy = Encoding.UTF8.GetBytes("{\"dummy\":true}");
    File.WriteAllBytes(ledgerFullPath, dummy);

    var (exitCode, _) = InvokeScript(repositoryRoot, [target]);
    var afterwards = File.Exists(ledgerFullPath) ? File.ReadAllBytes(ledgerFullPath) : null;

    var failures = 0;
    if (exitCode != 2)
    {
        Console.WriteLine($"FAIL unfinished (planted ledger): expected exit code 2, got {exitCode}");
        failures++;
    }

    if (afterwards is null || !afterwards.SequenceEqual(dummy))
    {
        Console.WriteLine("FAIL unfinished (planted ledger): expected the planted ledger to be left byte-identical (AC-71)");
        failures++;
    }

    File.Delete(ledgerFullPath);
    return failures;
}

static int CheckLedgerLeftAsFound(Row row, string repositoryRoot)
{
    var failures = 0;
    var ledgerFullPath = Path.Combine(repositoryRoot, row.LedgerTarget!, ".show-me-ledger.json");

    // With nothing planted before the row above ran, no ledger should remain now.
    if (File.Exists(ledgerFullPath))
    {
        Console.WriteLine($"FAIL {row.Name} (harness): expected no ledger to remain when none was planted");
        failures++;
    }

    // A planted ledger is restored byte-identically afterwards (AC-79).
    var dummy = Encoding.UTF8.GetBytes("{\"dummy\":true}");
    File.WriteAllBytes(ledgerFullPath, dummy);
    failures += RunRow(row with { Name = "declared (harness restore check)" }, repositoryRoot);
    var afterwards = File.Exists(ledgerFullPath) ? File.ReadAllBytes(ledgerFullPath) : null;
    if (afterwards is null || !afterwards.SequenceEqual(dummy))
    {
        Console.WriteLine($"FAIL {row.Name} (harness): expected the planted ledger to be restored byte-identically");
        failures++;
    }
    File.Delete(ledgerFullPath);

    return failures;
}

sealed record RunResult(int ExitCode, byte[]? LedgerBytes, JsonDocument? Ledger, JsonDocument? GateRecord);

sealed record Row(string Name, string[] Args, string? LedgerTarget, Func<RunResult, IEnumerable<string>> Assertions);

sealed record DeclaredIdExpectation(string Id, int FirstLine, int LastLine, int Bytes);

sealed record AdrListEntryExpectation(string Entry, string? Path, string? Reason, string[]? Matches, bool ExpectExtract);

sealed record ReleaseNotesSectionExpectation(int FirstLine, int LastLine, int Bytes);
