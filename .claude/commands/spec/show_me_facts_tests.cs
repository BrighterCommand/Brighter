#nullable enable
using System.Diagnostics;
using System.Text.Json;

// The sibling test script for show_me_facts.cs (NFR-9). No test framework: it is a plain
// file-based app that shells out to the measurement script for each row below, checks the
// row's assertions against the process exit code and the ledger it wrote, and reports.

var repositoryRoot = Directory.GetCurrentDirectory();

var rows = new[]
{
    new Row(
        Name: "declared",
        Target: ".claude/test-fixtures/show-me/declared",
        LedgerPath: ".claude/test-fixtures/show-me/declared/.show-me-ledger.json")
};

var failedAssertions = 0;
foreach (var row in rows)
{
    failedAssertions += RunRow(row, repositoryRoot);
}

Console.WriteLine(failedAssertions == 0
    ? $"{rows.Length} row(s) passed."
    : $"{failedAssertions} failed assertion(s) across {rows.Length} row(s).");

return failedAssertions == 0 ? 0 : 1;

static int RunRow(Row row, string repositoryRoot)
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
    startInfo.ArgumentList.Add(row.Target);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"row {row.Name}: could not start dotnet");

    // Both streams are captured so the child process never blocks on a full pipe buffer.
    // stdout is never parsed — only the process exit code and the ledger file are asserted on.
    process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();

    var failures = 0;

    if (process.ExitCode != 0)
    {
        Console.WriteLine($"FAIL {row.Name}: expected exit code 0, got {process.ExitCode}");
        failures++;
    }

    var ledgerFullPath = Path.Combine(repositoryRoot, row.LedgerPath);
    if (!File.Exists(ledgerFullPath))
    {
        Console.WriteLine($"FAIL {row.Name}: expected a ledger at {row.LedgerPath}, none was written");
        failures++;
    }
    else
    {
        try
        {
            using var ledger = JsonDocument.Parse(File.ReadAllText(ledgerFullPath));
            if (ledger.RootElement.ValueKind != JsonValueKind.Object)
            {
                Console.WriteLine($"FAIL {row.Name}: expected the ledger to parse as one JSON object, got {ledger.RootElement.ValueKind}");
                failures++;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"FAIL {row.Name}: expected the ledger to parse as one JSON object, but parsing failed: {ex.Message}");
            failures++;
        }
    }

    return failures;
}

sealed record Row(string Name, string Target, string LedgerPath);
