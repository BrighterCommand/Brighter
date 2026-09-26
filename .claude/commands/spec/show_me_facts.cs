#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const int UsageErrorExitCode = 1;
const int ToolingFaultExitCode = 1;
const int GateNotPassedExitCode = 2;

// ADR 0072 KC1's argument grammar: {target}; {target} --pinned {base} {head};
// {target} --release-notes {path}; {target} with both, in either order. Anything else — an
// absent target, an unrecognised option, or a malformed one — is a usage error, which exits
// with a fixed status other than 0 or 2 and writes no ledger. {file} --word-count is a later
// task's mode and is not recognised here yet, so it also falls through to a usage error.

if (args.Length == 0)
{
    return UsageErrorExitCode;
}

var target = args[0];
if (target.StartsWith("--", StringComparison.Ordinal))
{
    return UsageErrorExitCode;
}

string? pinnedBase = null;
string? pinnedHead = null;
string? releaseNotesPath = null;

var i = 1;
while (i < args.Length)
{
    switch (args[i])
    {
        case "--pinned":
            if (i + 2 >= args.Length)
            {
                return UsageErrorExitCode;
            }
            pinnedBase = args[i + 1];
            pinnedHead = args[i + 2];
            i += 3;
            break;
        case "--release-notes":
            if (i + 1 >= args.Length)
            {
                return UsageErrorExitCode;
            }
            releaseNotesPath = args[i + 1];
            i += 2;
            break;
        default:
            return UsageErrorExitCode;
    }
}

// FR-3's completeness gate: evaluated from the file fields before any ref resolution or gh
// call. A run that fails it writes no ledger and reports the gate facts on stderr instead,
// exiting 2 — the only status that distinguishes this stop from a tooling fault.
var tasksPath = Path.Combine(target, "tasks.md");
if (!File.Exists(tasksPath))
{
    WriteGateRecord(new JsonObject { ["case"] = "tasks.md absent" });
    return GateNotPassedExitCode;
}

var tasksInfo = CountTasks(File.ReadAllBytes(tasksPath));
var tasksTotal = (int)tasksInfo["total"]!;
var tasksUnchecked = (int)tasksInfo["unchecked"]!;

if (tasksTotal == 0)
{
    WriteGateRecord(new JsonObject { ["case"] = "zero checkboxes" });
    return GateNotPassedExitCode;
}

if (tasksUnchecked > 0)
{
    WriteGateRecord(new JsonObject
    {
        ["case"] = "unchecked",
        ["unchecked"] = tasksUnchecked,
        ["total"] = tasksTotal,
        ["first_unchecked"] = JsonNode.Parse(tasksInfo["first_unchecked"]!.ToJsonString()),
    });
    return GateNotPassedExitCode;
}

var pinned = pinnedBase is not null && pinnedHead is not null;

if (pinned && (!CommitExists(pinnedBase!) || !CommitExists(pinnedHead!)))
{
    return ToolingFaultExitCode;
}

var ledger = new JsonObject
{
    ["schema_version"] = 1,
    ["target"] = target,
    ["pinned"] = pinned,
};

var nullReasons = new JsonObject();

string[] refFields =
[
    "spec_branch", "rules_tried", "local_divergence", "base",
    "pr", "pr_count", "measured_head", "merge_base",
];
string[] diffFields =
[
    "buckets", "src_subdirectory_count", "public_api_lines",
    "commits", "src_diff", "f1_level", "triggers",
];

if (pinned)
{
    // FR-21: the pinned row takes precedence over the target's kind. Diff-field computation
    // for a pinned run is Phase 5's job; they are nulled here as a placeholder.
    ledger["merge_base"] = pinnedBase;
    ledger["measured_head"] = new JsonObject { ["sha"] = pinnedHead, ["source"] = "pinned" };

    foreach (var field in refFields.Where(f => f is not "merge_base" and not "measured_head"))
    {
        ledger[field] = null;
        nullReasons[field] = "pinned";
    }

    foreach (var field in diffFields)
    {
        ledger[field] = null;
        nullReasons[field] = "pinned";
    }
}
else if (!IsUnderSpecs(target))
{
    // FR-21's second kind of run: a target not directly under specs/ is not a spec directory.
    foreach (var field in refFields.Concat(diffFields))
    {
        ledger[field] = null;
        nullReasons[field] = "not a spec directory";
    }
}
// else: a real, unpinned spec directory — ref resolution is a later task (Phase 6).

// tasks.md's counts are file-derived figures, read from the working tree on every kind of run,
// pinned or not (NFR-9) — the gate above already computed them.
ledger["tasks"] = tasksInfo;

ledger["null_reasons"] = nullReasons;
ledger["gh_commands"] = new JsonArray();

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

File.WriteAllText(Path.Combine(target, ".show-me-ledger.json"), ledger.ToJsonString(options));

return 0;

static bool IsUnderSpecs(string target) =>
    target.TrimEnd('/').Split('/')[0].Equals("specs", StringComparison.Ordinal);

static void WriteGateRecord(JsonObject record)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    Console.Error.WriteLine($"show-me-gate: {record.ToJsonString(options)}");
}

static bool CommitExists(string sha)
{
    var startInfo = new ProcessStartInfo("git")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("cat-file");
    startInfo.ArgumentList.Add("-e");
    startInfo.ArgumentList.Add($"{sha}^{{commit}}");

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("could not start git");
    process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode == 0;
}

static JsonObject CountTasks(byte[] content)
{
    var lines = Encoding.UTF8.GetString(content).Split('\n');

    var total = 0;
    var uncheckedCount = 0;
    var firstUnchecked = new List<string>();
    var byTag = new Dictionary<string, int>
    {
        ["TEST + IMPLEMENT"] = 0,
        ["STRUCTURAL"] = 0,
        ["PROJECT"] = 0,
        ["DOC"] = 0,
        ["untagged"] = 0,
    };

    foreach (var rawLine in lines)
    {
        var line = rawLine.TrimEnd('\r');
        var checkboxMatch = TaskPatterns.Checkbox.Match(line);
        if (!checkboxMatch.Success)
        {
            continue;
        }

        total++;

        var tagMatch = TaskPatterns.Tag.Match(line);
        byTag[tagMatch.Success ? tagMatch.Groups[1].Value : "untagged"]++;

        if (TaskPatterns.Unchecked.IsMatch(line))
        {
            uncheckedCount++;
            if (firstUnchecked.Count < 3)
            {
                firstUnchecked.Add(line[checkboxMatch.Length..].Trim());
            }
        }
    }

    return new JsonObject
    {
        ["total"] = total,
        ["checked"] = total - uncheckedCount,
        ["unchecked"] = uncheckedCount,
        ["first_unchecked"] = new JsonArray(firstUnchecked.Select(title => (JsonNode)title).ToArray()),
        ["by_tag"] = new JsonObject
        {
            ["TEST + IMPLEMENT"] = byTag["TEST + IMPLEMENT"],
            ["STRUCTURAL"] = byTag["STRUCTURAL"],
            ["PROJECT"] = byTag["PROJECT"],
            ["DOC"] = byTag["DOC"],
            ["untagged"] = byTag["untagged"],
        },
        ["bytes"] = content.Length,
        ["windows"] = ComputeWindows(content),
    };
}

// Shared window helper (ADR 0072 IA 4): whole lines, at most 25,000 B per window; a single
// line already over that limit forms a window of its own, flagged oversize.
static JsonArray ComputeWindows(byte[] content)
{
    const int MaxWindowBytes = 25_000;

    var lines = new List<int>();
    var lineStart = 0;
    for (var i = 0; i < content.Length; i++)
    {
        if (content[i] == (byte)'\n')
        {
            lines.Add(i + 1 - lineStart);
            lineStart = i + 1;
        }
    }
    if (lineStart < content.Length)
    {
        lines.Add(content.Length - lineStart);
    }

    var windows = new List<JsonObject>();
    var windowStartLine = 1;
    var windowBytes = 0;
    var windowLineCount = 0;

    for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
    {
        var lineNumber = lineIndex + 1;
        var lineBytes = lines[lineIndex];

        if (lineBytes > MaxWindowBytes)
        {
            if (windowLineCount > 0)
            {
                windows.Add(Window(windowStartLine, lineNumber - 1, windowBytes, oversize: false));
                windowBytes = 0;
                windowLineCount = 0;
            }
            windows.Add(Window(lineNumber, lineNumber, lineBytes, oversize: true));
            windowStartLine = lineNumber + 1;
            continue;
        }

        if (windowLineCount > 0 && windowBytes + lineBytes > MaxWindowBytes)
        {
            windows.Add(Window(windowStartLine, lineNumber - 1, windowBytes, oversize: false));
            windowStartLine = lineNumber;
            windowBytes = 0;
            windowLineCount = 0;
        }

        windowBytes += lineBytes;
        windowLineCount++;
    }

    if (windowLineCount > 0)
    {
        windows.Add(Window(windowStartLine, lines.Count, windowBytes, oversize: false));
    }

    return new JsonArray(windows.Select(w => (JsonNode)w).ToArray());

    static JsonObject Window(int firstLine, int lastLine, int bytes, bool oversize) => new()
    {
        ["first_line"] = firstLine,
        ["last_line"] = lastLine,
        ["bytes"] = bytes,
        ["oversize"] = oversize,
    };
}

static class TaskPatterns
{
    // POSIX (requirements.md, Task checkbox pattern): ^[[:space:]]*-[[:space:]]\[[ xX]\]
    public static readonly Regex Checkbox = new(@"^\s*-\s\[[ xX]\]");

    // An unchecked box specifically — the same anchor, with a literal space between the brackets.
    public static readonly Regex Unchecked = new(@"^\s*-\s\[ \]");

    // POSIX (requirements.md, Task-type tag pattern):
    // ^[[:space:]]*-[[:space:]]\[[ xX]\][[:space:]]*\*\*(TEST [+] IMPLEMENT|STRUCTURAL|PROJECT|DOC):
    public static readonly Regex Tag = new(@"^\s*-\s\[[ xX]\]\s*\*\*(TEST \+ IMPLEMENT|STRUCTURAL|PROJECT|DOC):");
}
