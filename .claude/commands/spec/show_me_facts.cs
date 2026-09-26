#nullable enable
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

var target = args[0];

var ledger = new JsonObject
{
    ["schema_version"] = 1,
    ["target"] = target,
    ["pinned"] = false,
};

var nullReasons = new JsonObject();

// FR-21's second kind of run: a target not directly under specs/ is not a spec directory, so
// every ref and diff field is null with that reason. Ref resolution for a real spec directory
// is a later task (Phase 6).
if (!target.TrimEnd('/').Split('/')[0].Equals("specs", StringComparison.Ordinal))
{
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

    foreach (var field in refFields.Concat(diffFields))
    {
        ledger[field] = null;
        nullReasons[field] = "not a spec directory";
    }
}

ledger["null_reasons"] = nullReasons;
ledger["gh_commands"] = new JsonArray();

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

File.WriteAllText(Path.Combine(target, ".show-me-ledger.json"), ledger.ToJsonString(options));

return 0;
