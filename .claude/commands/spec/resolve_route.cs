#nullable enable
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

const int SuccessExitCode = 0;
const int UsageErrorExitCode = 1;

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
    ReportWriter.Write(DefinitionReport.For(invocation));
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

internal static class ReportSchema
{
    public const int Version = 1;
}

internal static class SlipReader
{
    private static readonly UTF8Encoding KeepsByteOrderMark = new(false);

    public static IReadOnlyList<string> ReadLines(string path) =>
        KeepsByteOrderMark.GetString(File.ReadAllBytes(path))
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd(' ', '\t'))
            .ToList();
}

internal static class DefinitionReport
{
    private const string FlowExtension = ".flow";

    public static JsonObject For(Invocation invocation)
    {
        var name = invocation.Target!;
        var path = Path.Combine(invocation.FlowsDirectory!, name + FlowExtension);
        var lines = SlipReader.ReadLines(path);
        return new JsonObject
        {
            ["schema_version"] = ReportSchema.Version,
            ["mode"] = "definition",
            ["flow"] = new JsonObject
            {
                ["name"] = name,
                ["source"] = "definition",
                ["definition_present"] = lines.Count > 0
            },
            ["valid"] = true,
            ["violations"] = new JsonArray(),
            ["stop"] = null
        };
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
