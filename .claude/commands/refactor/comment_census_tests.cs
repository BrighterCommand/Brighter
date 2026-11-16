#nullable enable
using System.Diagnostics;

// The sibling test script for comment_census.cs. No test framework: each row builds a throwaway git
// repository, commits files with known comment lines, runs the census against the range between the
// two commits, and checks its exit code and output.

const string CensusScript = ".claude/commands/refactor/comment_census.cs";
const string Header = "project\t//\t///\tfiles\trefs";
const int UsageErrorExitCode = 1;

var censusPath = Path.GetFullPath(CensusScript);
var failures = 0;

failures += Check("sizes added comments per project", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase();
    repository.Write("src/Alpha/Foo.cs", """
        namespace Alpha;
        // a comment the base already had
        public class Foo
        {
            // narrates the next line
            /// <summary>Documents Bar.</summary>
            public void Bar()
            {
                //Arrange
                // Act + Assert
                var x = 1; // trailing comment
                // see #4207 for history
            }
        }
        """);
    repository.Write("src/Alpha/New.cs", """
        #region Licence
        /* The MIT License (MIT)
        Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
        */
        #endregion
        // a comment in a new file
        namespace Alpha;
        """);
    repository.Write("tests/Beta/BarTests.cs", """
        // Spec 0027 history narrative
        public class BarTests { }
        """);
    repository.Write("tests/Beta/Generated/GeneratedTests.cs", """
        // a generated comment
        public class GeneratedTests { }
        """);
    repository.Write("src/Alpha/Foo.Designer.cs", """
        // a designer comment
        """);
    repository.Commit();

    //Act
    var result = RunCensus(repository.Root, repository.Range);

    //Assert
    return Expect(result, 0,
        Header,
        "src/Alpha\t4\t1\t2\t1",
        "tests/Beta\t1\t0\t1\t1",
        "TOTAL\t5\t1\t3\t2");
});

failures += Check("limits the census to the paths given", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase();
    repository.Write("src/Alpha/Foo.cs", "// in src\n");
    repository.Write("tests/Beta/BarTests.cs", "// in tests\n/// <summary>Docs.</summary>\n");
    repository.Commit();

    //Act
    var result = RunCensus(repository.Root, repository.Range, "tests");

    //Assert
    return Expect(result, 0,
        Header,
        "tests/Beta\t1\t1\t1\t0",
        "TOTAL\t1\t1\t1\t0");
});

failures += Check("counts Liquid templates only when asked", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase();
    repository.Write("tools/Gen/Templates/When_x.cs.liquid", "// a template comment\n");
    repository.Commit();

    //Act
    var withoutLiquid = RunCensus(repository.Root, repository.Range);
    var withLiquid = RunCensus(repository.Root, repository.Range, "--liquid");

    //Assert
    return Expect(withoutLiquid, 0, Header, "TOTAL\t0\t0\t0\t0")
        .Concat(Expect(withLiquid, 0, Header, "tools/Gen\t1\t0\t1\t0", "TOTAL\t1\t0\t1\t0"));
});

failures += Check("rejects a call with no range", () =>
{
    //Arrange
    using var repository = ScratchRepository.WithCommittedBase();

    //Act
    var result = RunCensus(repository.Root);

    //Assert
    return result.ExitCode == UsageErrorExitCode
        ? []
        : [$"expected exit {UsageErrorExitCode}, got {result.ExitCode}"];
});

Console.WriteLine(failures == 0 ? "PASS comment_census_tests" : $"FAIL comment_census_tests: {failures} row(s) failed");
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

ProcessResult RunCensus(string workingDirectory, params string[] arguments) =>
    ProcessResult.Run("dotnet", workingDirectory, ["run", censusPath, "--", .. arguments]);

static IEnumerable<string> Expect(ProcessResult result, int exitCode, params string[] lines)
{
    if (result.ExitCode != exitCode)
    {
        yield return $"expected exit {exitCode}, got {result.ExitCode}; stderr: {result.Error.Trim()}";
    }
    var actual = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
    if (!actual.SequenceEqual(lines))
    {
        yield return $"expected output:\n{string.Join('\n', lines)}\ngot:\n{string.Join('\n', actual)}";
    }
}

sealed class ScratchRepository : IDisposable
{
    private string? _baseCommit;

    private ScratchRepository(string root) => Root = root;

    public string Root { get; }

    public string Range => $"{_baseCommit}..HEAD";

    public static ScratchRepository WithCommittedBase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"comment-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var repository = new ScratchRepository(root);
        repository.Git("init", "-q");
        repository.Git("config", "user.email", "tests@example.com");
        repository.Git("config", "user.name", "tests");
        repository.Write("README.md", "base\n");
        repository.Write("src/Alpha/Foo.cs", "namespace Alpha;\n// a comment the base already had\n");
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
