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

var reporter = new Reporter();

foreach (var block in registry)
    CheckBlock(block, repositoryRoot, reporter);

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

// Harness

internal sealed record RoutingTextBlock(string Id, string CanonicalFile, IReadOnlyList<string> RequiredCopies);

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
