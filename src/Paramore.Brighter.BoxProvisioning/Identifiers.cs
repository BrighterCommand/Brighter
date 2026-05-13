#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Text.RegularExpressions;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Defence-in-depth validation for SQL identifiers (table names, schema names) that flow through
/// migration up-scripts and runner DDL. DDL statements such as <c>ALTER TABLE</c> or Spanner's
/// <c>CREATE TABLE</c> cannot parameterize identifiers, so backend migrations interpolate the
/// configured table name directly into the SQL string. A name containing characters outside the
/// allowed set is an injection vector for any host that builds the table name from external input.
/// </summary>
/// <remarks>
/// The allowed regex <c>^[A-Za-z_][A-Za-z0-9_]*$</c> matches the bare-identifier rules common to
/// every supported backend (MSSQL, PostgreSQL, MySQL, SQLite, Spanner) and is a strict subset of
/// what each backend permits inside quoted/backticked identifiers. The over-restriction is
/// deliberate: the helper rejects unusual but legal identifiers (e.g. names containing spaces or
/// non-ASCII characters that would otherwise need quoting) before they reach a code path where a
/// future contributor might forget to escape them.
/// </remarks>
public static class Identifiers
{
    private static readonly Regex s_safeIdentifier = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Throws if <paramref name="identifier"/> is not a safe SQL identifier.
    /// </summary>
    /// <param name="identifier">The identifier to validate (table name, schema name, etc.).</param>
    /// <param name="parameterName">The name of the caller's parameter — included in the exception
    /// message so contributors can see which call-site rejected the value.</param>
    /// <exception cref="ConfigurationException">Thrown when <paramref name="identifier"/> is null,
    /// empty, or contains characters outside <c>[A-Za-z0-9_]</c> or starts with a digit.</exception>
    public static void AssertSafe(string identifier, string parameterName)
    {
        // Split missing-configuration (null) from malformed-identifier so operators see the real
        // root cause. Pointing at the regex when no identifier was supplied at all sends them
        // looking for an "invalid character" that does not exist.
        if (identifier is null)
        {
            throw new ConfigurationException(
                $"Required SQL identifier '{parameterName}' was null. A non-null value must be supplied.");
        }

        if (s_safeIdentifier.IsMatch(identifier)) return;

        throw new ConfigurationException(
            $"Unsafe SQL identifier supplied for '{parameterName}': '{identifier}'. " +
            $"Identifiers must match the regex ^[A-Za-z_][A-Za-z0-9_]*$.");
    }
}
