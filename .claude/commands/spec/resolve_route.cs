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

if (invocation.Mode == RouteMode.Spec)
{
    var report = SpecReport.For(invocation);
    if (report is null)
    {
        return UsageErrorExitCode;
    }

    ReportWriter.Write(report);
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

internal sealed record SlipStep(string Name, string Kind, string Producer, IReadOnlyList<string> Artefacts, string Marker, string MarkerWriter);

internal static class SlipSyntax
{
    private const string Name = "[a-z][a-z0-9-]*";
    private const string Command = "/[a-z][a-z0-9-]*:[a-z][a-z0-9_-]*(?: [^ @]+)*";
    private const string HandOffName = "hand-off";
    private const string FlowLinePrefix = "# flow: ";
    private const string DetourPrefix = "detour: ";
    private const string AttachSeparator = " @ ";
    private const string ReturnSeparator = " -> ";
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

    public static IReadOnlyList<SlipStep> Steps(IReadOnlyList<string> lines) =>
        StepRows(lines)
            .Select(cells => new SlipStep(cells[0], cells[1], CommandCatalogue.Collapse(cells[2]), cells[3].Split(", "), cells[4], CommandCatalogue.Collapse(cells[5])))
            .ToList();

    public static string DeclaredName(IReadOnlyList<string> lines) => lines[0][FlowLinePrefix.Length..];

    public static int StepCount(IReadOnlyList<string> lines) => StepRows(lines).Count();

    public static IReadOnlyList<int> LinesAfterHandOff(IReadOnlyList<string> lines) =>
        lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .SkipWhile(row => !row.Line.StartsWith(HandOffName + ":"))
            .Where(row => row.Line.StartsWith('|'))
            .Select(row => row.Number)
            .ToList();

    public static string? HandOffCommand(IReadOnlyList<string> lines) =>
        lines.FirstOrDefault(line => HandOffLine.IsMatch(line))?[(HandOffName.Length + 2)..];

    public static IReadOnlyList<(int Line, string Command, string AttachedTo, string ReturnPoint)> Detours(IReadOnlyList<string> lines) =>
        lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(row => DetourLine.IsMatch(row.Line))
            .Select(row => DetourParts(row.Number, row.Line[DetourPrefix.Length..]))
            .ToList();

    private static (int Line, string Command, string AttachedTo, string ReturnPoint) DetourParts(int number, string text)
    {
        var attachSplit = text.LastIndexOf(AttachSeparator, StringComparison.Ordinal);
        var returnSplit = text.LastIndexOf(ReturnSeparator, StringComparison.Ordinal);
        return (number,
            text[..attachSplit],
            text[(attachSplit + AttachSeparator.Length)..returnSplit],
            text[(returnSplit + ReturnSeparator.Length)..]);
    }

    public static IReadOnlyList<string> StepNames(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => cells[0]).ToList();

    public static IReadOnlyList<(string Step, string Marker)> StepMarkers(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[4])).ToList();

    public static IReadOnlyList<(string Step, string Command)> StepCommands(IReadOnlyList<string> lines) =>
        StepRows(lines).SelectMany(cells => new[] { (cells[0], cells[2]), (cells[0], cells[5]) }).ToList();

    public static IReadOnlyList<(string Step, string Producer)> StepProducers(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[2])).ToList();

    public static IReadOnlyList<(string Step, string Kind, string Artefacts)> StepKindArtefacts(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[1], cells[3])).ToList();

    public static IReadOnlyList<(string Step, string Producer, string MarkerWriter)> StepGateCommands(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[2], cells[5])).ToList();

    public static IReadOnlyList<(string Step, string Kind)> StepKinds(IReadOnlyList<string> lines) =>
        StepRows(lines).Select(cells => (cells[0], cells[1])).ToList();

    public static (string Step, string Kind, string Artefacts, string Marker)? LastStep(IReadOnlyList<string> lines) =>
        StepRows(lines).LastOrDefault() is { } cells ? (cells[0], cells[1], cells[3], cells[4]) : null;

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

internal sealed record ApprovalEntry(string Kind, IReadOnlyList<string> Commands);

internal sealed record ApprovalTable(string Path, IReadOnlyList<ApprovalEntry> Entries, string? Fault);

internal static class ApprovalTableReader
{
    private const string Title = "# approvals";
    private const string EmptySubFlow = "(empty)";
    private const string Name = "[a-z][a-z0-9-]*";
    private const string BareCommand = "/[a-z][a-z0-9-]*:[a-z][a-z0-9_-]*";
    private const string CommandSeparator = ", ";
    private const int ColumnCount = 2;

    private static readonly string[] HeaderCells = ["Kind", "Approval sub-flow"];
    private static readonly Regex DelimiterCell = new("^:?-{3,}:?$");
    private static readonly Regex KindCell = new($"^{Name}$");
    private static readonly Regex SubFlowCell = new($"^{BareCommand}(?:, {BareCommand})*$");

    private sealed record NumberedLine(int Number, string Text);

    public static ApprovalTable Read(string path) =>
        File.Exists(path) ? Parse(path, SlipReader.ReadLines(path)) : Faulted(path, "the file is missing");

    private static ApprovalTable Parse(string path, IReadOnlyList<string> lines)
    {
        if (lines[0] != Title)
        {
            return Faulted(path, $"line 1 must be '{Title}'");
        }

        var body = lines
            .Select((text, index) => new NumberedLine(index + 1, text))
            .Skip(1)
            .SkipWhile(line => line.Text.Length == 0)
            .ToList();
        return ParseBody(path, body);
    }

    private static ApprovalTable ParseBody(string path, IReadOnlyList<NumberedLine> body)
    {
        if (body.Count == 0 || !IsHeader(body[0].Text))
        {
            return Faulted(path, $"line {LineNumber(body)}: the header row is missing or wrong");
        }

        if (body.Count < 2 || !IsDelimiter(body[1].Text))
        {
            return Faulted(path, $"line {LineNumber(body.Skip(1).ToList())}: the delimiter row is missing or not valid");
        }

        var rows = body.Skip(2).TakeWhile(line => line.Text.Length > 0).ToList();
        var stray = body.Skip(2 + rows.Count).FirstOrDefault(line => line.Text.Length > 0);
        return stray is null ? ParseRows(path, rows) : Faulted(path, $"line {stray.Number}: unexpected text after the table");
    }

    private static int LineNumber(IReadOnlyList<NumberedLine> body) => body.Count == 0 ? 0 : body[0].Number;

    private static ApprovalTable ParseRows(string path, IReadOnlyList<NumberedLine> rows)
    {
        var entries = new List<ApprovalEntry>();
        foreach (var row in rows)
        {
            var entry = ParseRow(row.Text);
            if (entry is null)
            {
                return Faulted(path, $"line {row.Number}: the approval row is not valid");
            }

            entries.Add(entry);
        }

        return new ApprovalTable(path, entries, null);
    }

    private static ApprovalEntry? ParseRow(string line) =>
        Cells(line) is { Length: ColumnCount } cells && KindCell.IsMatch(cells[0]) && IsSubFlow(cells[1])
            ? new ApprovalEntry(cells[0], Commands(cells[1]))
            : null;

    private static bool IsSubFlow(string cell) => cell == EmptySubFlow || SubFlowCell.IsMatch(cell);

    private static IReadOnlyList<string> Commands(string cell) =>
        cell == EmptySubFlow ? [] : cell.Split(CommandSeparator);

    private static bool IsHeader(string line) => Cells(line) is { } cells && cells.SequenceEqual(HeaderCells);

    private static bool IsDelimiter(string line) =>
        Cells(line) is { Length: ColumnCount } cells && cells.All(DelimiterCell.IsMatch);

    private static string[]? Cells(string line) =>
        line.StartsWith('|') && line.EndsWith('|') && line.Length > 1
            ? line[1..^1].Split('|').Select(cell => cell.Trim()).ToArray()
            : null;

    private static ApprovalTable Faulted(string path, string what) => new(path, [], what);
}

internal static class SlipValidator
{
    public static IReadOnlyList<Violation> Validate(string name, string path, IReadOnlyList<string> lines, ApprovalTable table) =>
        [.. SlipRules(name, lines, table), .. NameMismatch(name, path, SlipSyntax.DeclaredName(lines)), .. TableRules(table)];

    public static IReadOnlyList<Violation> ValidateTable(ApprovalTable table) => TableRules(table);

    private static IReadOnlyList<Violation> SlipRules(string name, IReadOnlyList<string> lines, ApprovalTable table) =>
        SlipSyntax.StepCount(lines) == 0
            ? [NoSteps(name)]
            : [.. DuplicateSteps(SlipSyntax.StepNames(lines)), .. DuplicateMarkers(SlipSyntax.StepMarkers(lines)), .. UnknownCommands(SlipSyntax.StepCommands(lines)), .. SharedProducers(SlipSyntax.StepProducers(lines)), .. MissingKinds(SlipSyntax.StepKinds(lines)), .. KnownKindArtefacts(SlipSyntax.StepKindArtefacts(lines)), .. DuplicateKnownKinds(SlipSyntax.StepKinds(lines)), .. GateNotAdjacent(SlipSyntax.StepGateCommands(lines)), .. SelfApproval(SlipSyntax.StepGateCommands(lines)), .. StepsAfterHandOff(SlipSyntax.LinesAfterHandOff(lines)), .. KindsWithoutEntry(SlipSyntax.StepKinds(lines), table), .. LastStepNotTasks(SlipSyntax.LastStep(lines)), .. HandOffNotImplement(SlipSyntax.HandOffCommand(lines)), .. BadDetours(SlipSyntax.Detours(lines), SlipSyntax.StepNames(lines))];

    private static IReadOnlyList<Violation> NameMismatch(string stem, string path, string declared) =>
        declared == stem ? [] : [NameMismatchViolation(path, declared)];

    private static Violation NameMismatchViolation(string path, string declared) =>
        new("R16", "name-mismatch", null, "name mismatch", $"{path} declares flow {declared}, which differs from its filename.");

    private static IReadOnlyList<Violation> TableRules(ApprovalTable table) =>
        [.. DuplicateEntries(table), .. UnresolvedCommands(table)];

    private static IReadOnlyList<Violation> BadDetours(IReadOnlyList<(int Line, string Command, string AttachedTo, string ReturnPoint)> detours, IReadOnlyList<string> stepNames) =>
        detours.SelectMany(detour => UnknownDetourParts(detour, stepNames)).ToList();

    private static IEnumerable<Violation> UnknownDetourParts((int Line, string Command, string AttachedTo, string ReturnPoint) detour, IReadOnlyList<string> stepNames)
    {
        var command = CommandCatalogue.Collapse(detour.Command);
        if (!CommandCatalogue.Resolves(command))
        {
            yield return BadDetour(command, detour.Line, "command");
        }

        if (!IsStepOrHandOff(detour.AttachedTo, stepNames))
        {
            yield return BadDetour(command, detour.Line, "attach point");
        }

        if (!IsStepOrHandOff(detour.ReturnPoint, stepNames))
        {
            yield return BadDetour(command, detour.Line, "return point");
        }
    }

    private static bool IsStepOrHandOff(string name, IReadOnlyList<string> stepNames) =>
        name == "hand-off" || stepNames.Contains(name);

    private static Violation BadDetour(string command, int line, string part) =>
        new("R15", "bad-detour", null, "bad detour", $"Detour {command} at line {line}: {part} is unknown.");

    private static IReadOnlyList<Violation> HandOffNotImplement(string? command) =>
        command is not null && command != "/spec:implement" ? [HandOffNotImplementViolation(command)] : [];

    private static Violation HandOffNotImplementViolation(string command) =>
        new("R14", "hand-off-not-implement", null, "hand-off not implement",
            $"The hand-off is {command}, not /spec:implement.");

    private static IReadOnlyList<Violation> LastStepNotTasks((string Step, string Kind, string Artefacts, string Marker)? last) =>
        last is { } step && !IsTasksStep(step) ? [LastStepNotTasksViolation(step.Step)] : [];

    private static bool IsTasksStep((string Step, string Kind, string Artefacts, string Marker) step) =>
        step.Kind == "tasks" && step.Artefacts == "tasks.md" && step.Marker == ".tasks-approved";

    private static Violation LastStepNotTasksViolation(string step) =>
        new("R13", "last-step-not-tasks", step, "last step not tasks",
            $"The last step, {step}, is not the tasks step (kind tasks, tasks.md, .tasks-approved).");

    private static IReadOnlyList<Violation> KindsWithoutEntry(IReadOnlyList<(string Step, string Kind)> stepKinds, ApprovalTable table) =>
        table.Fault is not null
            ? []
            : stepKinds
                .Where(stepKind => stepKind.Kind.Length > 0 && table.Entries.All(entry => entry.Kind != stepKind.Kind))
                .Select(stepKind => KindWithoutEntry(stepKind.Step, stepKind.Kind, table.Path))
                .ToList();

    private static Violation KindWithoutEntry(string step, string kind, string table) =>
        new("R12", "approval-entry", step, "kind without entry",
            $"Step {step} is of kind {kind}, which has no entry in {table}.");

    private static IReadOnlyList<Violation> DuplicateEntries(ApprovalTable table) =>
        table.Entries
            .GroupBy(entry => entry.Kind)
            .Where(group => group.Count() > 1)
            .Select(group => DuplicateEntry(group.Key, table.Path))
            .ToList();

    private static Violation DuplicateEntry(string kind, string table) =>
        new("R12", "approval-entry", null, "duplicate entry", $"{table} has more than one entry for kind {kind}.");

    private static IReadOnlyList<Violation> UnresolvedCommands(ApprovalTable table) =>
        table.Fault is not null
            ? [UnresolvedCommand(table.Path, table.Fault)]
            : table.Entries
                .SelectMany(entry => entry.Commands)
                .Where(command => !CommandCatalogue.Resolves(command))
                .Select(command => UnresolvedCommand(table.Path, command))
                .ToList();

    private static Violation UnresolvedCommand(string table, string what) =>
        new("R12", "approval-entry", null, "unreadable table or unknown command",
            $"{table} is unreadable or names a command that does not exist: {what}.");

    private static IReadOnlyList<Violation> StepsAfterHandOff(IReadOnlyList<int> lineNumbers) =>
        lineNumbers.Select(StepAfterHandOff).ToList();

    private static Violation StepAfterHandOff(int lineNumber) =>
        new("R11", "step-after-hand-off", null, "step after hand-off", $"A step follows the hand-off at line {lineNumber}.");

    private static IReadOnlyList<Violation> SelfApproval(IReadOnlyList<(string Step, string Producer, string MarkerWriter)> steps) =>
        steps
            .Select((step, index) => (Step: step, Index: index))
            .Where(row => IsSelfApproved(row.Step) && !ApprovesStepBefore(steps, row.Index))
            .Select(row => SelfApprovalViolation(row.Step.Step, CommandCatalogue.Collapse(row.Step.Producer)))
            .ToList();

    private static bool IsSelfApproved((string Step, string Producer, string MarkerWriter) step) =>
        CommandCatalogue.Collapse(step.Producer) == CommandCatalogue.Collapse(step.MarkerWriter);

    private static bool ApprovesStepBefore(IReadOnlyList<(string Step, string Producer, string MarkerWriter)> steps, int index) =>
        index > 0 && CommandCatalogue.Collapse(steps[index - 1].MarkerWriter) == CommandCatalogue.Collapse(steps[index].Producer);

    private static Violation SelfApprovalViolation(string step, string command) =>
        new("R10", "self-approval", step, "self approval",
            $"Step {step} is produced and approved by {command} with no gate before it.");

    private static IReadOnlyList<Violation> GateNotAdjacent(IReadOnlyList<(string Step, string Producer, string MarkerWriter)> steps) =>
        steps
            .SelectMany((approved, index) => steps
                .Select((produced, producedIndex) => (Approved: approved, Produced: produced, ApprovedIndex: index, ProducedIndex: producedIndex)))
            .Where(pair => pair.ProducedIndex != pair.ApprovedIndex && pair.ProducedIndex != pair.ApprovedIndex + 1)
            .Where(pair => CommandCatalogue.Collapse(pair.Approved.MarkerWriter) == CommandCatalogue.Collapse(pair.Produced.Producer))
            .Select(pair => GateNotAdjacentViolation(CommandCatalogue.Collapse(pair.Approved.MarkerWriter), pair.Approved.Step, pair.Produced.Step))
            .ToList();

    private static Violation GateNotAdjacentViolation(string command, string approvedStep, string producedStep) =>
        new("R9", "gate-not-adjacent", null, "gate not adjacent",
            $"{command} approves step {approvedStep} but produces step {producedStep}, which does not immediately follow it.");

    private static IReadOnlyList<Violation> DuplicateKnownKinds(IReadOnlyList<(string Step, string Kind)> stepKinds) =>
        stepKinds
            .Where(stepKind => KnownKindArtefactNames.ContainsKey(stepKind.Kind))
            .GroupBy(stepKind => stepKind.Kind)
            .Where(group => group.Count() > 1)
            .Select(group => DuplicateKnownKind(group.Key, group.Select(stepKind => stepKind.Step)))
            .ToList();

    private static Violation DuplicateKnownKind(string kind, IEnumerable<string> steps) =>
        new("R8", "duplicate-known-kind", null, "duplicate known kind",
            $"More than one step is of kind {kind} ({string.Join(", ", steps)}).");

    private static readonly Dictionary<string, string> KnownKindArtefactNames = new()
    {
        ["requirements"] = "requirements.md",
        ["design"] = ".adr-list",
        ["tasks"] = "tasks.md",
    };

    private static IReadOnlyList<Violation> KnownKindArtefacts(IReadOnlyList<(string Step, string Kind, string Artefacts)> stepKindArtefacts) =>
        stepKindArtefacts
            .Where(row => KnownKindArtefactNames.TryGetValue(row.Kind, out var artefact) && !NamesArtefact(row.Artefacts, artefact))
            .Select(row => KnownKindArtefact(row.Step, row.Kind, KnownKindArtefactNames[row.Kind]))
            .ToList();

    private static bool NamesArtefact(string artefacts, string artefact) =>
        artefacts.Split(", ").Contains(artefact, StringComparer.Ordinal);

    private static Violation KnownKindArtefact(string step, string kind, string artefact) =>
        new("R7", "known-kind-artefact", step, "known-kind artefact", $"Step {step} is of kind {kind} but does not name {artefact}.");

    private static IReadOnlyList<Violation> MissingKinds(IReadOnlyList<(string Step, string Kind)> stepKinds) =>
        stepKinds
            .Where(stepKind => stepKind.Kind.Length == 0)
            .Select(stepKind => MissingKind(stepKind.Step))
            .ToList();

    private static Violation MissingKind(string step) =>
        new("R6", "missing-kind", step, "missing kind", $"Step {step} declares no artefact kind.");

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
    private const string ApprovalTableFile = "approvals.md";
    private const string MalformedRule = "R0";
    private const string MalformedCode = "malformed";

    public static JsonObject For(Invocation invocation)
    {
        var name = invocation.Target!;
        if (!SlipSyntax.IsFlowName(name))
        {
            return Build(name, false, [Malformed("bad name", $"{name} is not a valid flow name.")]);
        }

        var directory = invocation.FlowsDirectory!.TrimEnd('/');
        var path = $"{directory}/{name}{FlowExtension}";
        if (!File.Exists(path))
        {
            return Build(name, false, [Malformed("definition missing", $"Flow {name} has no definition at {path}.")]);
        }

        var table = ApprovalTableReader.Read($"{directory}/{ApprovalTableFile}");
        var lines = SlipReader.ReadLines(path);
        return Build(name, true, Violations(name, path, lines, table));
    }

    public static IReadOnlyList<Violation> Violations(string name, string path, IReadOnlyList<string> lines, ApprovalTable table) =>
        SlipSyntax.FirstFault(lines) is { } fault
            ? [FaultViolation(path, fault), .. SlipValidator.ValidateTable(table)]
            : SlipValidator.Validate(name, path, lines, table);

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

internal sealed class SpecStateReader(string specDirectory)
{
    private const string AdrList = ".adr-list";

    public bool ArtefactsPresent(SlipStep step) => step.Artefacts.All(IsPresent);

    public bool Approved(SlipStep step) => File.Exists(Path.Combine(specDirectory, step.Marker));

    private bool IsPresent(string artefact)
    {
        var path = Path.Combine(specDirectory, artefact);
        return artefact == AdrList
            ? File.Exists(path) && File.ReadLines(path).Any(line => line.Trim().Length > 0)
            : File.Exists(path);
    }
}

internal static class RouteRecordInspector
{
    public static JsonObject Tampered() => new()
    {
        ["value"] = false,
        ["case"] = null,
        ["recorded_flow"] = null
    };

    public static JsonNode? RouteChange() => null;
}

internal static class SpecReport
{
    private const string DefaultFlowsDirectory = ".claude/flows";
    private const string LegacyFlow = "feature";
    private const string ApprovalTableFile = "approvals.md";
    private static readonly string[] KnownKinds = ["requirements", "design", "tasks"];

    public static JsonObject? For(Invocation invocation)
    {
        var directoryName = FindSpecDirectory(invocation.SpecsRoot, invocation.Target!);
        if (directoryName is null)
        {
            return null;
        }

        var flowsDirectory = (invocation.FlowsDirectory ?? DefaultFlowsDirectory).TrimEnd('/');
        var flowPath = $"{flowsDirectory}/{LegacyFlow}.flow";
        var lines = SlipReader.ReadLines(flowPath);
        var table = ApprovalTableReader.Read($"{flowsDirectory}/{ApprovalTableFile}");
        var specPath = $"{invocation.SpecsRoot}/{directoryName}";
        var violations = DefinitionReport.Violations(LegacyFlow, flowPath, lines, table);
        return RouteResolver.Resolve(specPath, LegacyFlow, "default", lines, violations, table, new SpecStateReader(specPath));
    }

    public static IReadOnlyList<string> Kinds => KnownKinds;

    private static string? FindSpecDirectory(string specsRoot, string target) =>
        Directory.Exists(specsRoot)
            ? Directory.GetDirectories(specsRoot)
                .Select(directory => Path.GetFileName(directory))
                .Where(name => !name.StartsWith('.'))
                .FirstOrDefault(name => string.Equals(name, target, StringComparison.Ordinal))
            : null;
}

internal static class RouteResolver
{
    public static JsonObject Resolve(string specPath, string flowName, string source, IReadOnlyList<string> lines, IReadOnlyList<Violation> violations, ApprovalTable table, SpecStateReader state)
    {
        var steps = SlipSyntax.Steps(lines);
        var approved = steps.Select(state.Approved).ToList();
        var unapproved = steps.Where((_, index) => !approved[index]).ToList();
        return new JsonObject
        {
            ["schema_version"] = ReportSchema.Version,
            ["mode"] = "spec",
            ["spec"] = specPath,
            ["flow"] = new JsonObject { ["name"] = flowName, ["source"] = source, ["definition_present"] = true },
            ["valid"] = violations.Count == 0,
            ["violations"] = new JsonArray(violations.Select(violation => (JsonNode)violation.ToJson()).ToArray()),
            ["tampered"] = RouteRecordInspector.Tampered(),
            ["route_change"] = RouteRecordInspector.RouteChange(),
            ["steps"] = new JsonArray(steps.Select((step, index) => (JsonNode)StepJson(steps, index, approved, table, state)).ToArray()),
            ["known_kinds"] = KnownKindsJson(steps),
            ["gates"] = new JsonArray(),
            ["stray_markers"] = new JsonArray(),
            ["out_of_order"] = new JsonArray(),
            ["next"] = NextJson(unapproved[0]),
            ["hand_off"] = HandOffJson(SlipSyntax.HandOffCommand(lines), unapproved),
            ["approve_steps"] = new JsonArray(steps.Where(step => step.MarkerWriter == ApproveCommand(step.Name)).Select(step => (JsonNode)step.Name).ToArray()),
            ["stop"] = null
        };
    }

    private static string ApproveCommand(string step) => $"/spec:approve {step}";

    private static JsonObject StepJson(IReadOnlyList<SlipStep> steps, int index, IReadOnlyList<bool> approved, ApprovalTable table, SpecStateReader state)
    {
        var step = steps[index];
        return new JsonObject
        {
            ["name"] = step.Name,
            ["kind"] = step.Kind,
            ["known_kind"] = SpecReport.Kinds.Contains(step.Kind),
            ["producer"] = step.Producer,
            ["artefacts"] = new JsonArray(step.Artefacts.Select(artefact => (JsonNode)artefact).ToArray()),
            ["artefacts_present"] = state.ArtefactsPresent(step),
            ["marker"] = step.Marker,
            ["marker_writer"] = step.MarkerWriter,
            ["approved"] = approved[index],
            ["approval_subflow"] = ApprovalSubFlow(table, step.Kind),
            ["gate"] = null,
            ["precondition"] = index == 0 || approved[index - 1] ? Precondition(true, [], null) : StepPrecondition(step, steps[index - 1])
        };
    }

    private static JsonNode? ApprovalSubFlow(ApprovalTable table, string kind) =>
        table.Fault is null && table.Entries.FirstOrDefault(entry => entry.Kind == kind) is { } entry
            ? new JsonArray(entry.Commands.Select(command => (JsonNode)command).ToArray())
            : null;

    private static JsonObject StepPrecondition(SlipStep step, SlipStep previous) =>
        Precondition(false, [previous],
            $"`{step.Producer}` needs step `{previous.Name}` approved — run `{previous.MarkerWriter}`.");

    private static JsonObject Precondition(bool holds, IEnumerable<SlipStep> unapproved, string? message) => new()
    {
        ["holds"] = holds,
        ["unapproved"] = new JsonArray(unapproved.Select(step => (JsonNode)new JsonObject { ["step"] = step.Name, ["marker_writer"] = step.MarkerWriter }).ToArray()),
        ["message"] = message
    };

    private static JsonObject KnownKindsJson(IReadOnlyList<SlipStep> steps)
    {
        var kinds = new JsonObject();
        foreach (var kind in SpecReport.Kinds)
        {
            kinds[kind] = steps.FirstOrDefault(step => step.Kind == kind) is { } step
                ? new JsonObject { ["step"] = step.Name, ["producer"] = step.Producer, ["marker_writer"] = step.MarkerWriter }
                : null;
        }

        return kinds;
    }

    private static JsonObject NextJson(SlipStep step) => new()
    {
        ["step"] = step.Name,
        ["kind"] = step.Kind,
        ["commands"] = new JsonArray((JsonNode)step.Producer),
        ["unattended_driver"] = null,
        ["detours"] = new JsonArray()
    };

    private static JsonObject HandOffJson(string? command, IReadOnlyList<SlipStep> unapproved) => new()
    {
        ["command"] = command,
        ["precondition"] = Precondition(unapproved.Count == 0, unapproved, HandOffMessage(unapproved))
    };

    private static string? HandOffMessage(IReadOnlyList<SlipStep> unapproved) =>
        unapproved.Count == 0
            ? null
            : $"Implementation needs every step approved — unapproved: {string.Join(", ", unapproved.Select(step => $"`{step.Name}` (run `{step.MarkerWriter}`)"))}.";
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
