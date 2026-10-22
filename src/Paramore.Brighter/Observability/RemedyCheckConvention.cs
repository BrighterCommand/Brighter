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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Observability;

/// <summary>
/// The tags and <c>Data</c> keys that every Brighter remedy health check uses, so that a host can route
/// each remedy kind to its own endpoint.
/// </summary>
public static class RemedyCheckConvention
{
    /// <summary>The remedy tag for <see cref="RemedyKind.Restart"/>.</summary>
    public const string RestartTag = "brighter-restart";

    /// <summary>The remedy tag for <see cref="RemedyKind.TakeOutOfService"/>.</summary>
    public const string TakeOutOfServiceTag = "brighter-take-out-of-service";

    /// <summary>The remedy tag for <see cref="RemedyKind.Investigate"/>.</summary>
    public const string InvestigateTag = "brighter-investigate";

    /// <summary>The <c>Data</c> key that holds the remedy kind's name.</summary>
    public const string RemedyKindKey = "remedy_kind";

    /// <summary>The <c>Data</c> key that holds the names of the conditions that apply.</summary>
    public const string ConditionsKey = "conditions";

    /// <summary>
    /// Gets the remedy tag for a remedy kind.
    /// </summary>
    /// <param name="kind">The remedy kind.</param>
    /// <returns>The remedy tag.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not a defined value.</exception>
    public static string Tag(RemedyKind kind) => kind switch
    {
        RemedyKind.Restart => RestartTag,
        RemedyKind.TakeOutOfService => TakeOutOfServiceTag,
        RemedyKind.Investigate => InvestigateTag,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a defined remedy kind.")
    };

    /// <summary>
    /// Gets the <c>remedy_kind</c> <c>Data</c> value for a remedy kind.
    /// </summary>
    /// <param name="kind">The remedy kind.</param>
    /// <returns>The kind's name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not a defined value.</exception>
    public static string Name(RemedyKind kind) => kind switch
    {
        RemedyKind.Restart => "restart",
        RemedyKind.TakeOutOfService => "take_out_of_service",
        RemedyKind.Investigate => "investigate",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a defined remedy kind.")
    };

    /// <summary>Builds the <c>Data</c> for a remedy check with no applicable conditions.</summary>
    /// <param name="kind">The remedy kind.</param>
    /// <returns>A mutable dictionary holding the kind's name and an empty conditions array.</returns>
    public static Dictionary<string, object> Data(RemedyKind kind) => new()
    {
        [RemedyKindKey] = Name(kind),
        [ConditionsKey] = Array.Empty<string>()
    };

    /// <summary>Builds the <c>Data</c> for a remedy check with applicable conditions.</summary>
    /// <param name="kind">The remedy kind.</param>
    /// <param name="conditions">The applicable conditions.</param>
    /// <returns>A mutable dictionary holding the kind's name, the condition names in order, and each condition's values under its name.</returns>
    public static Dictionary<string, object> Data(RemedyKind kind, IReadOnlyCollection<RemedyCondition> conditions)
    {
        var data = new Dictionary<string, object>
        {
            [RemedyKindKey] = Name(kind),
            [ConditionsKey] = conditions.Select(condition => condition.Name).ToArray()
        };
        foreach (var condition in conditions)
            data[condition.Name] = condition.Values;
        return data;
    }

    /// <summary>Joins the descriptions of the applicable conditions.</summary>
    /// <param name="conditions">The applicable conditions.</param>
    /// <returns>The descriptions separated by a semicolon and a space.</returns>
    public static string Describe(IEnumerable<RemedyCondition> conditions) =>
        string.Join("; ", conditions.Select(condition => condition.Description));
}
