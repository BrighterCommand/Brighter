#nullable enable
using System.Diagnostics;

// The sibling test script for comment_only_check.cs. No test framework: each row builds a throwaway
// git repository, commits a change, runs the check against the range between the two commits, and
// checks its exit code and output.

const string CheckScript = ".claude/commands/refactor/comment_only_check.cs";
const int CommentOnlyExitCode = 0;
const int UsageErrorExitCode = 1;
const int NonCommentChangeExitCode = 2;

var checkPath = Path.GetFullPath(CheckScript);
var failures = 0;

const string BaseFoo = """
    namespace Alpha;
    // a narrating comment
    public class Foo
    {
        public int Bar() => 1; // returns one
    }
    """;

failures += Check("passes a diff that changes only comments", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase("src/Alpha/Foo.cs", BaseFoo);
    repository.Write("src/Alpha/Foo.cs", """
        namespace Alpha;
        /// <summary>Documents Foo.</summary>
        public class Foo
        {
            public int Bar() => 1; // returns one
        }
        """);
    repository.Commit();

    //Act
    var result = RunCheck(repository.Root, repository.Range);

    //Assert
    return ExpectExit(result, CommentOnlyExitCode);
});

failures += Check("passes a change to a trailing comment when the code before it is unchanged", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase("src/Alpha/Foo.cs", BaseFoo);
    repository.Write("src/Alpha/Foo.cs", """
        namespace Alpha;
        // a narrating comment
        public class Foo
        {
            public int Bar() => 1; // the answer
        }
        """);
    repository.Commit();

    //Act
    var result = RunCheck(repository.Root, repository.Range);

    //Assert
    return ExpectExit(result, CommentOnlyExitCode);
});

failures += Check("fails a diff that changes code and names the line", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase("src/Alpha/Foo.cs", BaseFoo);
    repository.Write("src/Alpha/Foo.cs", """
        namespace Alpha;
        public class Foo
        {
            public int Bar() => 2; // returns one
        }
        """);
    repository.Commit();

    //Act
    var result = RunCheck(repository.Root, repository.Range);

    //Assert
    var problems = ExpectExit(result, NonCommentChangeExitCode).ToList();
    if (!result.Output.Contains("src/Alpha/Foo.cs") || !result.Output.Contains("public int Bar() => 2;"))
    {
        problems.Add($"expected the output to name src/Alpha/Foo.cs and the changed line; got:\n{result.Output}");
    }
    return problems;
});

failures += Check("ignores files that are not code", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase("docs/notes.md", "first line\n");
    repository.Write("docs/notes.md", "a different line\n");
    repository.Commit();

    //Act
    var result = RunCheck(repository.Root, repository.Range);

    //Assert
    return ExpectExit(result, CommentOnlyExitCode);
});

failures += Check("rejects a range git cannot resolve", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase("src/Alpha/Foo.cs", BaseFoo);

    //Act
    var result = RunCheck(repository.Root, "no-such-ref..HEAD");

    //Assert
    return ExpectExit(result, UsageErrorExitCode);
});

Console.WriteLine(failures == 0 ? "PASS comment_only_check_tests" : $"FAIL comment_only_check_tests: {failures} row(s) failed");
return failures == 0 ? 0 : 1;

int Check(string name, Func<IEnumerable<string>> row)
{
    var problems = row().ToList();
    foreach (var problem in problems)
    {
        Console.WriteLine($"FAIL {name}: {problem}");
    }
    if (problems.Count == 0)
    {
        Console.WriteLine($"ok   {name}");
    }
    return problems.Count == 0 ? 0 : 1;
}

ProcessResult RunCheck(string workingDirectory, params string[] arguments) =>
    ProcessResult.Run("dotnet", workingDirectory, ["run", checkPath, "--", .. arguments]);

static IEnumerable<string> ExpectExit(ProcessResult result, int exitCode)
{
    if (result.ExitCode != exitCode)
    {
        yield return $"expected exit {exitCode}, got {result.ExitCode}; stdout: {result.Output.Trim()} stderr: {result.Error.Trim()}";
    }
}

sealed class ScratchRepository : IDisposable
{
    private string? _baseCommit;

    private ScratchRepository(string root) => Root = root;

    public string Root { get; }

    public string Range => $"{_baseCommit}..HEAD";

    public static ScratchRepository WithCommittedBase(string relativePath, string content)
    {
        var root = Path.Combine(Path.GetTempPath(), $"comment-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var repository = new ScratchRepository(root);
        repository.Git("init", "-q");
        repository.Git("config", "user.email", "tests@example.com");
        repository.Git("config", "user.name", "tests");
        repository.Write(relativePath, content);
        repository._baseCommit = repository.Commit();
        return repository;
    }

    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.EndsWith('\n') ? content : content + "\n");
    }

    public string Commit()
    {
        Git("add", "-A");
        Git("commit", "-q", "-m", "change");
        return Git("rev-parse", "HEAD").Trim();
    }

    public string Git(params string[] arguments)
    {
        var result = ProcessResult.Run("git", Root, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {result.Error}");
        }
        return result.Output;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}

sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    public static ProcessResult Run(string fileName, string workingDirectory, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"could not start {fileName}");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output.Result, error.Result);
    }
}
