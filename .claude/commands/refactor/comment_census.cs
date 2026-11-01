#nullable enable
using System.Diagnostics;
using System.Text.RegularExpressions;

// Sizes the comments a git range added, per project, so /refactor:tidy-comments can slice a scope
// before reading it. Usage: comment_census.cs <range> [path...] [--liquid]
// Writes a tab-separated table: project, // lines, /// lines, files, lines carrying a reference.

const int SuccessExitCode = 0;
const int UsageErrorExitCode = 1;
const string LiquidFlag = "--liquid";

var includeLiquid = args.Contains(LiquidFlag);
var positional = args.Where(a => a != LiquidFlag).ToArray();
if (positional.Length == 0 || positional[0].StartsWith("--", StringComparison.Ordinal))
{
    Console.Error.WriteLine("usage: comment_census.cs <range> [path...] [--liquid]");
    return UsageErrorExitCode;
}

var diff = GitDiff.AddedLines(range: positional[0], paths: positional[1..]);
if (diff is null)
{
    return UsageErrorExitCode;
}

var census = new Census(new SourceFileFilter(includeLiquid));
census.Read(diff);
census.WriteTo(Console.Out);
return SuccessExitCode;

sealed class Census(SourceFileFilter filter)
{
    private readonly Dictionary<string, ProjectTally> _projects = new(StringComparer.Ordinal);

    public void Read(IEnumerable<string> diffLines)
    {
        var file = (string?)null;
        var inLicence = false;
        foreach (var line in diffLines)
        {
            if (DiffLine.IsNewFileHeader(line, out var path))
            {
                file = path is not null && filter.Includes(path) ? path : null;
                inLicence = false;
                continue;
            }

            if (file is null || !DiffLine.IsAddition(line)) continue;

            var text = line[1..].Trim();
            if (LicenceRegion.Opens(text)) { inLicence = true; continue; }
            if (inLicence) { inLicence = !LicenceRegion.Closes(text); continue; }

            Tally(file, CommentLine.Classify(text));
        }
    }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine("project\t//\t///\tfiles\trefs");
        foreach (var (name, tally) in _projects.OrderByDescending(p => p.Value.Slash).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.WriteLine($"{name}\t{tally.Slash}\t{tally.Xml}\t{tally.Files.Count}\t{tally.References}");
        }
        var all = _projects.Values;
        writer.WriteLine($"TOTAL\t{all.Sum(t => t.Slash)}\t{all.Sum(t => t.Xml)}\t{all.Sum(t => t.Files.Count)}\t{all.Sum(t => t.References)}");
    }

    private void Tally(string file, CommentLine comment)
    {
        if (comment.Kind == CommentKind.None) return;

        var project = ProjectOf(file);
        if (!_projects.TryGetValue(project, out var tally))
        {
            tally = new ProjectTally();
            _projects[project] = tally;
        }
        tally.Count(file, comment);
    }

    private static readonly string[] s_projectRoots = ["src", "tests", "samples", "tools"];

    private static string ProjectOf(string path)
    {
        var segments = path.Split('/');
        return segments.Length > 2 && s_projectRoots.Contains(segments[0])
            ? $"{segments[0]}/{segments[1]}"
            : segments[0];
    }
}

sealed class ProjectTally
{
    public int Slash { get; private set; }
    public int Xml { get; private set; }
    public int References { get; private set; }
    public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

    public void Count(string file, CommentLine comment)
    {
        Files.Add(file);
        if (comment.Kind == CommentKind.Xml) Xml++; else Slash++;
        if (comment.CarriesReference) References++;
    }
}

sealed class SourceFileFilter(bool includeLiquid)
{
    public bool Includes(string path)
    {
        if (path.Split('/').Contains("Generated")) return false;
        if (path.EndsWith(".Designer.cs", StringComparison.Ordinal)) return false;
        return path.EndsWith(".cs", StringComparison.Ordinal)
            || (includeLiquid && path.EndsWith(".liquid", StringComparison.Ordinal));
    }
}

enum CommentKind { None, Slash, Xml }

sealed record CommentLine(CommentKind Kind, bool CarriesReference)
{
    private static readonly CommentLine s_none = new(CommentKind.None, false);

    private static readonly Regex s_bareAaaMarker = new(
        @"^//\s*(Arrange|Act|Assert)(\s*(\+|&|/|and)\s*(Arrange|Act|Assert))*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex s_reference = new(
        @"#\d{3,5}\b|\b[Ss]pec\s?\d{4}\b|\bPR\s?#|\bItem [A-Z#]|\b\w+\.cs:\d+|\bPhase \d",
        RegexOptions.CultureInvariant);

    public static CommentLine Classify(string text)
    {
        if (text.StartsWith("///", StringComparison.Ordinal)) return Of(CommentKind.Xml, text);
        if (text.StartsWith("//", StringComparison.Ordinal))
            return s_bareAaaMarker.IsMatch(text) ? s_none : Of(CommentKind.Slash, text);
        if (text.StartsWith("/*", StringComparison.Ordinal)) return Of(CommentKind.Slash, text);

        var trailing = TrailingComment.Find(text);
        return trailing is null ? s_none : Of(CommentKind.Slash, text[trailing.Value..]);
    }

    private static CommentLine Of(CommentKind kind, string comment) => new(kind, s_reference.IsMatch(comment));
}

static class TrailingComment
{
    // A "//" counts only when preceded by whitespace and outside a string literal, so URLs and
    // string contents are not mistaken for comments.
    public static int? Find(string text)
    {
        var inString = false;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
            if (!inString && text[i] == '/' && text[i + 1] == '/' && i > 0 && char.IsWhiteSpace(text[i - 1]))
                return i;
        }
        return null;
    }
}

static class LicenceRegion
{
    public static bool Opens(string text) =>
        text.StartsWith("#region", StringComparison.Ordinal) && text.Contains("Licen", StringComparison.OrdinalIgnoreCase);

    public static bool Closes(string text) => text.StartsWith("#endregion", StringComparison.Ordinal);
}

static class DiffLine
{
    public const char FileHeaderMarker = '\0';

    public static bool IsNewFileHeader(string line, out string? path)
    {
        path = null;
        if (line.Length == 0 || line[0] != FileHeaderMarker) return false;
        var target = line[1..];
        path = target.StartsWith("b/", StringComparison.Ordinal) ? target[2..] : null;
        return true;
    }

    public static bool IsAddition(string line) => line.StartsWith('+');
}

static class GitDiff
{
    public static IReadOnlyList<string>? AddedLines(string range, string[] paths)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "diff", "--no-color", "--no-ext-diff", "-U0", range, "--" }.Concat(paths))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start git");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Console.Error.Write(error.Result);
            return null;
        }
        return DiffHunks.WithoutHeaderNoise(output.Result.Split('\n'));
    }
}

static class DiffHunks
{
    // Inside a hunk, an added line that itself starts with "++ " looks like a file header, so the
    // "+++ " header is recognised only between "diff --git" and the first "@@", and re-marked.
    public static IReadOnlyList<string> WithoutHeaderNoise(string[] lines)
    {
        var result = new List<string>(lines.Length);
        var inHeader = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal)) { inHeader = true; continue; }
            if (line.StartsWith("@@", StringComparison.Ordinal)) { inHeader = false; continue; }
            if (inHeader)
            {
                if (line.StartsWith("+++ ", StringComparison.Ordinal)) result.Add(DiffLine.FileHeaderMarker + line[4..]);
                continue;
            }
            result.Add(line);
        }
        return result;
    }
}
