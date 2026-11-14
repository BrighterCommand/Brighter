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
    CheckAllSpecs(repositoryRoot, unusualLegacySpecs, reporter);
    CheckCurrentSpecStates(repositoryRoot, reporter);
    CheckMissingCommandTree(repositoryRoot, reporter);
    CheckSharedViolationsOverInvalidFlows(repositoryRoot, reporter);
    CheckSharedViolationsOverFixtureSpecs(repositoryRoot, reporter);
    CheckAllOutputSizes(repositoryRoot, reporter);
}

CheckShippedFlowDirectory(repositoryRoot, reporter);
CheckCreationRecords(repositoryRoot, reporter);
CheckGeneratedTamperCases(repositoryRoot, reporter);

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

void CheckSharedViolationsOverInvalidFlows(string root, Reporter report)
{
    const string check = "shared violations over flows-invalid";
    report.Checked();
    var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", RepositorySpecs, "--all", "--flows-dir", $"{FixtureRoot}/flows-invalid"]);
    var (current, summaries) = ParseAllRecords(result);
    if (current == null)
    {
        report.Fail(check, $"expected exit 0 and a first {CurrentPrefix.Trim()} line, got exit {result.ExitCode}");
        return;
    }

    var table = $"{FixtureRoot}/flows-invalid/approvals.md";
    var expected = JsonNode.Parse($$"""
        [
          { "rule": "R4", "code": "unknown-command", "step": "design", "detail": "unknown command",
            "message": "Step design names a command that does not exist, /spec:designx." },
          { "rule": "R12", "code": "approval-entry", "step": null, "detail": "duplicate entry",
            "message": "{{table}} has more than one entry for kind tasks." },
          { "rule": "R12", "code": "approval-entry", "step": null, "detail": "unreadable table or unknown command",
            "message": "{{table}} is unreadable or names a command that does not exist: /adr:acceptx." }
        ]
        """);
    foreach (var mismatch in Mismatches(expected, current["shared_violations"], "shared_violations"))
        report.Fail(check, mismatch);

    foreach (var summary in summaries)
        VerifySharedFaultsOnlyOnSummary(summary, check, report);
}

void VerifySharedFaultsOnlyOnSummary(JsonObject summary, string check, Reporter report)
{
    var label = $"{check}: {summary["spec"]}";
    if (summary["valid"]?.GetValue<bool>() != false)
        report.Fail(label, "expected valid false");
    if (summary.ContainsKey("violations"))
        report.Fail(label, "a shared violation must not appear on a summary line");
    if (summary["stop"]?["reason"]?.GetValue<string>() != "invalid")
        report.Fail(label, $"expected stop {{reason: invalid}}, got {summary["stop"]?.ToJsonString() ?? "null"}");
}

void CheckSharedViolationsOverFixtureSpecs(string root, Reporter report)
{
    const string check = "violations over the fixture specs";
    report.Checked();
    var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", FixtureSpecs, "--all", "--flows-dir", $"{FixtureRoot}/flows"]);
    var (current, summaries) = ParseAllRecords(result);
    if (current == null)
    {
        report.Fail(check, $"expected exit 0 and a first {CurrentPrefix.Trim()} line, got exit {result.ExitCode}");
        return;
    }

    var expectedShared = JsonNode.Parse($$"""
        [
          { "rule": "R12", "code": "approval-entry", "step": "spike", "detail": "kind without entry",
            "message": "Step spike is of kind spike, which has no entry in {{FixtureRoot}}/flows/approvals.md." }
        ]
        """);
    foreach (var mismatch in Mismatches(expectedShared, current["shared_violations"], "shared_violations"))
        report.Fail(check, $"{mismatch} (the same record raised by several spike specs is listed once)");

    var copyInvalid = summaries.FirstOrDefault(summary => summary["spec"]?.GetValue<string>() == $"{FixtureSpecs}/copy-invalid");
    var expectedLine = JsonNode.Parse("""
        {
          "valid": false,
          "violations": [ { "rule": "R4", "code": "unknown-command", "step": "design" } ],
          "stop": { "reason": "invalid" }
        }
        """);
    foreach (var mismatch in Mismatches(expectedLine, copyInvalid, "copy-invalid"))
        report.Fail(check, mismatch);
    if (copyInvalid?["violations"]?[0] is JsonObject { Count: not 3 })
        report.Fail(check, "a summary violation carries only rule, code and step");
}

void CheckAllOutputSizes(string root, Reporter report)
{
    CheckAllOutputSize(root, "specs with the shipped flows", RepositorySpecs, ShippedFlows, true, report);
    CheckAllOutputSize(root, "specs with flows-invalid", RepositorySpecs, $"{FixtureRoot}/flows-invalid", true, report);
    CheckAllOutputSize(root, "the fixture specs with flows", FixtureSpecs, $"{FixtureRoot}/flows", false, report);
}

void CheckAllOutputSize(string root, string label, string specsRoot, string flowsDirectory, bool checkTotal, Reporter report)
{
    const int summaryLineLimit = 650;
    const int totalLimit = 24000;
    const string remedy = "the remedy is a later decision, such as paging or retiring finished specs";
    var check = $"--all size over {label}";
    report.Checked();

    var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--all", "--flows-dir", flowsDirectory]);
    if (result.ExitCode != 0)
    {
        report.Fail(check, $"expected exit 0, got {result.ExitCode}");
        return;
    }

    foreach (var line in SplitLines(result.Stdout).Where(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal) && l.Length > summaryLineLimit))
        report.Fail(check, $"a summary line is {line.Length} characters, over {summaryLineLimit}; {remedy}");

    if (checkTotal && result.Stdout.Length >= totalLimit)
        report.Fail(check, $"the output is {result.Stdout.Length} characters, not under {totalLimit}; {remedy}");
}

(JsonObject? Current, List<JsonObject> Summaries) ParseAllRecords(ProcessResult result)
{
    var records = SplitLines(result.Stdout).Where(IsRecordLine).ToList();
    if (result.ExitCode != 0 || records.Count == 0 || !records[0].StartsWith(CurrentPrefix, StringComparison.Ordinal))
        return (null, []);

    return ((JsonObject)JsonNode.Parse(records[0][CurrentPrefix.Length..])!,
        records.Skip(1).Select(line => (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!).ToList());
}

void CheckAllSpecs(string root, IReadOnlyList<string> pinnedToHandOff, Reporter report)
{
    const string check = "--all over specs";
    report.Checked();
    var stopwatch = Stopwatch.StartNew();
    var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", RepositorySpecs, "--all", "--flows-dir", ShippedFlows]);
    stopwatch.Stop();
    report.WarnIfSlow(check, stopwatch.Elapsed);
    var records = SplitLines(result.Stdout).Where(IsRecordLine).ToList();
    var directories = Directory.GetDirectories(Path.Combine(root, RepositorySpecs))
        .Select(directory => Path.GetFileName(directory))
        .Where(name => !name.StartsWith('.'))
        .Order(StringComparer.Ordinal)
        .ToList();

    if (result.ExitCode != 0 || records.Count == 0 || !records[0].StartsWith(CurrentPrefix, StringComparison.Ordinal))
    {
        report.Fail(check, $"expected exit 0 and a first {CurrentPrefix.Trim()} line, got exit {result.ExitCode} and {records.Count} record line(s)");
        return;
    }

    VerifyCurrentLine(JsonNode.Parse(records[0][CurrentPrefix.Length..])!, directories.Count, check, report);
    var summaries = records.Skip(1).Select(line => (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!).ToList();
    if (!summaries.Select(summary => summary["spec"]?.GetValue<string>()).SequenceEqual(directories.Select(name => $"{RepositorySpecs}/{name}")))
    {
        report.Fail(check, "expected one summary line per spec directory, in ordinal order of directory name");
        return;
    }

    foreach (var summary in summaries)
        VerifySummary(root, summary, pinnedToHandOff, check, report);

    VerifyNamedSpecsAreListed(summaries, check, report);
}

void VerifyNamedSpecsAreListed(IReadOnlyList<JsonObject> summaries, string check, Reporter report)
{
    var listed = summaries.Select(summary => summary["spec"]!.GetValue<string>()).ToList();
    var spaced = $"{RepositorySpecs}/0021-Expose Unacceptable Message Window";
    if (!listed.Contains(spaced))
        report.Fail(check, $"expected {spaced} to be listed");

    var withoutArtefacts = summaries.FirstOrDefault(summary => summary["spec"]!.GetValue<string>() == $"{RepositorySpecs}/0036-universal-transport-conformance-tests");
    if (withoutArtefacts?["steps"]?[2]?["state"]?.GetValue<string>() != "approved-no-artefacts")
        report.Fail(check, "expected the tasks step of 0036-universal-transport-conformance-tests to be approved-no-artefacts");
}

void VerifyCurrentLine(JsonNode line, int specCount, string check, Reporter report)
{
    var keys = ((JsonObject)line).Select(pair => pair.Key).ToList();
    if (!keys.SequenceEqual(["schema_version", "mode", "current_spec", "specs", "shared_violations"]))
        report.Fail(check, $"the {CurrentPrefix.Trim()} line has keys {string.Join(", ", keys)}");
    if (line["schema_version"]?.GetValue<int>() != 1 || line["mode"]?.GetValue<string>() != "all")
        report.Fail(check, "the route-current line must carry schema_version 1 and mode all");
    if (line["specs"]?.GetValue<int>() != specCount)
        report.Fail(check, $"expected specs {specCount}, got {line["specs"]?.ToJsonString() ?? "null"}");
    if (line["shared_violations"] is not JsonArray { Count: 0 })
        report.Fail(check, "expected no shared violations over the shipped flows");
}

void VerifySummary(string root, JsonObject summary, IReadOnlyList<string> pinnedToHandOff, string check, Reporter report)
{
    var spec = summary["spec"]!.GetValue<string>();
    var name = spec[(RepositorySpecs.Length + 1)..];
    var label = $"{check}: {name}";
    if (summary.ContainsKey("schema_version") || summary.ContainsKey("mode"))
        report.Fail(label, "a summary line carries neither schema_version nor mode");

    var directory = Path.Combine(root, spec);
    if (File.Exists(Path.Combine(directory, ".flow")))
        return;

    var expectedFlow = JsonNode.Parse("""{ "name": "feature", "source": "default", "definition_present": true }""");
    foreach (var mismatch in Mismatches(expectedFlow, summary["flow"], "flow"))
        report.Fail(label, mismatch);
    if (summary["valid"]?.GetValue<bool>() != true)
        report.Fail(label, "expected valid");
    foreach (var key in new[] { "tampered", "stray_markers", "violations" }.Where(summary.ContainsKey))
        report.Fail(label, $"a legacy spec's line must not carry {key}");

    VerifySteps(summary, FeatureOracle.ExpectedSteps(directory), label, report);
    VerifyNext(summary, FeatureOracle.ExpectedNext(directory), pinnedToHandOff.Contains(name), label, report);
}

void VerifySteps(JsonObject summary, IReadOnlyList<OracleStepState> expected, string label, Reporter report)
{
    var actual = (summary["steps"] as JsonArray)?.Select(step => step as JsonObject).ToList();
    if (actual == null || actual.Count != expected.Count)
    {
        report.Fail(label, $"expected {expected.Count} steps, got {summary["steps"]?.ToJsonString() ?? "null"}");
        return;
    }

    for (var index = 0; index < expected.Count; index++)
    {
        var step = actual[index]!;
        if (!step.Select(pair => pair.Key).SequenceEqual(["name", "kind", "state"]))
            report.Fail(label, $"steps[{index}] must be exactly {{name, kind, state}}, got {step.ToJsonString()}");
        if (step["name"]?.GetValue<string>() != expected[index].Name || step["state"]?.GetValue<string>() != expected[index].State)
            report.Fail(label, $"steps[{index}]: expected {expected[index].Name} {expected[index].State}, got {step.ToJsonString()}");
    }
}

void VerifyNext(JsonObject summary, OracleNext expected, bool pinned, string label, Reporter report)
{
    var commands = (summary["next"]?["commands"] as JsonArray)?.Select(command => command?.GetValue<string>()).ToList();
    if (commands == null || !commands.SequenceEqual(expected.Commands))
        report.Fail(label, $"expected next {string.Join(" then ", expected.Commands)}, got {summary["next"]?.ToJsonString() ?? "null"}");
    if (pinned && commands?.SequenceEqual(["/spec:implement"]) != true)
        report.Fail(label, "expected the hand-off, /spec:implement");
}

void CheckMissingCommandTree(string root, Reporter report)
{
    const string check = "no command tree";
    report.Checked();
    var workingDirectory = Directory.CreateTempSubdirectory("routing-no-commands-").FullName;
    try
    {
        Directory.CreateDirectory(Path.Combine(workingDirectory, "specs", "0001-sample"));
        File.WriteAllText(Path.Combine(workingDirectory, "specs", "0001-sample", "README.md"), "# sample\n");

        var result = RunProcess(workingDirectory, "dotnet",
            ["run", Path.Combine(root, ResolverScript), "--", "specs", "--spec", "0001-sample", "--flows-dir", Path.Combine(root, ShippedFlows)]);

        if (result.ExitCode != 1)
            report.Fail(check, $"expected exit 1, got {result.ExitCode}");
        if (result.Stdout.Length > 0)
            report.Fail(check, "expected empty standard output");
        if (SplitLines(result.Stdout).Any(IsRecordLine))
            report.Fail(check, "expected no record line");
    }
    finally
    {
        Directory.Delete(workingDirectory, true);
    }
}

void CheckCurrentSpecStates(string root, Reporter report)
{
    const string existingSpec = "0001-sample";
    CurrentSpecCase[] cases =
    [
        new("missing", null, "missing", null, "current-spec-missing", ["missing", "/spec:switch", "/spec:new"]),
        new("zero-byte", "", "empty", "", "current-spec-empty", ["empty", "/spec:switch", "/spec:new"]),
        new("whitespace-only", "  \n\t\n", "empty", "", "current-spec-empty", ["empty", "/spec:switch", "/spec:new"]),
        new("stale", "0099-gone\n", "stale", "0099-gone", "current-spec-stale", ["stale", "0099-gone", "/spec:switch", "/spec:new"]),
        new("ok", existingSpec + "\n", "ok", existingSpec, null, [])
    ];

    foreach (var currentCase in cases)
        CheckCurrentSpecState(root, currentCase, existingSpec, report);
}

void CheckCurrentSpecState(string root, CurrentSpecCase currentCase, string existingSpec, Reporter report)
{
    var check = $"current-spec {currentCase.Name}";
    report.Checked();
    var specsRoot = Directory.CreateTempSubdirectory("routing-current-").FullName;
    try
    {
        Directory.CreateDirectory(Path.Combine(specsRoot, existingSpec));
        File.WriteAllText(Path.Combine(specsRoot, existingSpec, "README.md"), "# sample\n");
        if (currentCase.Content != null)
            File.WriteAllText(Path.Combine(specsRoot, ".current-spec"), currentCase.Content);

        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--current", "--flows-dir", ShippedFlows]);
        VerifyCurrentSpecReport(result, currentCase, specsRoot, check, report);
    }
    finally
    {
        Directory.Delete(specsRoot, true);
    }
}

void VerifyCurrentSpecReport(ProcessResult result, CurrentSpecCase currentCase, string specsRoot, string check, Reporter report)
{
    var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
    if (result.ExitCode != 0 || line == null)
    {
        report.Fail(check, $"expected exit 0 and a route-report line, got exit {result.ExitCode} and {(line == null ? "no line" : "a line")}");
        return;
    }

    var actual = (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!;
    var expectedState = new JsonObject { ["state"] = currentCase.State, ["value"] = currentCase.Value };
    foreach (var mismatch in Mismatches(JsonNode.Parse(expectedState.ToJsonString()), actual["current_spec"], "report.current_spec"))
        report.Fail(check, mismatch);

    if (currentCase.StopReason == null)
        VerifyResolvedSpec(actual, specsRoot, check, report);
    else
        VerifyNoSpecForm(actual, currentCase, check, report);
}

void VerifyNoSpecForm(JsonObject actual, CurrentSpecCase currentCase, string check, Reporter report)
{
    string[] keys = ["schema_version", "mode", "current_spec", "spec", "stop"];
    if (!actual.Select(pair => pair.Key).SequenceEqual(keys))
        report.Fail(check, $"expected only the keys {string.Join(", ", keys)}, got {string.Join(", ", actual.Select(pair => pair.Key))}");
    if (actual["mode"]?.GetValue<string>() != "current")
        report.Fail(check, "expected mode current");
    if (actual["spec"] != null)
        report.Fail(check, "expected spec null");
    if (actual["stop"]?["reason"]?.GetValue<string>() != currentCase.StopReason)
        report.Fail(check, $"expected stop.reason {currentCase.StopReason}, got {actual["stop"]?["reason"]?.ToJsonString() ?? "null"}");

    var message = actual["stop"]?["message"]?.GetValue<string>() ?? "";
    foreach (var part in currentCase.MessageParts.Where(part => !message.Contains(part, StringComparison.Ordinal)))
        report.Fail(check, $"expected stop.message to contain \"{part}\", got \"{message}\"");
}

void VerifyResolvedSpec(JsonObject actual, string specsRoot, string check, Reporter report)
{
    if (actual["stop"] != null)
        report.Fail(check, "expected stop null for a usable current spec");
    if (actual["flow"]?["name"]?.GetValue<string>() != "feature")
        report.Fail(check, "expected the named spec to resolve to flow feature");
    if (actual["spec"]?.GetValue<string>() != $"{specsRoot}/0001-sample")
        report.Fail(check, $"expected spec {specsRoot}/0001-sample, got {actual["spec"]?.ToJsonString() ?? "null"}");
}

void CheckShippedFlowDirectory(string root, Reporter report)
{
    report.Checked();
    var directory = Path.Combine(root, ShippedFlows);
    var names = Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory).Select(file => Path.GetFileName(file)).ToList()
        : [];

    foreach (var required in new[] { "feature.flow", "approvals.md" }.Where(name => !names.Contains(name)))
        report.Fail("shipped flows", $"{ShippedFlows} does not hold {required}");
    foreach (var forbidden in new[] { "light.flow", "bugfix.flow" }.Where(names.Contains))
        report.Fail("shipped flows", $"{ShippedFlows} holds {forbidden}, which is fixture-only");
}

void CheckCreationRecords(string root, Reporter report)
{
    string[] tamperCases = ["tamper-a-route-changed", "tamper-b-record-only"];
    var specs = Path.Combine(root, FixtureSpecs);
    foreach (var directory in Directory.EnumerateDirectories(specs).Order(StringComparer.Ordinal))
    {
        var name = Path.GetFileName(directory);
        if (tamperCases.Contains(name) || !File.Exists(Path.Combine(directory, ".flow.record")))
            continue;

        report.Checked();
        var problem = RecordProblem(directory);
        if (problem != null)
            report.Fail($"creation record {name}", $"{problem}; re-run the installation steps for this case");
    }
}

string? RecordProblem(string directory)
{
    var slipPath = Path.Combine(directory, ".flow");
    if (!File.Exists(slipPath))
        return "the case has a .flow.record but no .flow";

    var slip = File.ReadAllText(slipPath).Replace("\r\n", "\n");
    var recorded = ReadLf(Path.Combine(directory, ".flow.record")).Split('\n').ToDictionary(
        line => line.Split(':')[0], line => line.Contains(':') ? line[(line.IndexOf(':') + 1)..].Trim() : "");

    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slip))).ToLowerInvariant();
    var firstLine = slip.Split('\n')[0];
    var flowName = firstLine.StartsWith("# flow:", StringComparison.Ordinal) ? firstLine["# flow:".Length..].Trim() : "";

    if (recorded.GetValueOrDefault("sha256") != hash)
        return $"sha256 is {recorded.GetValueOrDefault("sha256") ?? "missing"}, the normalised .flow hashes to {hash}";
    if (recorded.GetValueOrDefault("flow") != flowName)
        return $"flow is {recorded.GetValueOrDefault("flow") ?? "missing"}, line 1 of .flow names {flowName}";
    return null;
}

void CheckGeneratedTamperCases(string root, Reporter report)
{
    var slip = File.ReadAllBytes(Path.Combine(root, FixtureSpecs, "feature-slip", ".flow"));
    var text = Encoding.UTF8.GetString(slip);
    var tamperedCases = new (string Name, byte[] Slip, bool Valid)[]
    {
        ("gen-one-char", Encoding.UTF8.GetBytes(text.Replace("/spec:design", "/spec:desigx")), false),
        ("gen-trailing-space", Encoding.UTF8.GetBytes(text.Replace("| .requirements-approved |", "| .requirements-approved  |")), true),
        ("gen-bom", [0xEF, 0xBB, 0xBF, .. slip], false)
    };

    foreach (var tamperedCase in tamperedCases)
        CheckGeneratedTamperCase(root, tamperedCase.Name, tamperedCase.Slip, tamperedCase.Valid, report);

    CheckGeneratedUntamperedCases(root, text, report);
    CheckGeneratedUnreadableRecordCase(root, report);
    CheckGeneratedRouteLogCases(root, report);
}

void CheckGeneratedRouteLogCases(string root, Reporter report)
{
    const string twoEntries = "# Route changes\n\n## 2026-11-01: feature -> light\n\nReason: first\n\n## 2026-11-02: light -> feature\n\nReason: second\n";
    const string malformedHeading = "# Route changes\n\n## 2026-11-01: feature -> light\n\nReason: first\n\n## 2026-11-02 light to feature\n\nReason: second\n";
    CheckGeneratedRouteLogCase(root, "gen-log-two", twoEntries, """{ "date": "2026-11-02", "from": "light", "to": "feature", "reason": "second" }""", report);
    CheckGeneratedRouteLogCase(root, "gen-log-malformed", malformedHeading, "null", report);
}

void CheckGeneratedRouteLogCase(string root, string name, string log, string expectedRouteChange, Reporter report)
{
    var check = $"generated {name}";
    report.Checked();
    var specsRoot = Directory.CreateTempSubdirectory("routing-generated-").FullName;
    try
    {
        var spec = Path.Combine(specsRoot, name);
        CopyDirectory(Path.Combine(root, FixtureSpecs, "feature-slip"), spec);
        File.WriteAllText(Path.Combine(spec, "route-changes.md"), log);

        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--spec", name, "--flows-dir", ShippedFlows]);
        var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (result.ExitCode != 0 || line == null)
        {
            report.Fail(check, $"expected exit 0 and a route-report line, got exit {result.ExitCode}");
            return;
        }

        var actual = (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!;
        var expected = JsonNode.Parse($$"""{ "valid": true, "tampered": { "value": false }, "route_change": {{expectedRouteChange}} }""")!;
        foreach (var mismatch in Mismatches(expected, actual, "report"))
            report.Fail(check, mismatch);
    }
    finally
    {
        Directory.Delete(specsRoot, true);
    }
}

void CheckGeneratedUnreadableRecordCase(string root, Reporter report)
{
    const string name = "gen-unreadable-record";
    var check = $"generated {name}";
    report.Checked();
    var specsRoot = Directory.CreateTempSubdirectory("routing-generated-").FullName;
    try
    {
        var spec = Path.Combine(specsRoot, name);
        CopyDirectory(Path.Combine(root, FixtureSpecs, "feature-slip"), spec);
        var recordPath = Path.Combine(spec, ".flow.record");
        var lines = File.ReadAllLines(recordPath).Select(line => line.StartsWith("sha256:", StringComparison.Ordinal) ? "sha256: nothex" : line);
        File.WriteAllLines(recordPath, lines);

        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--spec", name, "--flows-dir", ShippedFlows]);
        var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (result.ExitCode != 0 || line == null)
        {
            report.Fail(check, $"expected exit 0 and a route-report line, got exit {result.ExitCode}");
            return;
        }

        var actual = (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!;
        var expected = JsonNode.Parse("""{ "tampered": { "value": true, "case": "c", "recorded_flow": null } }""")!;
        foreach (var mismatch in Mismatches(expected, actual, "report"))
            report.Fail(check, mismatch);
    }
    finally
    {
        Directory.Delete(specsRoot, true);
    }
}

void CheckGeneratedUntamperedCases(string root, string text, Reporter report)
{
    CheckGeneratedUntamperedCase(root, "gen-crlf", Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n")), report);
    CheckGeneratedUntamperedCase(root, "gen-name with spaces", File.ReadAllBytes(Path.Combine(root, FixtureSpecs, "feature-slip", ".flow")), report);
}

void CheckGeneratedUntamperedCase(string root, string name, byte[] slip, Reporter report)
{
    var check = $"generated {name}";
    report.Checked();
    var specsRoot = Directory.CreateTempSubdirectory("routing-generated-").FullName;
    try
    {
        CopyDirectory(Path.Combine(root, FixtureSpecs, "feature-slip"), Path.Combine(specsRoot, name));
        File.WriteAllBytes(Path.Combine(specsRoot, name, ".flow"), slip);

        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--spec", name, "--flows-dir", ShippedFlows]);
        var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (result.ExitCode != 0 || line == null)
        {
            report.Fail(check, $"expected exit 0 and a route-report line, got exit {result.ExitCode}");
            return;
        }

        var actual = (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!;
        var expected = new JsonObject
        {
            ["valid"] = true,
            ["spec"] = $"{specsRoot}/{name}",
            ["tampered"] = new JsonObject { ["value"] = false }
        };
        foreach (var mismatch in Mismatches(expected, actual, "report"))
            report.Fail(check, mismatch);
    }
    finally
    {
        Directory.Delete(specsRoot, true);
    }
}

void CheckGeneratedTamperCase(string root, string name, byte[] slip, bool valid, Reporter report)
{
    var check = $"generated {name}";
    report.Checked();
    var specsRoot = Directory.CreateTempSubdirectory("routing-generated-").FullName;
    try
    {
        CopyDirectory(Path.Combine(root, FixtureSpecs, "feature-slip"), Path.Combine(specsRoot, name));
        File.WriteAllBytes(Path.Combine(specsRoot, name, ".flow"), slip);

        var result = RunProcess(root, "dotnet", ["run", ResolverScript, "--", specsRoot, "--spec", name, "--flows-dir", ShippedFlows]);
        var line = SplitLines(result.Stdout).LastOrDefault(l => l.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (result.ExitCode != 0 || line == null)
        {
            report.Fail(check, $"expected exit 0 and a route-report line, got exit {result.ExitCode}");
            return;
        }

        var actual = (JsonObject)JsonNode.Parse(line[ReportPrefix.Length..])!;
        var expected = JsonNode.Parse($$"""{ "valid": {{(valid ? "true" : "false")}}, "tampered": { "value": true, "case": "a", "recorded_flow": "feature" } }""")!;
        foreach (var mismatch in Mismatches(expected, actual, "report"))
            report.Fail(check, mismatch);

        if (name == "gen-bom" && !(actual["violations"]?.AsArray().Any(v => v?["rule"]?.GetValue<string>() == "R0") ?? false))
            report.Fail(check, "expected an R0 violation for the byte-order mark on line 1");

        if (name == "gen-bom" && actual["stop"]?["reason"]?.GetValue<string>() != "tampered")
            report.Fail(check, $"expected stop.reason tampered ahead of the R0 invalid stop, got {actual["stop"]?["reason"]}");
    }
    finally
    {
        Directory.Delete(specsRoot, true);
    }
}

void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.EnumerateFiles(source))
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
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

internal sealed record OracleStepState(string Name, string State);

internal sealed record OracleNext(string[] Commands);

internal static class FeatureOracle
{
    private static readonly (string Name, string Producer, string Artefact, string Marker)[] Steps =
    [
        ("requirements", "/spec:requirements", "requirements.md", ".requirements-approved"),
        ("design", "/spec:design", ".adr-list", ".design-approved"),
        ("tasks", "/spec:tasks", "tasks.md", ".tasks-approved")
    ];

    public static IReadOnlyList<OracleStepState> ExpectedSteps(string directory) =>
        Steps.Select(step => new OracleStepState(step.Name, StateOf(directory, step.Artefact, step.Marker))).ToList();

    public static OracleNext ExpectedNext(string directory)
    {
        var first = Steps.FirstOrDefault(step => !File.Exists(Path.Combine(directory, step.Marker)));
        if (first.Name == null)
            return new OracleNext(["/spec:implement"]);

        return ArtefactPresent(directory, first.Artefact)
            ? new OracleNext([$"/spec:review {first.Name}", $"/spec:approve {first.Name}"])
            : new OracleNext([first.Producer]);
    }

    private static string StateOf(string directory, string artefact, string marker) =>
        (File.Exists(Path.Combine(directory, marker)), ArtefactPresent(directory, artefact)) switch
        {
            (true, true) => "approved",
            (true, false) => "approved-no-artefacts",
            (false, true) => "ready",
            _ => "pending"
        };

    private static bool ArtefactPresent(string directory, string artefact)
    {
        var path = Path.Combine(directory, artefact);
        return artefact == ".adr-list"
            ? File.Exists(path) && File.ReadLines(path).Any(line => line.Trim().Length > 0)
            : File.Exists(path);
    }
}

internal sealed record CurrentSpecCase(string Name, string? Content, string State, string? Value, string? StopReason, string[] MessageParts);

internal sealed record Expectation(IReadOnlyList<string> Run, string Flows, int Exit, JsonNode? Report);

internal sealed class Reporter
{
    private static readonly TimeSpan SlowRun = TimeSpan.FromSeconds(10);

    private int _checks;
    private int _failures;

    public void Checked() => _checks++;

    public void WarnIfSlow(string check, TimeSpan elapsed)
    {
        if (elapsed >= SlowRun)
            Console.WriteLine($"WARN {check}: took {elapsed.TotalSeconds:F1} seconds, which is {SlowRun.TotalSeconds:F0} seconds or more");
    }

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
