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
    /// The conformance-ledger row identifier, e.g. "Kafka / Standard".
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

    public string GetSkip(string ledgerKey, string frColumn, string behaviourName)
    {
        if (!_cells.TryGetValue(ledgerKey, out var row)) return string.Empty;
        if (!row.TryGetValue(frColumn, out var cellValue)) return string.Empty;
        return ComputeSkip(cellValue, ledgerKey, behaviourName);
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

    private static Dictionary<string, Dictionary<string, string>> ParseLedger(string path)
    {
        var cells = new Dictionary<string, Dictionary<string, string>>();
        var columns = new List<string>();

        var lines = File.ReadAllLines(path);

        var headerLineIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('|') && line.Contains("FR-2") && line.Contains("FR-4"))
            {
                headerLineIndex = i;
                break;
            }
        }

        if (headerLineIndex < 0) return cells;

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

        return cells;
    }

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
