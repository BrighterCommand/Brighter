#nullable enable
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

internal sealed record Violation(string Rule, string Code, string? Step, string Detail, string Message)
{
    public JsonObject ToJson() => new()
    {
        ["rule"] = Rule,
        ["code"] = Code,
        ["step"] = Step,
        ["detail"] = Detail,
        ["message"] = Message
    };
}

internal sealed record SyntaxFault(int Line, string What);

internal static class SlipSyntax
{
    private const string Name = "[a-z][a-z0-9-]*";
    private const string Command = "/[a-z][a-z0-9-]*:[a-z][a-z0-9_-]*(?: [^ @]+)*";
    private const string HandOffName = "hand-off";
    private const int ColumnCount = 6;
    private const int TableHeadingRowCount = 2;

    private static readonly string[] HeaderCells = ["Step", "Kind", "Producer", "Artefacts", "Marker", "Marker-writer"];
    private static readonly Regex FlowLine = new($"^# flow: {Name}$");
    private static readonly Regex DelimiterCell = new("^:?-{3,}:?$");
    private static readonly Regex StepCell = new($"^{Name}$");
    private static readonly Regex KindCell = new($"^(?:{Name})?$");
    private static readonly Regex CommandCell = new($"^{Command}$");
    private static readonly Regex ArtefactsCell = new("^[A-Za-z0-9._-]+(?:, [A-Za-z0-9._-]+)*$");
    private static readonly Regex MarkerCell = new("^\\.[a-z0-9-]+-approved$");
    private static readonly Regex HandOffLine = new($"^{HandOffName}: {Command}$");
    private static readonly Regex DetourLine = new($"^detour: {Command} @ (?:{Name}|{HandOffName}) -> (?:{Name}|{HandOffName})$");

    private enum Position
    {
        BeforeHeader,
        BeforeDelimiter,
        InTable,
        AfterTable,
        AfterHandOff
    }

    public static bool IsFlowName(string name) => StepCell.IsMatch(name);

    public static int StepCount(IReadOnlyList<string> lines) =>
        lines.Count(line => line.StartsWith('|')) - TableHeadingRowCount;

    public static IReadOnlyList<string> StepNames(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => cells[0]).ToList();

    public static IReadOnlyList<(string Step, string Marker)> StepMarkers(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[4])).ToList();

    public static IReadOnlyList<(string Step, string Command)> StepCommands(IReadOnlyList<string> lines) =>
        StepRows(lines).SelectMany(cells => new[] { (cells[0], cells[2]), (cells[0], cells[5]) }).ToList();

    public static IReadOnlyList<(string Step, string Producer)> StepProducers(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[2])).ToList();

    private static IEnumerable<string[]> StepRows(IReadOnlyList<string> lines) =>
        lines
            .TakeWhile(line => !line.StartsWith(HandOffName + ":"))
            .Where(line => line.StartsWith('|'))
            .Skip(TableHeadingRowCount)
            .Select(line => Cells(line)!);

    public static SyntaxFault? FirstFault(IReadOnlyList<string> lines)
    {
        if (!FlowLine.IsMatch(lines[0]))
        {
            return new SyntaxFault(1, "line 1 must be '# flow: {name}'");
        }

        var position = Position.BeforeHeader;
        for (var index = 1; index < lines.Count; index++)
        {
            var (next, what) = Advance(position, lines[index]);
            if (what is not null)
            {
                return new SyntaxFault(index + 1, what);
            }

            position = next;
        }

        return position == Position.AfterHandOff ? null : new SyntaxFault(lines.Count, "the hand-off line is missing");
    }

    private static (Position Next, string? What) Advance(Position position, string line)
    {
        if (line.Length == 0)
        {
            return BlankLine(position);
        }

        return position switch
        {
            Position.BeforeHeader => CheckHeader(line),
            Position.BeforeDelimiter => CheckDelimiter(line),
            Position.InTable => line.StartsWith('|') ? CheckRow(line, Position.InTable) : CheckHandOff(line),
            Position.AfterTable => line.StartsWith('|') ? Fault("a table row follows a blank line") : CheckHandOff(line),
            _ => CheckAfterHandOff(line)
        };
    }

    private static (Position Next, string? What) BlankLine(Position position) => position switch
    {
        Position.BeforeDelimiter => Fault("the delimiter row is missing"),
        Position.InTable => (Position.AfterTable, null),
        _ => (position, null)
    };

    private static (Position Next, string? What) CheckHeader(string line) =>
        Cells(line) is { } cells && cells.SequenceEqual(HeaderCells)
            ? (Position.BeforeDelimiter, null)
            : Fault("the step table header is missing or wrong");

    private static (Position Next, string? What) CheckDelimiter(string line) =>
        Cells(line) is { Length: ColumnCount } cells && cells.All(DelimiterCell.IsMatch)
            ? (Position.InTable, null)
            : Fault("the delimiter row is not valid");

    private static (Position Next, string? What) CheckRow(string line, Position next) =>
        Cells(line) is { Length: ColumnCount } cells && RowCellsAreValid(cells)
            ? (next, null)
            : Fault("the step row is not valid");

    private static bool RowCellsAreValid(string[] cells) =>
        StepCell.IsMatch(cells[0]) && cells[0] != HandOffName
        && KindCell.IsMatch(cells[1])
        && CommandCell.IsMatch(cells[2])
        && ArtefactsCell.IsMatch(cells[3])
        && MarkerCell.IsMatch(cells[4])
        && CommandCell.IsMatch(cells[5]);

    private static (Position Next, string? What) CheckHandOff(string line) =>
        HandOffLine.IsMatch(line) ? (Position.AfterHandOff, null) : Fault("unexpected text");

    private static (Position Next, string? What) CheckAfterHandOff(string line)
    {
        if (line.StartsWith('|'))
        {
            return CheckRow(line, Position.AfterHandOff);
        }

        return DetourLine.IsMatch(line) ? (Position.AfterHandOff, null) : Fault("unexpected text");
    }

    private static string[]? Cells(string line) =>
        line.StartsWith('|') && line.EndsWith('|') && line.Length > 1
            ? line[1..^1].Split('|').Select(cell => cell.Trim()).ToArray()
            : null;

    private static (Position Next, string? What) Fault(string what) => (default, what);
}

internal static class CommandCatalogue
{
    private const string CommandsDirectory = ".claude/commands";
    private const string CommandExtension = ".md";

    private static readonly Regex SpaceRun = new(" +");

    public static string Collapse(string command) => SpaceRun.Replace(command, " ");

    public static bool Resolves(string command)
    {
        var firstToken = Collapse(command).Split(' ')[0];
        var parts = firstToken.TrimStart('/').Split(':');
        return parts.Length == 2 && FamilyHasCommand(parts[0], parts[1]);
    }

    private static bool FamilyHasCommand(string family, string name) =>
        ListDirectory(CommandsDirectory, Directory.GetDirectories).Contains(family, StringComparer.Ordinal)
        && ListDirectory($"{CommandsDirectory}/{family}", Directory.GetFiles).Contains(name + CommandExtension, StringComparer.Ordinal);

    private static IEnumerable<string> ListDirectory(string directory, Func<string, string[]> list) =>
        list(directory).Select(entry => Path.GetFileName(entry));
}

internal static class SlipValidator
{
    public static IReadOnlyList<Violation> Validate(string name, IReadOnlyList<string> lines) =>
        SlipSyntax.StepCount(lines) == 0
            ? [NoSteps(name)]
            : [.. DuplicateSteps(SlipSyntax.StepNames(lines)), .. DuplicateMarkers(SlipSyntax.StepMarkers(lines)), .. UnknownCommands(SlipSyntax.StepCommands(lines)), .. SharedProducers(SlipSyntax.StepProducers(lines))];

    private static IReadOnlyList<Violation> SharedProducers(IReadOnlyList<(string Step, string Producer)> stepProducers) =>
        stepProducers
            .GroupBy(stepProducer => CommandCatalogue.Collapse(stepProducer.Producer))
            .Where(group => group.Count() > 1)
            .Select(group => SharedProducer(group.Key, group.Select(stepProducer => stepProducer.Step)))
            .ToList();

    private static Violation SharedProducer(string command, IEnumerable<string> steps) =>
        new("R5", "shared-producer", null, "shared producer",
            $"{command} is the producer of more than one step ({string.Join(", ", steps)}).");

    private static IReadOnlyList<Violation> UnknownCommands(IReadOnlyList<(string Step, string Command)> stepCommands) =>
        stepCommands
            .Select(stepCommand => (stepCommand.Step, Command: CommandCatalogue.Collapse(stepCommand.Command)))
            .Where(stepCommand => !CommandCatalogue.Resolves(stepCommand.Command))
            .Select(stepCommand => UnknownCommand(stepCommand.Step, stepCommand.Command))
            .ToList();

    private static Violation UnknownCommand(string step, string command) =>
        new("R4", "unknown-command", step, "unknown command",
            $"Step {step} names a command that does not exist, {command}.");

    private static IReadOnlyList<Violation> DuplicateSteps(IReadOnlyList<string> stepNames) =>
        stepNames
            .GroupBy(step => step)
            .Where(group => group.Count() > 1)
            .Select(group => DuplicateStep(group.Key))
            .ToList();

    private static IReadOnlyList<Violation> DuplicateMarkers(IReadOnlyList<(string Step, string Marker)> stepMarkers) =>
        stepMarkers
            .GroupBy(stepMarker => stepMarker.Marker)
            .Where(group => group.Count() > 1)
            .Select(group => DuplicateMarker(group.Key, group.Select(stepMarker => stepMarker.Step)))
            .ToList();

    private static Violation DuplicateMarker(string marker, IEnumerable<string> steps) =>
        new("R3", "duplicate-marker", null, "duplicate marker",
            $"Marker {marker} is named by more than one step ({string.Join(", ", steps)}).");

    private static Violation DuplicateStep(string step) =>
        new("R2", "duplicate-step", step, "duplicate step", $"Step {step} appears more than once.");

    private static Violation NoSteps(string name) =>
        new("R1", "no-steps", null, "no steps", $"Flow {name} has no steps.");
}

internal static class DefinitionReport
{
    private const string FlowExtension = ".flow";
    private const string MalformedRule = "R0";
    private const string MalformedCode = "malformed";

    public static JsonObject For(Invocation invocation)
    {
        var name = invocation.Target!;
        if (!SlipSyntax.IsFlowName(name))
        {
            return Build(name, false, [Malformed("bad name", $"{name} is not a valid flow name.")]);
        }

        var path = $"{invocation.FlowsDirectory!.TrimEnd('/')}/{name}{FlowExtension}";
        if (!File.Exists(path))
        {
            return Build(name, false, [Malformed("definition missing", $"Flow {name} has no definition at {path}.")]);
        }

        var lines = SlipReader.ReadLines(path);
        var fault = SlipSyntax.FirstFault(lines);
        return Build(name, true, fault is null ? SlipValidator.Validate(name, lines) : [FaultViolation(path, fault)]);
    }

    private static Violation FaultViolation(string path, SyntaxFault fault) =>
        Malformed(fault.What, $"{path} line {fault.Line} is not valid slip syntax: {fault.What}.");

    private static Violation Malformed(string detail, string message) =>
        new(MalformedRule, MalformedCode, null, detail, message);

    private static JsonObject Build(string name, bool present, IReadOnlyList<Violation> violations) => new()
    {
        ["schema_version"] = ReportSchema.Version,
        ["mode"] = "definition",
        ["flow"] = new JsonObject
        {
            ["name"] = name,
            ["source"] = "definition",
            ["definition_present"] = present
        },
        ["valid"] = violations.Count == 0,
        ["violations"] = new JsonArray(violations.Select(violation => (JsonNode)violation.ToJson()).ToArray()),
        ["stop"] = null
    };
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
