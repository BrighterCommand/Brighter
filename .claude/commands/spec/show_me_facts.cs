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
    // FR-21: the pinned row takes precedence over the target's kind.
    ledger["merge_base"] = pinnedBase;
    ledger["measured_head"] = new JsonObject { ["sha"] = pinnedHead, ["source"] = "pinned" };

    foreach (var field in refFields.Where(f => f is not "merge_base" and not "measured_head"))
    {
        ledger[field] = null;
        nullReasons[field] = "pinned";
    }

    // Buckets, net lines, src_subdirectory_count, public_api_lines, commits and src_diff are
    // measured over the pinned pair alone, independent of the target (ADR 0072 IA 6). D3 reads
    // adr_resolved_count, which is computed later, and is filled in below once that is known.
    var (srcDiffCommand, srcDiffBytes) = RunScopedDiff(pinnedBase!, pinnedHead!, "src/");
    var buckets = ComputeBuckets(pinnedBase!, pinnedHead!);
    var srcSubdirectoryCount = ComputeSrcSubdirectoryCount(pinnedBase!, pinnedHead!);
    var publicApiLines = CountPublicApiLines(srcDiffBytes);

    ledger["buckets"] = buckets;
    ledger["src_subdirectory_count"] = srcSubdirectoryCount;
    ledger["public_api_lines"] = publicApiLines;
    ledger["commits"] = CountCommits(pinnedBase!, pinnedHead!);
    ledger["src_diff"] = new JsonObject
    {
        ["command"] = srcDiffCommand,
        ["bytes"] = srcDiffBytes.Length,
        ["windows"] = ComputeWindows(srcDiffBytes),
    };
    ledger["f1_level"] = ComputeF1Level((int)buckets["src"]!["files"]!);
    ledger["triggers"] = new JsonObject
    {
        ["d1"] = ComputeD1((int)buckets["src"]!["files"]!, srcSubdirectoryCount),
        ["d2"] = ComputeD2(publicApiLines),
        ["d3"] = null,
    };
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

// D3 reads adr_resolved_count (FR-6 (a)), known only now that .adr-list has been resolved.
if (pinned)
{
    ledger["triggers"]!["d3"] = ComputeD3(adrResolvedCount);
}

// Marked release-notes sections (FR-7, FR-21's {m} rule) are file-derived too, read from the
// working tree on every kind of run — the release-notes path is a test-only input (ADR 0072
// KC1), not a fault hook, and it never nulls on a pinned or fixture run.
ledger["release_notes"] = ReadReleaseNotes(target, releaseNotesPath);

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

// Runs git and captures stdout as raw bytes — needed for the src-scoped diff, whose exact byte
// count and windows are charged (NFR-3), so it must not go through a string round-trip.
static byte[] RunGitBytes(params string[] args)
{
    var startInfo = new ProcessStartInfo("git")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var arg in args)
    {
        startInfo.ArgumentList.Add(arg);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("could not start git");
    using var stdout = new MemoryStream();
    process.StandardOutput.BaseStream.CopyTo(stdout);
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return stdout.ToArray();
}

static string RunGitText(params string[] args) => Encoding.UTF8.GetString(RunGitBytes(args));

// ADR 0072 IA 6: the src-scoped diff is the one full-content diff the script reads — over
// exactly the pinned (or measured) pair, bounded by a pathspec (NFR-3).
static (string Command, byte[] Bytes) RunScopedDiff(string mergeBase, string measuredHead, string pathspec)
{
    var bytes = RunGitBytes("diff", $"{mergeBase}..{measuredHead}", "--", pathspec);
    var command = $"git diff {mergeBase}..{measuredHead} -- {pathspec}";
    return (command, bytes);
}

// FR-10's six buckets (src/, tests/, docs/, specs/, .github/, other) plus their total, each
// {files, added, removed}. Net lines come from --numstat (a summary flag, never a whole diff);
// binary files report "-" for both columns, treated as 0 per the task's counting rule.
static JsonObject ComputeBuckets(string mergeBase, string measuredHead)
{
    var numstatLines = RunGitText("diff", "--numstat", $"{mergeBase}..{measuredHead}")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    var bucketNames = new[] { "src", "tests", "docs", "specs", "github", "other" };
    var files = bucketNames.ToDictionary(name => name, _ => 0);
    var added = bucketNames.ToDictionary(name => name, _ => 0L);
    var removed = bucketNames.ToDictionary(name => name, _ => 0L);

    foreach (var line in numstatLines)
    {
        var parts = line.Split('\t');
        var lineAdded = parts[0] == "-" ? 0L : long.Parse(parts[0]);
        var lineRemoved = parts[1] == "-" ? 0L : long.Parse(parts[1]);
        var bucket = BucketFor(parts[2]);

        files[bucket]++;
        added[bucket] += lineAdded;
        removed[bucket] += lineRemoved;
    }

    var buckets = new JsonObject();
    foreach (var name in bucketNames)
    {
        buckets[name] = new JsonObject { ["files"] = files[name], ["added"] = added[name], ["removed"] = removed[name] };
    }

    buckets["total"] = new JsonObject
    {
        ["files"] = files.Values.Sum(),
        ["added"] = added.Values.Sum(),
        ["removed"] = removed.Values.Sum(),
    };

    return buckets;
}

// FR-11's F1 thresholds: Low <= 10, Medium 11-50, High > 50 changed src/ files.
static string ComputeF1Level(int srcFiles) => srcFiles switch
{
    <= 10 => "Low",
    <= 50 => "Medium",
    _ => "High",
};

// FR-6 (a) D1: fires only when the spec diff changes >= 5 files under src/ and those files span
// >= 2 distinct immediate subdirectories of src/ (Definitions).
static bool ComputeD1(int srcFiles, int srcSubdirectoryCount) => srcFiles >= 5 && srcSubdirectoryCount >= 2;

// FR-6 (a) D2: fires at >= 10 changed public API declaration lines.
static bool ComputeD2(int publicApiLines) => publicApiLines >= 10;

// FR-6 (a) D3: fires at >= 2 resolved .adr-list entries.
static bool ComputeD3(int adrResolvedCount) => adrResolvedCount >= 2;

static string BucketFor(string path) => path.Split('/')[0] switch
{
    "src" => "src",
    "tests" => "tests",
    "docs" => "docs",
    "specs" => "specs",
    ".github" => "github",
    _ => "other",
};

// Definitions, Immediate subdirectory of src/: for src/{X}/…, {X} is the contributed subdirectory;
// a file directly under src/ (no further segment) contributes none. Counts distinct {X} values.
static int ComputeSrcSubdirectoryCount(string mergeBase, string measuredHead)
{
    var paths = RunGitText("diff", "--name-only", $"{mergeBase}..{measuredHead}", "--", "src/")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    var subdirectories = new HashSet<string>(StringComparer.Ordinal);
    foreach (var path in paths)
    {
        var segments = path.Split('/');
        if (segments.Length > 2)
        {
            subdirectories.Add(segments[1]);
        }
    }

    return subdirectories.Count;
}

// Public API declaration line — the exact rule (requirements.md § Definitions), POSIX
// ^[+-][[:space:]]*(public|protected)[[:space:]], translated to .NET's dialect. Counted per diff
// line over the src-scoped diff, so a modified declaration (one "-" line, one "+" line) counts 2.
static int CountPublicApiLines(byte[] srcDiffBytes)
{
    var lines = Encoding.UTF8.GetString(srcDiffBytes).Split('\n');
    return lines.Count(line => PublicApiPatterns.DeclarationLine.IsMatch(line.TrimEnd('\r')));
}

static int CountCommits(string mergeBase, string measuredHead) =>
    int.Parse(RunGitText("rev-list", "--count", $"{mergeBase}..{measuredHead}").Trim());

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

// Marked release-notes sections (Definitions, FR-7, FR-21's {m} rule). Reads the release-notes
// path given by NFR-9's test-only input, or the repository-root release_notes.md by default.
// An absent file gives present false, count 0, an empty sections array and m null.
static JsonObject ReadReleaseNotes(string target, string? releaseNotesPathOverride)
{
    var path = releaseNotesPathOverride ?? "release_notes.md";

    if (!File.Exists(path))
    {
        return new JsonObject
        {
            ["path"] = path,
            ["present"] = false,
            ["count"] = 0,
            ["sections"] = new JsonArray(),
            ["m"] = null,
        };
    }

    var content = File.ReadAllBytes(path);
    var targetName = Path.GetFileName(target.TrimEnd('/'));
    var marker = $"<!-- spec: {targetName} -->";

    var markedSections = FindMarkedSections(content, marker);

    var sectionsArray = new JsonArray();
    int? m = null;

    foreach (var (startLine, sectionBytes) in markedSections)
    {
        sectionsArray.Add(new JsonObject
        {
            ["bytes"] = sectionBytes.Length,
            ["windows"] = ComputeWindows(sectionBytes, startLine: startLine),
        });

        var sectionM = CountBreakingChangeBullets(sectionBytes);
        if (sectionM.HasValue)
        {
            m = (m ?? 0) + sectionM.Value;
        }
    }

    return new JsonObject
    {
        ["path"] = path,
        ["present"] = true,
        ["count"] = markedSections.Count,
        ["sections"] = sectionsArray,
        ["m"] = m,
    };
}

// A marked section (Definitions): a `###` heading, outside any fence, whose very next line,
// trimmed, equals the marker literally. It runs to the next `##` or `###` heading outside a
// fence, or to the end of the file — a `####` subsection (Breaking changes, Usage) stays inside.
static List<(int StartLine, byte[] Content)> FindMarkedSections(byte[] content, string marker)
{
    var lineLengths = ComputeLineByteLengths(content);
    var lineCount = lineLengths.Count;
    var rawLines = Encoding.UTF8.GetString(content).Split('\n');
    var offsets = ComputeLineOffsets(lineLengths);

    var startIndexes = new List<int>();
    var inFence = false;
    for (var i = 0; i < lineCount; i++)
    {
        var line = rawLines[i].TrimEnd('\r');
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            inFence = !inFence;
            continue;
        }

        if (!inFence
            && ReleaseNotesPatterns.SectionHeading.IsMatch(line)
            && i + 1 < lineCount
            && rawLines[i + 1].TrimEnd('\r').Trim() == marker)
        {
            startIndexes.Add(i);
        }
    }

    var sections = new List<(int, byte[])>();
    foreach (var startIndex in startIndexes)
    {
        var endIndex = lineCount - 1;
        var fence = false;
        for (var j = startIndex + 1; j < lineCount; j++)
        {
            var line = rawLines[j].TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
                continue;
            }

            if (!fence && ReleaseNotesPatterns.SectionBoundaryHeading.IsMatch(line))
            {
                endIndex = j - 1;
                break;
            }
        }

        var startByte = offsets[startIndex];
        var endByte = offsets[endIndex + 1];
        sections.Add((startIndex + 1, content[(int)startByte..(int)endByte]));
    }

    return sections;
}

// FR-21's {m} rule: null when the section carries no `#### Breaking changes` heading; otherwise
// the count of lines starting `- ` in column 0 between that heading and the next heading of any
// level (or the section's end), skipping fenced lines and indented sub-bullets.
static int? CountBreakingChangeBullets(byte[] sectionContent)
{
    var lineLengths = ComputeLineByteLengths(sectionContent);
    var lineCount = lineLengths.Count;
    var rawLines = Encoding.UTF8.GetString(sectionContent).Split('\n');

    var headingIndex = -1;
    var inFence = false;
    for (var i = 0; i < lineCount; i++)
    {
        var line = rawLines[i].TrimEnd('\r');
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            inFence = !inFence;
            continue;
        }

        if (!inFence && line == "#### Breaking changes")
        {
            headingIndex = i;
            break;
        }
    }

    if (headingIndex < 0)
    {
        return null;
    }

    var count = 0;
    inFence = false;
    for (var j = headingIndex + 1; j < lineCount; j++)
    {
        var line = rawLines[j].TrimEnd('\r');
        if (line.StartsWith("```", StringComparison.Ordinal))
        {
            inFence = !inFence;
            continue;
        }

        if (!inFence && ReleaseNotesPatterns.AnyHeading.IsMatch(line))
        {
            break;
        }

        if (!inFence && line.StartsWith("- ", StringComparison.Ordinal))
        {
            count++;
        }
    }

    return count;
}

static class PublicApiPatterns
{
    // POSIX (requirements.md, Public API declaration line): ^[+-][[:space:]]*(public|protected)[[:space:]]
    public static readonly Regex DeclarationLine = new(@"^[+-]\s*(public|protected)\s");
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

static class ReleaseNotesPatterns
{
    // A level-3 heading — the only level a marked section can start at (Definitions).
    public static readonly Regex SectionHeading = new(@"^###\s");

    // A level-2 or level-3 heading — bounds a marked section's extent; a #### subsection
    // (Breaking changes, Usage) stays inside it.
    public static readonly Regex SectionBoundaryHeading = new(@"^#{2,3}\s");

    // A heading of any level — bounds the #### Breaking changes list ({m} rule).
    public static readonly Regex AnyHeading = new(@"^#{1,6}\s");
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
