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

using System.Collections.Generic;

namespace Paramore.Brighter.Test.Generator;

/// <summary>
/// The canonical conformance behaviours (FR-21 / ADR 0067): the mapping from a canonical template
/// to the conformance-ledger column it is judged against, and the human-readable label used for
/// that column in a Deferred Skip string.
///
/// This is the single source of truth for both mappings. The generator reads it when deciding
/// whether to emit a Skip, and the conformance audit reads it when checking an already-generated
/// Skip back against the ledger cell it claims to come from. Holding one copy is what lets the
/// audit assert the exact cell rather than merely that the issue number appears somewhere in the
/// ledger — two copies could drift and the audit would then be checking the wrong cell.
/// </summary>
public static class CanonicalBehaviours
{
    /// <summary>
    /// Canonical template base name (which is also the generated file's name, without extension)
    /// to the conformance-ledger column that template is judged against.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TEMPLATE_FR_COLUMNS =
        new Dictionary<string, string>
        {
            ["When_requeuing_a_failed_message_should_be_redelivered"]                          = "FR-22",
            ["When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay"]        = "FR-2",
            ["When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately"]   = "FR-15",
            ["When_rejecting_message_with_delivery_error_should_send_to_dlq"]                  = "FR-4",
            ["When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel"] = "FR-5",
            ["When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq"] = "FR-6",
            ["When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log"]  = "FR-7",
            ["When_rejecting_message_with_unknown_reason_should_send_to_dlq"]                  = "FR-17",
            ["When_rejecting_message_should_include_metadata"]                                 = "FR-8",
            ["When_sending_a_delayed_message_should_deliver_after_delay"]                      = "FR-9",
            ["When_nacking_a_message_it_should_be_redelivered"]                                = "FR-16",
        };

    /// <summary>
    /// Human-readable behaviour label for each conformance-ledger column, used in the Skip string.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FR_COLUMN_BEHAVIOURS =
        new Dictionary<string, string>
        {
            ["FR-2"]  = "requeue with delay",
            ["FR-4"]  = "reject with delivery error to DLQ",
            ["FR-5"]  = "reject with unacceptable reason to invalid channel",
            ["FR-6"]  = "fallback: unacceptable, DLQ-only",
            ["FR-7"]  = "no channels configured: acknowledge and log",
            ["FR-8"]  = "rejection metadata stamping",
            ["FR-9"]  = "delayed send",
            ["FR-15"] = "explicit zero-delay requeue",
            ["FR-16"] = "Nack redelivers",
            ["FR-17"] = "reject with None reason to DLQ",
            ["FR-22"] = "canonical plain requeue",
        };

    /// <summary>
    /// Returns the ledger column for <paramref name="templateBaseName"/>, or null when the
    /// template is not a canonical conformance behaviour (and so is never ledger-gated).
    /// </summary>
    public static string? FrColumnFor(string templateBaseName)
        => TEMPLATE_FR_COLUMNS.GetValueOrDefault(templateBaseName);

    /// <summary>
    /// Returns the behaviour label for <paramref name="frColumn"/>, falling back to the column
    /// key itself so an unmapped column still yields a legible Skip string.
    /// </summary>
    public static string BehaviourFor(string frColumn)
        => FR_COLUMN_BEHAVIOURS.GetValueOrDefault(frColumn, frColumn);
}
