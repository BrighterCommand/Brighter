#nullable enable
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

const int SuccessExitCode = 0;
const int UsageErrorExitCode = 1;
const int SchemaVersion = 1;

// The read-only route resolver (spec 0040, ADR 0089). Run from the repository root:
// dotnet run .claude/commands/spec/resolve_route.cs -- {specs-root} {mode} [--flows-dir {dir}]
// A usage error exits 1 and writes nothing to standard output.

var invocation = Invocation.Parse(args);
if (invocation is null)
{
    return UsageErrorExitCode;
}

if (invocation.Mode == RouteMode.Definition)
{
    ReportWriter.Write(new JsonObject
    {
        ["schema_version"] = SchemaVersion,
        ["mode"] = "definition"
    });
}

return SuccessExitCode;

internal enum RouteMode
{
    Spec,
    Current,
    All,
    Definition
}

internal sealed record Invocation(string SpecsRoot, RouteMode Mode, string? Target, string? FlowsDirectory)
{
    private const string FlowsDirectoryOption = "--flows-dir";

    public static Invocation? Parse(string[] args)
    {
        if (args.Length < 2 || !TryParseMode(args[1], out var mode))
        {
            return null;
        }

        var optionsStart = TakesTarget(mode) ? 3 : 2;
        if (args.Length < optionsStart)
        {
            return null;
        }

        var target = TakesTarget(mode) ? args[2] : null;
        return ParseOptions(args[optionsStart..], out var flowsDirectory)
            ? new Invocation(args[0].TrimEnd('/'), mode, target, flowsDirectory)
            : null;
    }

    private static bool TryParseMode(string flag, out RouteMode mode)
    {
        (var known, mode) = flag switch
        {
            "--spec" => (true, RouteMode.Spec),
            "--current" => (true, RouteMode.Current),
            "--all" => (true, RouteMode.All),
            "--definition" => (true, RouteMode.Definition),
            _ => (false, default(RouteMode))
        };
        return known;
    }

    private static bool TakesTarget(RouteMode mode) => mode is RouteMode.Spec or RouteMode.Definition;

    private static bool ParseOptions(string[] options, out string? flowsDirectory)
    {
        flowsDirectory = null;
        if (options.Length == 0)
        {
            return true;
        }

        if (options.Length != 2 || options[0] != FlowsDirectoryOption)
        {
            return false;
        }

        flowsDirectory = options[1];
        return true;
    }
}

internal static class ReportWriter
{
    private const string ReportPrefix = "route-report: ";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Write(JsonObject report) =>
        Console.Out.Write(ReportPrefix + report.ToJsonString(Options) + "\n");
}
