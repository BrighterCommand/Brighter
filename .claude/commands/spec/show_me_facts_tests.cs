#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

// The sibling test script for show_me_facts.cs (NFR-9). No test framework: it is a plain
// file-based app that shells out to the measurement script for each row below, checks the
// row's assertions against the process exit code and the ledger it wrote, and reports.

var repositoryRoot = Directory.GetCurrentDirectory();

var declaredRow = new Row(
    Name: "declared",
    Target: ".claude/test-fixtures/show-me/declared",
    ExtraArgs: [],
    Assertions: DeclaredAssertions);

var rows = new[] { declaredRow };

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
    var ledgerFullPath = Path.Combine(repositoryRoot, row.Target, ".show-me-ledger.json");

    // NFR-9: the test script leaves every target's ledger as it found it.
    var preExisting = File.Exists(ledgerFullPath) ? File.ReadAllBytes(ledgerFullPath) : null;

    var exitCode = InvokeScript(repositoryRoot, row.Target, row.ExtraArgs);

    var ledgerBytes = File.Exists(ledgerFullPath) ? File.ReadAllBytes(ledgerFullPath) : null;
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

    if (preExisting is not null)
    {
        File.WriteAllBytes(ledgerFullPath, preExisting);
    }
    else if (File.Exists(ledgerFullPath))
    {
        File.Delete(ledgerFullPath);
    }

    return failures;
}

static int InvokeScript(string repositoryRoot, string target, string[] extraArgs)
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
    startInfo.ArgumentList.Add(target);
    foreach (var extraArg in extraArgs)
    {
        startInfo.ArgumentList.Add(extraArg);
    }

    // Both streams are captured so the child process never blocks on a full pipe buffer.
    // stdout is never parsed — only the process exit code and the ledger file are asserted on.
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"could not start dotnet for target {target}");
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

    if (!root.TryGetProperty("null_reasons", out var nullReasons) || nullReasons.ValueKind != JsonValueKind.Object)
    {
        yield return "expected a null_reasons object";
    }
    else
    {
        foreach (var field in refAndDiffFields)
        {
            if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Null)
            {
                yield return $"expected {field} to be null";
            }

            if (!nullReasons.TryGetProperty(field, out var reason) || reason.GetString() != "not a spec directory")
            {
                yield return $"expected null_reasons.{field} to read \"not a spec directory\"";
            }
        }
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

static int CheckLedgerLeftAsFound(Row row, string repositoryRoot)
{
    var failures = 0;
    var ledgerFullPath = Path.Combine(repositoryRoot, row.Target, ".show-me-ledger.json");

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

sealed record Row(string Name, string Target, string[] ExtraArgs, Func<RunResult, IEnumerable<string>> Assertions);
