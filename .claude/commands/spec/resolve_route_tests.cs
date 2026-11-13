#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// The sibling test script for resolve_route.cs (spec 0040, ADR 0092). No test framework: a
// plain file-based app that runs the resolver against the expectation files under
// .claude/test-fixtures/routing/expect/, reports one FAIL line per failed assertion and
// exits 1 when anything failed.

var repositoryRoot = Directory.GetCurrentDirectory();

const string FixtureRoot = ".claude/test-fixtures/routing";
const string FixtureSpecs = FixtureRoot + "/specs";
const string ResolverScript = ".claude/commands/spec/resolve_route.cs";
const string ShippedFlows = ".claude/flows";
const string RepositorySpecs = "specs";
const string ReportPrefix = "route-report: ";
const string CurrentPrefix = "route-current: ";

string[] unusualLegacySpecs =
[
    "0036-scoped-lifetime-per-pipeline",
    "0036-universal-transport-conformance-tests",
    "0031-test_naming_conventions",
    "0028-box-provisioning-rdd-role-interfaces"
];

var knownKeys = new[] { "run", "flows", "exit", "report" };
var reporter = new Reporter();

CheckMatcherSelfTest(reporter);

var hashesBefore = HashTree(repositoryRoot, FixtureRoot, "specs");

if (ResolverRuns(repositoryRoot, reporter))
{
    CheckExpectations(repositoryRoot, reporter);
    CheckUnusualLegacySpecs(repositoryRoot, unusualLegacySpecs, reporter);
}

CheckNothingWritten(hashesBefore, HashTree(repositoryRoot, FixtureRoot, "specs"), reporter);

return reporter.Summary();

bool ResolverRuns(string root, Reporter report)
{
    report.Checked();
    var lines = SplitLines(RunResolver(root, ["--definition", "feature"], ShippedFlows).Stdout);
    if (!lines.Any(line => line.StartsWith(ReportPrefix, StringComparison.Ordinal)))
    {
        report.Fail("pre-check", "the resolver did not run");
        return false;
    }

    if (lines.Any(line => !IsRecordLine(line)))
        report.Fail("pre-check", "the resolver printed lines other than record lines");
    return true;
}

void CheckExpectations(string root, Reporter report)
{
    var directory = Path.Combine(root, FixtureRoot, "expect");
    if (!Directory.Exists(directory))
    {
        report.Fail("fixtures", $"the expectation directory {FixtureRoot}/expect is missing");
        return;
    }

    foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        CheckExpectation(file, root, report);
}

void CheckExpectation(string file, string root, Reporter report)
{
    var check = $"expectation {Path.GetFileNameWithoutExtension(file)}";
    report.Checked();

    var expectation = ParseExpectation(file, check, report);
    if (expectation == null)
        return;

    var first = RunResolver(root, expectation.Run, expectation.Flows);
    var second = RunResolver(root, expectation.Run, expectation.Flows);
    if (first.Stdout != second.Stdout)
        report.Fail(check, "standard output differs between two runs of the same expectation");

    CheckExit(expectation, first, check, report);
    CheckReport(expectation, first, check, report);
}

Expectation? ParseExpectation(string file, string check, Reporter report)
{
    JsonObject? document;
    try
    {
        document = JsonNode.Parse(ReadLf(file)) as JsonObject;
    }
    catch (JsonException)
    {
        document = null;
    }

    if (document == null)
    {
        report.Fail(check, "the file is not readable JSON holding one object");
        return null;
    }

    var unknown = document.Select(pair => pair.Key).Except(knownKeys).ToList();
    foreach (var key in unknown)
        report.Fail(check, $"unknown top-level key \"{key}\"");
    if (unknown.Count > 0)
        return null;

    return BuildExpectation(document, check, report);
}

Expectation? BuildExpectation(JsonObject document, string check, Reporter report)
{
    if (document["run"] is not JsonArray run || run.Any(item => item is not JsonValue))
    {
        report.Fail(check, "\"run\" must be an array of mode arguments");
        return null;
    }

    var flows = document["flows"]?.GetValue<string>();
    return new Expectation(
        run.Select(item => item!.GetValue<string>()).ToList(),
        flows == null ? ShippedFlows : $"{FixtureRoot}/{flows}",
        document["exit"]?.GetValue<int>() ?? 0,
        document["report"]);
}

void CheckExit(Expectation expectation, ProcessResult result, string check, Reporter report)
{
    if (result.ExitCode != expectation.Exit)
        report.Fail(check, $"expected exit {expectation.Exit}, got {result.ExitCode}");

    if (expectation.Exit == 1 && (result.Stdout.Length > 0 || SplitLines(result.Stdout).Any(IsRecordLine)))
        report.Fail(check, "an exit 1 must leave standard output empty, with no record line");
}

void CheckReport(Expectation expectation, ProcessResult result, string check, Reporter report)
{
    if (expectation.Report == null)
        return;

    var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
    if (line == null)
    {
        report.Fail(check, "expected a route-report line, got none");
        return;
    }

    var actual = JsonNode.Parse(line[ReportPrefix.Length..]);
    foreach (var mismatch in Mismatches(expectation.Report, actual, "report"))
        report.Fail(check, mismatch);
}

void CheckUnusualLegacySpecs(string root, IEnumerable<string> specNames, Reporter report)
{
    var expected = JsonNode.Parse("""
        {
          "flow": { "name": "feature", "source": "default" },
          "valid": true,
          "stray_markers": [],
          "next": { "commands": ["/spec:implement"] }
        }
        """);

    foreach (var name in specNames)
    {
        var check = $"existing spec {name}";
        report.Checked();
        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", RepositorySpecs, "--spec", name, "--flows-dir", ShippedFlows]);
        var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (result.ExitCode != 0 || line == null)
        {
            report.Fail(check, $"expected a route-report line and exit 0, got exit {result.ExitCode}");
            continue;
        }

        foreach (var mismatch in Mismatches(expected, JsonNode.Parse(line[ReportPrefix.Length..]), "report"))
            report.Fail(check, mismatch);
    }
}

void CheckMatcherSelfTest(Reporter report)
{
    report.Checked();
    var expected = JsonNode.Parse("""{ "a": 1, "b": [1, 2] }""");
    if (!Mismatches(expected, JsonNode.Parse("""{ "a": 2, "b": [1] }"""), "self-test").Any())
        report.Fail("matcher self-test", "a known mismatch was not reported, so the matcher cannot fail");

    var partial = JsonNode.Parse("""{ "a": 1 }""");
    if (Mismatches(partial, JsonNode.Parse("""{ "a": 1, "extra": true }"""), "self-test").Any())
        report.Fail("matcher self-test", "a partial match was reported as a mismatch");
}

IEnumerable<string> Mismatches(JsonNode? expected, JsonNode? actual, string path)
{
    if (expected is JsonObject expectedObject)
        return ObjectMismatches(expectedObject, actual, path);
    if (expected is JsonArray expectedArray)
        return ArrayMismatches(expectedArray, actual, path);
    return JsonNode.DeepEquals(expected, actual)
        ? []
        : [$"{path}: expected {expected?.ToJsonString() ?? "null"}, got {actual?.ToJsonString() ?? "null"}"];
}

IEnumerable<string> ObjectMismatches(JsonObject expected, JsonNode? actual, string path)
{
    if (actual is not JsonObject actualObject)
        return [$"{path}: expected an object, got {actual?.ToJsonString() ?? "null"}"];

    return expected.SelectMany(pair => actualObject.ContainsKey(pair.Key)
        ? Mismatches(pair.Value, actualObject[pair.Key], $"{path}.{pair.Key}")
        : [$"{path}.{pair.Key}: expected key is missing"]);
}

IEnumerable<string> ArrayMismatches(JsonArray expected, JsonNode? actual, string path)
{
    if (actual is not JsonArray actualArray)
        return [$"{path}: expected an array, got {actual?.ToJsonString() ?? "null"}"];
    if (actualArray.Count != expected.Count)
        return [$"{path}: expected {expected.Count} element(s), got {actualArray.Count}"];

    return expected.SelectMany((item, index) => Mismatches(item, actualArray[index], $"{path}[{index}]"));
}

void CheckNothingWritten(Dictionary<string, string> before, Dictionary<string, string> after, Reporter report)
{
    report.Checked();
    foreach (var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
    {
        before.TryGetValue(path, out var was);
        after.TryGetValue(path, out var now);
        if (was != now)
            report.Fail("read-only", $"{path} was {Describe(was, now)} while the resolver ran");
    }
}

string Describe(string? was, string? now) => was == null ? "added" : now == null ? "removed" : "changed";

Dictionary<string, string> HashTree(string root, params string[] directories)
{
    var hashes = new Dictionary<string, string>();
    foreach (var directory in directories.Select(d => Path.Combine(root, d)).Where(Directory.Exists))
    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        hashes[Path.GetRelativePath(root, file).Replace('\\', '/')] =
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    return hashes;
}

ProcessResult RunResolver(string root, IEnumerable<string> modeArguments, string flowsDirectory) =>
    RunProcess(root, "dotnet", ["run", ResolverScript, "--", FixtureSpecs, .. modeArguments, "--flows-dir", flowsDirectory]);

bool IsRecordLine(string line) =>
    line.StartsWith(ReportPrefix, StringComparison.Ordinal) || line.StartsWith(CurrentPrefix, StringComparison.Ordinal);

List<string> SplitLines(string text) =>
    text.Replace("\r\n", "\n").Split('\n').Where(line => line.Length > 0).ToList();

// Harness

ProcessResult RunProcess(string workingDirectory, string fileName, IEnumerable<string> arguments)
{
    var start = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start)!;
    var error = process.StandardError.ReadToEndAsync();
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return new ProcessResult(process.ExitCode, output, error.Result);
}

string ReadLf(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

internal sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

internal sealed record Expectation(IReadOnlyList<string> Run, string Flows, int Exit, JsonNode? Report);

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
