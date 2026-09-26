#nullable enable
using System.Diagnostics;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

const int UsageErrorExitCode = 1;
const int ToolingFaultExitCode = 1;

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
