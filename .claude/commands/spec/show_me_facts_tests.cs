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
const string ReleaseNotesFixture = ".claude/test-fixtures/show-me/release-notes.md";
const string CalibrationTarget = "specs/0036-scoped-lifetime-per-pipeline";
const string CalibrationMergeBase = "6145913a0";
const string CalibrationMeasuredHead = "91d549be6";

var declaredRow = new Row(
    Name: "declared",
    Args: [DeclaredTarget],
    LedgerTarget: DeclaredTarget,
    Assertions: DeclaredAssertions);

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
        PinnedAssertions(DeclaredTarget, CalibrationMergeBase, CalibrationMeasuredHead)),
    new Row(
        "declared pinned + release-notes (release-notes first)",
        [DeclaredTarget, "--release-notes", ReleaseNotesFixture, "--pinned", CalibrationMergeBase, CalibrationMeasuredHead],
        DeclaredTarget,
        PinnedAssertions(DeclaredTarget, CalibrationMergeBase, CalibrationMeasuredHead)),
    new Row(
        "declared release-notes only, unpinned",
        [DeclaredTarget, "--release-notes", ReleaseNotesFixture],
        DeclaredTarget,
        UnpinnedReleaseNotesAssertions),
    new Row(
        "calibration pinned",
        [CalibrationTarget, "--pinned", CalibrationMergeBase, CalibrationMeasuredHead],
        CalibrationTarget,
        PinnedAssertions(CalibrationTarget, CalibrationMergeBase, CalibrationMeasuredHead)),
};

var failedAssertions = 0;
foreach (var row in rows)
{
    failedAssertions += RunRow(row, repositoryRoot);
}

failedAssertions += CheckLedgerLeftAsFound(declaredRow, repositoryRoot);

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

    var exitCode = InvokeScript(repositoryRoot, row.Args);

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

    var failures = 0;
    foreach (var message in row.Assertions(new RunResult(exitCode, ledgerBytes, ledger)))
    {
        Console.WriteLine($"FAIL {row.Name}: {message}");
        failures++;
    }

    ledger?.Dispose();

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

static int InvokeScript(string repositoryRoot, string[] scriptArgs)
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
    // stdout is never parsed — only the process exit code and the ledger file are asserted on.
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("could not start dotnet");
    process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode;
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

sealed record RunResult(int ExitCode, byte[]? LedgerBytes, JsonDocument? Ledger);

sealed record Row(string Name, string[] Args, string? LedgerTarget, Func<RunResult, IEnumerable<string>> Assertions);
