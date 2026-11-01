#nullable enable
using System.Diagnostics;
using System.Text.RegularExpressions;

// Checks that a diff changes only comments and blank lines in code files, so a comment-only pass
// of /refactor:tidy-comments can be committed and reviewed as one. Usage: comment_only_check.cs
// [git diff arguments] — with none, the working tree against HEAD. Exit 0 when the diff is
// comment-only, 2 when it changes code (each such line is printed), 1 when git cannot run the diff.

const int CommentOnlyExitCode = 0;
const int UsageErrorExitCode = 1;
const int NonCommentChangeExitCode = 2;

var diff = GitDiff.Run(args.Length == 0 ? ["HEAD"] : args);
if (diff is null)
{
    return UsageErrorExitCode;
}

var violations = Hunk.Parse(diff).SelectMany(h => h.CodeChanges()).ToList();
foreach (var violation in violations)
{
    Console.WriteLine(violation);
}
return violations.Count == 0 ? CommentOnlyExitCode : NonCommentChangeExitCode;

sealed record ChangedLine(string File, int Number, char Sign, string Text)
{
    public string Trimmed => Text.Trim();

    public bool IsCommentOrBlank =>
        Trimmed.Length == 0
        || Trimmed.StartsWith("//", StringComparison.Ordinal)
        || Trimmed.StartsWith("/*", StringComparison.Ordinal)
        || Trimmed.StartsWith("*", StringComparison.Ordinal);

    public string CodeBeforeTrailingComment
    {
        get
        {
            var trailing = TrailingComment.Find(Text);
            return (trailing is null ? Text : Text[..trailing.Value]).TrimEnd();
        }
    }

    public override string ToString() => $"{File}:{Number}: {Sign}{Text}";
}

sealed class Hunk(string file)
{
    private readonly string _file = file;
    private readonly List<ChangedLine> _removed = [];
    private readonly List<ChangedLine> _added = [];

    private static readonly Regex s_hunkHeader = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.CultureInvariant);
    private static readonly string[] s_codeExtensions = [".cs", ".liquid"];

    public static IEnumerable<Hunk> Parse(IEnumerable<string> diffLines)
    {
        var file = (string?)null;
        var inHeader = false;
        Hunk? hunk = null;
        int oldNumber = 0, newNumber = 0;

        foreach (var line in diffLines)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (hunk is not null) yield return hunk;
                hunk = null;
                file = null;
                inHeader = true;
                continue;
            }
            if (inHeader)
            {
                if (line.StartsWith("+++ b/", StringComparison.Ordinal)) file = line[6..];
                else if (line.StartsWith("--- a/", StringComparison.Ordinal) && file is null) file = line[6..];
                if (!line.StartsWith("@@", StringComparison.Ordinal)) continue;
                inHeader = false;
            }

            var header = s_hunkHeader.Match(line);
            if (header.Success)
            {
                if (hunk is not null) yield return hunk;
                hunk = file is not null && IsCode(file) ? new Hunk(file) : null;
                oldNumber = int.Parse(header.Groups[1].Value);
                newNumber = int.Parse(header.Groups[2].Value);
                continue;
            }

            if (hunk is null || line.Length == 0) continue;
            if (line[0] == '-') hunk._removed.Add(new ChangedLine(hunk._file, oldNumber++, '-', line[1..]));
            else if (line[0] == '+') hunk._added.Add(new ChangedLine(hunk._file, newNumber++, '+', line[1..]));
        }
        if (hunk is not null) yield return hunk;
    }

    // A removed and an added line whose code before the trailing comment is identical only changed
    // the comment, so they cancel out.
    public IEnumerable<ChangedLine> CodeChanges()
    {
        var removed = _removed.Where(l => !l.IsCommentOrBlank).ToList();
        var added = _added.Where(l => !l.IsCommentOrBlank).ToList();
        foreach (var line in added.ToList())
        {
            var twin = removed.FirstOrDefault(r => r.CodeBeforeTrailingComment == line.CodeBeforeTrailingComment);
            if (twin is null) continue;
            removed.Remove(twin);
            added.Remove(line);
        }
        return removed.Concat(added);
    }

    private static bool IsCode(string path) => s_codeExtensions.Any(e => path.EndsWith(e, StringComparison.Ordinal));
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

static class GitDiff
{
    public static string[]? Run(string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "diff", "--no-color", "--no-ext-diff", "-U0" }.Concat(arguments))
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
        return output.Result.Split('\n');
    }
}
