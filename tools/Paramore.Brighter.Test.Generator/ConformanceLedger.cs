#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Paramore.Brighter.Test.Generator;

/// <summary>
/// Provides per-(configuration × behaviour) conformance Skip values for canonical test
/// templates, driven by the checked-in conformance ledger (FR-21 / ADR 0067).
/// </summary>
public interface IAmAConformanceLedger
{
    /// <summary>
    /// Returns the Deferred Skip string for the given gateway configuration and canonical
    /// behaviour column, or an empty string when the cell is Pass or Fixed (test runs).
    /// </summary>
    /// <param name="ledgerKey">
    /// The conformance-ledger row identifier, e.g. "Kafka / Classic".
    /// </param>
    /// <param name="frColumn">
    /// The canonical-behaviour column key, e.g. "FR-22".
    /// </param>
    /// <param name="behaviourName">
    /// Human-readable behaviour label used in the Skip string, e.g. "canonical plain requeue".
    /// </param>
    string GetSkip(string ledgerKey, string frColumn, string behaviourName);
}

/// <summary>
/// Loads the conformance ledger from the checked-in markdown file and provides
/// per-(configuration × behaviour) Skip values for the generator.
/// </summary>
public sealed class ConformanceLedger : IAmAConformanceLedger
{
    private const string RELATIVE_LEDGER_PATH =
        "specs/0036-universal-transport-conformance-tests/conformance-status.md";

    private readonly Dictionary<string, Dictionary<string, string>> _cells;

    public ConformanceLedger(string ledgerPath)
        => _cells = ParseLedger(ledgerPath);

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> until the conformance ledger is found.
    /// Returns the full path or null when the ledger cannot be located.
    /// </summary>
    public static string? FindLedgerPath(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, RELATIVE_LEDGER_PATH);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Loads the conformance ledger by walking up from <paramref name="startDirectory"/>.
    /// </summary>
    /// <param name="startDirectory">The directory to begin the upward search from.</param>
    /// <returns>The loaded ledger.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no ledger is found above <paramref name="startDirectory"/>.
    /// </exception>
    /// <remarks>
    /// Failing is the point. A caller handed a null ledger generates the canonical suite with no
    /// Skip values assigned at all, and a model whose Skip stays null renders
    /// <c>[Fact(Skip = "")]</c> - Liquid treats <c>nil != empty</c> as TRUE - so every canonical
    /// test on every transport is reported skipped and the suite goes green having run nothing.
    /// A gate that cannot find its own criteria has to stop, not wave the build through.
    /// </remarks>
    public static ConformanceLedger LoadFrom(string startDirectory)
    {
        var path = FindLedgerPath(startDirectory)
                   ?? throw new InvalidOperationException(
                       $"Could not locate the conformance ledger '{RELATIVE_LEDGER_PATH}' by walking up "
                       + $"from '{startDirectory}'. The generator will not emit the canonical suite "
                       + "without it: with no ledger every canonical test renders as skipped and the "
                       + "conformance run proves nothing. Run the generator from within the repository, "
                       + "or update the ledger path if the spec directory has moved.");

        return new ConformanceLedger(path);
    }

    public string GetSkip(string ledgerKey, string frColumn, string behaviourName)
    {
        if (!_cells.TryGetValue(ledgerKey, out var row)) return string.Empty;
        if (!row.TryGetValue(frColumn, out var cellValue)) return string.Empty;
        return ComputeSkip(cellValue, ledgerKey, behaviourName);
    }

    /// <summary>
    /// Reports whether the ledger carries a row for <paramref name="ledgerKey"/>.
    /// </summary>
    /// <param name="ledgerKey">The configuration row key, for example <c>AWS / SqsFifo</c>.</param>
    /// <returns><c>true</c> when the row exists.</returns>
    /// <remarks>
    /// <see cref="GetSkip"/> answers "no Skip" for a row it cannot find, which is indistinguishable
    /// from a row that says Pass. An audit that needs to tell those apart asks here first.
    /// </remarks>
    public bool HasRow(string ledgerKey) => _cells.ContainsKey(ledgerKey);

    /// <summary>
    /// Reads the raw cell at (<paramref name="ledgerKey"/>, <paramref name="frColumn"/>) without
    /// interpreting it.
    /// </summary>
    /// <param name="ledgerKey">The configuration row key.</param>
    /// <param name="frColumn">The behaviour column, for example <c>FR-16</c>.</param>
    /// <param name="cellValue">The cell's text when both row and column exist.</param>
    /// <returns><c>true</c> when the cell exists.</returns>
    public bool TryGetCell(string ledgerKey, string frColumn, out string cellValue)
    {
        cellValue = string.Empty;
        if (!_cells.TryGetValue(ledgerKey, out var row)) return false;
        if (!row.TryGetValue(frColumn, out var value)) return false;

        cellValue = value;
        return true;
    }

    /// <summary>
    /// Reports whether <paramref name="cellValue"/> is one of the vocabulary tokens
    /// <see cref="ComputeSkip"/> understands: Pass, Fixed, Unknown, or <c>Deferred -&gt;</c>.
    /// </summary>
    /// <param name="cellValue">The raw cell text.</param>
    /// <returns><c>true</c> when the value carries a meaning rather than being read as "no Skip" by default.</returns>
    /// <remarks>
    /// Anything else - an empty cell, a bare "Deferred" with no issue arrow, or a hand-typed note -
    /// falls through <see cref="ComputeSkip"/> to the empty string and so silently runs the
    /// behaviour. That is the one outcome a ledger cell should never be able to mean by accident.
    /// </remarks>
    public static bool IsRecognisedCellValue(string cellValue)
    {
        var value = cellValue.Trim();
        return value.StartsWith("Pass", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("Fixed", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("Deferred ->", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a raw ledger cell value to the Deferred Skip string.
    /// Pass/Fixed → empty; Unknown → placeholder #NNNN; Deferred → real issue number.
    /// </summary>
    internal static string ComputeSkip(string cellValue, string transport, string behaviour)
    {
        if (string.IsNullOrEmpty(cellValue)) return string.Empty;

        // Pass or Fixed (#PR/commit) → test runs without Skip.
        if (cellValue.StartsWith("Pass", StringComparison.OrdinalIgnoreCase)
            || cellValue.StartsWith("Fixed", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        // Unknown (transient fix-phase state) → use the pre-audit placeholder.
        if (cellValue.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
            return $"Deferred: #NNNN — {behaviour} not yet conformant for {transport} (maintainer sign-off)";

        // Deferred -> #NNNN (sign-off: @maintainer) → extract the real issue number.
        if (cellValue.StartsWith("Deferred ->", StringComparison.OrdinalIgnoreCase))
        {
            var issueNumber = ExtractIssueNumber(cellValue);
            return $"Deferred: #{issueNumber} — {behaviour} not yet conformant for {transport} (maintainer sign-off)";
        }

        return string.Empty;
    }

    private static string ExtractIssueNumber(string cellValue)
    {
        var hashIndex = cellValue.IndexOf('#');
        if (hashIndex < 0) return "NNNN";
        var start = hashIndex + 1;
        var end = start;
        while (end < cellValue.Length && char.IsDigit(cellValue[end]))
            end++;
        return end > start ? cellValue[start..end] : "NNNN";
    }

    /// <summary>
    /// Reads the conformance matrix out of the ledger's markdown table.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the matrix cannot be found, or is found but holds no rows.
    /// </exception>
    /// <remarks>
    /// Throws for the same reason <see cref="LoadFrom"/> does, one level down. Returning an empty
    /// dictionary here is not "no criteria found" but "every cell means run without a Skip", which
    /// is the single answer a ledger should never be able to give by accident: reformat the header
    /// row and the whole deferral mechanism silently switches off. The cross-check audit would
    /// report every key unresolved, but that is defence-in-depth doing the primary check's job.
    /// </remarks>
    private static Dictionary<string, Dictionary<string, string>> ParseLedger(string path)
    {
        var cells = new Dictionary<string, Dictionary<string, string>>();
        var columns = new List<string>();

        var lines = File.ReadAllLines(path);

        var headerLineIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            // Anchored on the whole cell rather than a substring: "FR-2" as a substring also
            // matches a header carrying only FR-22 or FR-20, and FR-22 is a real column here.
            if (line.StartsWith('|') && HasColumn(line, "FR-2") && HasColumn(line, "FR-4"))
            {
                headerLineIndex = i;
                break;
            }
        }

        if (headerLineIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the conformance matrix in the ledger at '{path}': no table header "
                + "row carrying both an FR-2 and an FR-4 column. The generator will not emit the "
                + "canonical suite from a ledger it cannot read: every cell would resolve to no "
                + "Skip, so every deferred behaviour would be reported as running. Restore the "
                + "matrix header, or update the parser if the table's shape has changed.");
        }

        // Parse column headers (skip the first cell which is "Configuration").
        var headerCells = SplitTableRow(lines[headerLineIndex]);
        for (var i = 1; i < headerCells.Count; i++)
            columns.Add(headerCells[i].Trim());

        // Skip the separator row (|---|---|...|) and parse data rows.
        for (var i = headerLineIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('|')) break;

            var rowCells = SplitTableRow(line);
            if (rowCells.Count < 2) continue;

            var rowKey = rowCells[0].Trim();
            var rowData = new Dictionary<string, string>();

            for (var j = 1; j < Math.Min(rowCells.Count, columns.Count + 1); j++)
                rowData[columns[j - 1]] = rowCells[j].Trim();

            cells[rowKey] = rowData;
        }

        if (cells.Count == 0)
        {
            throw new InvalidOperationException(
                $"Found the conformance matrix in the ledger at '{path}' but it holds no data rows. "
                + "An empty matrix means every cell resolves to no Skip, so every deferred "
                + "behaviour would be reported as running against a ledger that claims nothing.");
        }

        return cells;
    }

    /// <summary>
    /// Whether a markdown table row carries <paramref name="columnName"/> as a whole cell.
    /// </summary>
    private static bool HasColumn(string line, string columnName) =>
        SplitTableRow(line).Any(cell => cell.Trim() == columnName);

    private static List<string> SplitTableRow(string line)
    {
        var parts = line.Split('|');
        var result = new List<string>();
        // Skip the first element (before the leading |) and the last (after the trailing |).
        for (var i = 1; i < parts.Length - 1; i++)
            result.Add(parts[i]);
        return result;
    }
}

/// <summary>
/// An in-memory conformance ledger for use in generator tests.
/// Pre-configure cells as a dictionary of (ledgerKey, frColumn) → raw cell value
/// using the same vocabulary as the markdown ledger:
/// "Pass", "Fixed (#PR)", "Unknown", "Deferred -> #1234 (sign-off: @m)".
/// </summary>
public sealed class InMemoryConformanceLedger : IAmAConformanceLedger
{
    private readonly IReadOnlyDictionary<(string LedgerKey, string FrColumn), string> _cells;

    public InMemoryConformanceLedger(
        IReadOnlyDictionary<(string LedgerKey, string FrColumn), string> cells)
        => _cells = cells;

    public string GetSkip(string ledgerKey, string frColumn, string behaviourName)
    {
        var cellValue = _cells.TryGetValue((ledgerKey, frColumn), out var value)
            ? value
            : string.Empty;
        return ConformanceLedger.ComputeSkip(cellValue, ledgerKey, behaviourName);
    }
}
