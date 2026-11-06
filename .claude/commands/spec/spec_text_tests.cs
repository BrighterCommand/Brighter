#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Text;

// The text-comparison script for the routing-slip canonical texts and their copies (spec 0040,
// ADR 0092). No test framework: a plain file-based app that reads the files under
// .claude/commands/, compares each registered block with its canonical text, reports one FAIL
// line per failed assertion and exits 1 when anything failed.

var repositoryRoot = Directory.GetCurrentDirectory();

var registry = new List<RoutingTextBlock>
{
    new(
        "adr-selection-rule",
        ".claude/commands/adr/README.md",
        [
            ".claude/commands/adr/accept.md",
            ".claude/commands/adr/retire-superseded.md",
            ".claude/commands/adr/report-approval.md"
        ])
};

var templateRegistry = new List<TemplateExpectation>
{
    new(".claude/commands/adr/README.md", ".claude/commands/adr/retire-superseded.md", ["Supersession question"]),
    new(".claude/commands/adr/README.md", ".claude/commands/adr/report-approval.md", ["Report"])
};

var reporter = new Reporter();

CheckParserSelfTest(reporter);

CheckIgnoreRules(repositoryRoot, reporter);

foreach (var block in registry)
    CheckBlock(block, repositoryRoot, reporter);

foreach (var expectation in templateRegistry)
    CheckTemplates(expectation, repositoryRoot, reporter);

return reporter.Summary();

void CheckBlock(RoutingTextBlock block, string root, Reporter report)
{
    var canonical = ReadBlocks(root, block.CanonicalFile, block.Id);
    report.Checked();
    if (canonical.Count != 1)
    {
        report.Fail(block.Id, canonical.Count == 0
            ? $"canonical block missing from {block.CanonicalFile}"
            : $"canonical block not unique in {block.CanonicalFile} ({canonical.Count} found)");
    }

    var canonicalText = canonical.Count == 1 ? canonical[0] : null;
    foreach (var file in CopyFiles(block, root))
        CheckCopy(block, canonicalText, file, root, report);
}

void CheckCopy(RoutingTextBlock block, string? canonicalText, string file, string root, Reporter report)
{
    var copies = ReadBlocks(root, file, block.Id);
    report.Checked();
    if (copies.Count == 0)
        report.Fail(block.Id, $"copy missing from {file}");
    else if (copies.Count > 1)
        report.Fail(block.Id, $"copy duplicated in {file} ({copies.Count} found)");
    else if (canonicalText != null && copies[0] != canonicalText)
        report.Fail(block.Id, $"copy in {file} differs from the canonical block in {block.CanonicalFile}");
}

IEnumerable<string> CopyFiles(RoutingTextBlock block, string root) =>
    block.RequiredCopies.Concat(FilesCarryingMarker(root, block.Id))
        .Where(file => file != block.CanonicalFile)
        .Distinct();

IEnumerable<string> FilesCarryingMarker(string root, string id)
{
    var commands = Path.Combine(root, ".claude", "commands");
    if (!Directory.Exists(commands))
        return [];

    return Directory.EnumerateFiles(commands, "*.md", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
        .Where(file => ReadBlocks(root, file, id).Count > 0);
}

List<string> ReadBlocks(string root, string file, string id)
{
    var path = Path.Combine(root, file);
    if (!File.Exists(path))
        return [];

    var text = File.ReadAllText(path).Replace("\r\n", "\n");
    var begin = $"<!-- routing-text:{id}:begin -->";
    var end = $"<!-- routing-text:{id}:end -->";
    var blocks = new List<string>();
    for (var start = text.IndexOf(begin, StringComparison.Ordinal); start >= 0;
         start = text.IndexOf(begin, start + begin.Length, StringComparison.Ordinal))
    {
        var contentStart = start + begin.Length;
        var stop = text.IndexOf(end, contentStart, StringComparison.Ordinal);
        if (stop < 0)
            break;
        blocks.Add(text[contentStart..stop]);
    }

    return blocks;
}

void CheckIgnoreRules(string root, Reporter report)
{
    const int Ignored = 0;
    const int NotIgnored = 1;
    var probes = new (string Path, int ExpectedExit)[]
    {
        ("specs/9999-check ignore probe/.flow", NotIgnored),
        ("specs/9999-check ignore probe/.flow.record", NotIgnored),
        ("specs/9999-check ignore probe/route-changes.md", NotIgnored),
        ("specs/9999-probe.log/.flow", Ignored)
    };

    foreach (var (path, expectedExit) in probes)
    {
        report.Checked();
        var exit = RunGit(root, "check-ignore", "-q", "--no-index", path);
        if (exit != expectedExit)
            report.Fail("ignore check", $"git check-ignore -q --no-index \"{path}\" exited {exit}, expected {expectedExit}");
    }
}

void CheckParserSelfTest(Reporter report)
{
    var rows = new (string Row, string[] Cells)[]
    {
        (
            """| Route usage | `/spec:route` | ``/spec:route needs a target flow name and, if given, a --flows-dir without $, `, " or \. Usage: /spec:route {flow} [reason]. Nothing was changed.`` |""",
            [
                "Route usage",
                "`/spec:route`",
                """``/spec:route needs a target flow name and, if given, a --flows-dir without $, `, " or \. Usage: /spec:route {flow} [reason]. Nothing was changed.``"""
            ]),
        (
            """| Spec name | `/spec:new`, starters | ``{command} needs a spec name that is not empty and has none of / \ $ ` ". Nothing was created.`` |""",
            [
                "Spec name",
                "`/spec:new`, starters",
                """``{command} needs a spec name that is not empty and has none of / \ $ ` ". Nothing was created.``"""
            ])
    };

    foreach (var (row, expected) in rows)
    {
        report.Checked();
        var actual = SplitCells(row);
        if (!actual.SequenceEqual(expected))
            report.Fail("table parser self-test", $"expected {expected.Length} cells for \"{expected[0]}\", got {actual.Count}: [{string.Join(" | ", actual)}]");
    }
}

void CheckTemplates(TemplateExpectation expectation, string root, Reporter report)
{
    const string id = "templates";
    var check = $"{id} in {expectation.File}";
    report.Checked();

    var canonical = ReadBlocks(root, expectation.CanonicalFile, id);
    if (canonical.Count != 1)
    {
        report.Fail(check, canonical.Count == 0
            ? $"canonical templates block missing from {expectation.CanonicalFile}"
            : $"canonical templates block not unique in {expectation.CanonicalFile} ({canonical.Count} found)");
        return;
    }

    var copies = ReadBlocks(root, expectation.File, id);
    if (copies.Count != 1)
    {
        report.Fail(check, copies.Count == 0
            ? $"templates block missing from {expectation.File}"
            : $"templates block duplicated in {expectation.File} ({copies.Count} found)");
        return;
    }

    CompareTemplates(expectation, ReadTemplates(canonical[0]), ReadTemplates(copies[0]), check, report);
}

void CompareTemplates(
    TemplateExpectation expectation,
    Dictionary<string, string> canonical,
    Dictionary<string, string> copy,
    string check,
    Reporter report)
{
    foreach (var name in expectation.Names)
    {
        if (!canonical.TryGetValue(name, out var canonicalText))
            report.Fail(check, $"template \"{name}\" missing from {expectation.CanonicalFile}");
        else if (!copy.TryGetValue(name, out var copyText))
            report.Fail(check, $"template \"{name}\" missing from {expectation.File}");
        else if (copyText != canonicalText)
            report.Fail(check, $"template \"{name}\" in {expectation.File} differs from {expectation.CanonicalFile}");
    }

    foreach (var name in copy.Keys.Except(expectation.Names))
        report.Fail(check, $"unexpected template \"{name}\" in {expectation.File}");
}

Dictionary<string, string> ReadTemplates(string block)
{
    var templates = new Dictionary<string, string>();
    foreach (var line in block.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith('|')))
    {
        var cells = SplitCells(line);
        if (cells.Count >= 2 && cells[0] != "Template" && !cells[0].StartsWith("---"))
            templates[cells[0]] = cells[^1];
    }

    return templates;
}

List<string> SplitCells(string row)
{
    var cells = new List<string>();
    var cell = new StringBuilder();
    var spanLength = 0;
    for (var i = 0; i < row.Length; i++)
    {
        if (row[i] == '`')
        {
            var run = BacktickRunLength(row, i);
            spanLength = NextSpanLength(row, i, run, spanLength);
            cell.Append(row, i, run);
            i += run - 1;
        }
        else if (row[i] == '|' && spanLength == 0)
        {
            cells.Add(cell.ToString().Trim());
            cell.Clear();
        }
        else
        {
            cell.Append(row[i]);
        }
    }

    cells.Add(cell.ToString().Trim());
    return TrimEdgeCells(cells);
}

int BacktickRunLength(string text, int start)
{
    var end = start;
    while (end < text.Length && text[end] == '`')
        end++;
    return end - start;
}

int NextSpanLength(string text, int runStart, int run, int spanLength)
{
    if (spanLength == run)
        return 0;
    if (spanLength == 0 && HasClosingRun(text, runStart + run, run))
        return run;
    return spanLength;
}

bool HasClosingRun(string text, int from, int run)
{
    for (var i = from; i < text.Length; i++)
    {
        if (text[i] != '`')
            continue;
        var length = BacktickRunLength(text, i);
        if (length == run)
            return true;
        i += length - 1;
    }

    return false;
}

List<string> TrimEdgeCells(List<string> cells)
{
    if (cells.Count > 0 && cells[0].Length == 0)
        cells.RemoveAt(0);
    if (cells.Count > 0 && cells[^1].Length == 0)
        cells.RemoveAt(cells.Count - 1);
    return cells;
}

// Harness

int RunGit(string workingDirectory, params string[] arguments)
{
    var start = new ProcessStartInfo("git")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start)!;
    process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode;
}

internal sealed record RoutingTextBlock(string Id, string CanonicalFile, IReadOnlyList<string> RequiredCopies);

internal sealed record TemplateExpectation(string CanonicalFile, string File, IReadOnlyList<string> Names);

internal sealed class Reporter
{
    private int _checks;
    private int _failures;

    public void Checked() => _checks++;

    public void Fail(string check, string message)
    {
        _failures++;
        Console.WriteLine($"FAIL {check}: {message}");
    }

    public int Summary()
    {
        if (_failures == 0)
        {
            Console.WriteLine($"{_checks} check(s) passed.");
            return 0;
        }

        Console.WriteLine($"{_failures} failed assertion(s) across {_checks} check(s).");
        return 1;
    }
}
