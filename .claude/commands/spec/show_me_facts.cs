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

// requirements.md's declared ids are file-derived too, computed the same way on every kind of run.
var requirementsPath = Path.Combine(target, "requirements.md");
if (File.Exists(requirementsPath))
{
    var requirementsBytes = File.ReadAllBytes(requirementsPath);
    var (declaredIds, declarations) = ExtractDeclaredIds(requirementsBytes);

    ledger["requirements"] = new JsonObject
    {
        ["present"] = true,
        ["bytes"] = requirementsBytes.Length,
        ["windows"] = ComputeWindows(requirementsBytes),
    };
    ledger["declared_ids"] = new JsonArray(declaredIds.Select(id => (JsonNode)id).ToArray());
    ledger["declared_total"] = declaredIds.Count;
    ledger["declarations"] = declarations;
}
else
{
    ledger["requirements"] = new JsonObject
    {
        ["present"] = false,
        ["bytes"] = null,
        ["windows"] = null,
    };
    ledger["declared_ids"] = null;
    ledger["declared_total"] = null;
    ledger["declarations"] = null;
    nullReasons["requirements"] = "not present";
    nullReasons["declared_ids"] = "not present";
    nullReasons["declared_total"] = "not present";
    nullReasons["declarations"] = "not present";
}

// .adr-list entry resolution (FR-16 row 7) is file-derived too, computed on every kind of run.
var (adrList, adrResolvedCount) = ReadAdrList(target);
ledger["adr_list"] = adrList;
ledger["adr_resolved_count"] = adrResolvedCount;

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
// line already over that limit forms a window of its own, flagged oversize. `startLine` offsets
// the reported line numbers for content that is a slice of a larger file (a declaration's
// paragraph), rather than the whole file.
static JsonArray ComputeWindows(byte[] content, int startLine = 1)
{
    const int MaxWindowBytes = 25_000;

    var lines = ComputeLineByteLengths(content);

    var windows = new List<JsonObject>();
    var windowStartLine = startLine;
    var windowBytes = 0;
    var windowLineCount = 0;

    for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
    {
        var lineNumber = startLine + lineIndex;
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
        windows.Add(Window(windowStartLine, startLine + lines.Count - 1, windowBytes, oversize: false));
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

// Byte length of each line (including its trailing '\n', when present) in file order.
static List<int> ComputeLineByteLengths(byte[] content)
{
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
    return lines;
}

// The byte offset each line starts at, given its byte lengths (offsets[i]..offsets[i+1] is line i).
static long[] ComputeLineOffsets(List<int> lineLengths)
{
    var offsets = new long[lineLengths.Count + 1];
    for (var i = 0; i < lineLengths.Count; i++)
    {
        offsets[i + 1] = offsets[i] + lineLengths[i];
    }
    return offsets;
}

// FR-8's declared-id set and each declaration's paragraph (ADR 0072 IA 4). A paragraph runs
// from its declaration line to the line before the next declaration or the next heading of
// level three or higher (###, ##, or #), whichever comes first, or to the end of the file.
static (List<string> DeclaredIds, JsonArray Declarations) ExtractDeclaredIds(byte[] content)
{
    var lineLengths = ComputeLineByteLengths(content);
    var rawLines = Encoding.UTF8.GetString(content).Split('\n');
    var offsets = ComputeLineOffsets(lineLengths);

    var declarationLineIndexes = new List<int>();
    var idsInFileOrder = new List<string>();

    for (var i = 0; i < lineLengths.Count; i++)
    {
        var line = rawLines[i].TrimEnd('\r');
        var match = RequirementsPatterns.DeclaredId.Match(line);
        if (match.Success)
        {
            declarationLineIndexes.Add(i);
            idsInFileOrder.Add($"{match.Groups["prefix"].Value}-{match.Groups["num"].Value}");
        }
    }

    var declarations = new JsonArray();
    for (var d = 0; d < declarationLineIndexes.Count; d++)
    {
        var startIndex = declarationLineIndexes[d];
        var endIndex = lineLengths.Count - 1;

        for (var j = startIndex + 1; j < lineLengths.Count; j++)
        {
            var line = rawLines[j].TrimEnd('\r');
            if (RequirementsPatterns.DeclaredId.IsMatch(line) || RequirementsPatterns.Heading.IsMatch(line))
            {
                endIndex = j - 1;
                break;
            }
        }

        var startByte = offsets[startIndex];
        var endByte = offsets[endIndex + 1];
        var paragraphBytes = content[(int)startByte..(int)endByte];

        declarations.Add(new JsonObject
        {
            ["id"] = idsInFileOrder[d],
            ["bytes"] = paragraphBytes.Length,
            ["windows"] = ComputeWindows(paragraphBytes, startLine: startIndex + 1),
        });
    }

    var declaredIds = idsInFileOrder
        .Distinct()
        .OrderBy(id => id.StartsWith("NFR-", StringComparison.Ordinal) ? 1 : 0)
        .ThenBy(id => int.Parse(id[(id.StartsWith("NFR-", StringComparison.Ordinal) ? 4 : 3)..]))
        .ToList();

    return (declaredIds, declarations);
}

// FR-16 row 7: .adr-list entry resolution. A full filename, or docs/adr/{filename}, resolves to
// that file; anything else does not. A bare number that matches more than one docs/adr/ file is
// ambiguous; one that matches exactly one file (or none) still does not resolve.
static (JsonArray AdrList, int ResolvedCount) ReadAdrList(string target)
{
    var adrListPath = Path.Combine(target, ".adr-list");
    var entries = File.Exists(adrListPath)
        ? File.ReadAllLines(adrListPath).Select(line => line.Trim()).Where(line => line.Length > 0).ToList()
        : [];

    var adrList = new JsonArray();
    var resolvedCount = 0;

    foreach (var entry in entries)
    {
        var (path, reason, matches) = ResolveAdrEntry(entry);

        adrList.Add(new JsonObject
        {
            ["entry"] = entry,
            ["path"] = path,
            ["reason"] = reason,
            ["matches"] = matches is null ? null : new JsonArray(matches.Select(m => (JsonNode)m).ToArray()),
            ["extract"] = path is null ? null : BuildAdrExtract(path),
        });

        if (path is not null)
        {
            resolvedCount++;
        }
    }

    return (adrList, resolvedCount);
}

static (string? Path, string? Reason, List<string>? Matches) ResolveAdrEntry(string entry)
{
    const string AdrDirectory = "docs/adr";

    string? candidateFilename = entry.StartsWith("docs/adr/", StringComparison.Ordinal)
        ? entry["docs/adr/".Length..]
        : !entry.Contains('/') ? entry : null;

    if (candidateFilename is not null)
    {
        var candidatePath = $"{AdrDirectory}/{candidateFilename}";
        if (File.Exists(candidatePath))
        {
            return (candidatePath, null, null);
        }
    }

    if (AdrPatterns.BareNumber.IsMatch(entry))
    {
        var matches = Directory.GetFiles(AdrDirectory, $"{entry}-*.md")
            .Select(path => Path.GetFileName(path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (matches.Count > 1)
        {
            return (null, "ambiguous ADR number", matches);
        }
    }

    return (null, "ADR file not found", null);
}

// Each resolved ADR's extract (ADR 0072 IA 4): front matter, ## Status and ## Consequences, each
// a {part, bytes, windows}. A heading is recognised only outside a fenced code block, so a `#`
// inside a diagram fence never bounds a section, and a section's own ### subheadings (Positive,
// Negative, Risks and Mitigations) stay inside it.
static JsonArray BuildAdrExtract(string adrPath)
{
    var content = File.ReadAllBytes(adrPath);
    var lineLengths = ComputeLineByteLengths(content);
    var lineCount = lineLengths.Count;
    var rawLines = Encoding.UTF8.GetString(content).Split('\n');
    var offsets = ComputeLineOffsets(lineLengths);

    var (frontMatterStart, frontMatterEnd) = FindFrontMatterLineRange(rawLines, lineCount);
    var (statusStart, statusEnd) = FindHeadingSectionLineRange(rawLines, lineCount, "## Status");
    var (consequencesStart, consequencesEnd) = FindHeadingSectionLineRange(rawLines, lineCount, "## Consequences");

    return new JsonArray(
        BuildExtractPart("front_matter", offsets, content, frontMatterStart, frontMatterEnd),
        BuildExtractPart("status", offsets, content, statusStart, statusEnd),
        BuildExtractPart("consequences", offsets, content, consequencesStart, consequencesEnd));
}

static (int Start, int End) FindFrontMatterLineRange(string[] rawLines, int lineCount)
{
    if (lineCount > 0 && rawLines[0].TrimEnd('\r') == "---")
    {
        for (var i = 1; i < lineCount; i++)
        {
            if (rawLines[i].TrimEnd('\r') == "---")
            {
                return (0, i);
            }
        }
    }

    return (0, -1);
}

static (int Start, int End) FindHeadingSectionLineRange(string[] rawLines, int lineCount, string headingText)
{
    var inFence = false;
    var startIndex = -1;

    for (var i = 0; i < lineCount; i++)
    {
        var line = rawLines[i].TrimEnd('\r');
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            inFence = !inFence;
            continue;
        }

        if (startIndex < 0)
        {
            if (!inFence && line == headingText)
            {
                startIndex = i;
            }
            continue;
        }

        if (!inFence && AdrPatterns.SectionBoundaryHeading.IsMatch(line))
        {
            return (startIndex, i - 1);
        }
    }

    return startIndex < 0 ? (0, -1) : (startIndex, lineCount - 1);
}

static JsonObject BuildExtractPart(string part, long[] offsets, byte[] content, int startIndex, int endIndex)
{
    if (endIndex < startIndex)
    {
        return new JsonObject { ["part"] = part, ["bytes"] = 0, ["windows"] = new JsonArray() };
    }

    var startByte = offsets[startIndex];
    var endByte = offsets[endIndex + 1];
    var partBytes = content[(int)startByte..(int)endByte];

    return new JsonObject
    {
        ["part"] = part,
        ["bytes"] = partBytes.Length,
        ["windows"] = ComputeWindows(partBytes, startLine: startIndex + 1),
    };
}

static class RequirementsPatterns
{
    // POSIX (requirements.md, Declared-id pattern): ^[[:space:]]*(-[[:space:]]+)?\*\*(FR|NFR)-[0-9]+
    public static readonly Regex DeclaredId = new(@"^\s*(-\s+)?\*\*(?<prefix>FR|NFR)-(?<num>[0-9]+)");

    // A markdown heading of level three or higher (###, ##, or #) — stops a declaration's paragraph.
    public static readonly Regex Heading = new(@"^#{1,3}\s");
}

static class AdrPatterns
{
    // FR-16 row 7: a bare-number entry is checked for ambiguity against docs/adr/{number}-*.md.
    public static readonly Regex BareNumber = new(@"^[0-9]+$");

    // A markdown heading of level one or two — bounds an ADR's ## Status / ## Consequences
    // section boundary (unlike the declared-id paragraph rule, level three does not stop it).
    public static readonly Regex SectionBoundaryHeading = new(@"^#{1,2}\s");
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
