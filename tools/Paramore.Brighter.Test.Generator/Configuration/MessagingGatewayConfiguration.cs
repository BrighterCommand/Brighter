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

using System.Text.Json.Serialization;

namespace Paramore.Brighter.Test.Generator.Configuration;

/// <summary>
/// Represents the configuration for generating messaging gateway tests.
/// </summary>
public class MessagingGatewayConfiguration
{
    /// <summary>
    /// Gets or sets the prefix to use for the generated test class names.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace for the generated messaging gateway test code. If null, uses the parent configuration's namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the message builder for creating test messages. If null, uses the parent configuration's message builder.
    /// </summary>
    public string? MessageBuilder { get; set; }

    /// <summary>
    /// Gets or sets the message assertion helper to use for validating test messages. If null, uses the parent configuration's message assertion.
    /// </summary>
    public string? MessageAssertion { get; set; }

    /// <summary>
    /// Gets or sets the messaging gateway provider implementation to test.
    /// </summary>
    public string? MessageGatewayProvider { get; set; }

    /// <summary>
    /// Gets or sets the test category to apply to generated test classes.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the test collection name for controlling test execution grouping.
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the publication configuration for the messaging gateway.
    /// </summary>
    public string Publication { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subscription configuration for the messaging gateway.
    /// </summary>
    public string Subscription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delay between consecutive receive message operations in milliseconds.
    /// </summary>
    public int? DelayBetweenReceiveMessageInMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the messaging gateway supports publish confirmations.
    /// </summary>
    public bool HasSupportToPublishConfirmation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the messaging gateway supports validating broker existence.
    /// </summary>
    public bool HasSupportToValidateBrokerExistence { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the messaging gateway supports validating infrastructure existence.
    /// </summary>
    public bool HasSupportToValidateInfrastructure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the messaging gateway surfaces an error when
    /// infrastructure is absent and <c>OnMissingChannel.Assume</c> told it not to look.
    ///
    /// This is narrower than <see cref="HasSupportToValidateInfrastructure"/>, which covers the
    /// explicit <c>OnMissingChannel.Validate</c> check. A transport can support the explicit check
    /// and still complete a send/receive silently against infrastructure that does not exist —
    /// Kafka's KIP-848 consumer-group protocol does exactly that. Setting this false skips only the
    /// <c>assume_channel</c> template, leaving <c>validate_channel</c> generated.
    ///
    /// The only configuration declaring it false is <c>Kafka / Consumer</c>, tracked by
    /// BrighterCommand/Brighter#4299. That is a gateway defect, not a platform limit: when it is
    /// fixed, drop the flag from that configuration and let this default back to true.
    /// </summary>
    public bool HasSupportToDetectMissingInfrastructureOnAssume { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum time to wait when receiving a message in milliseconds.
    /// </summary>
    public int ReceiveMessageTimeoutInMilliseconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum time to wait for a message publish confirmation in milliseconds.
    /// </summary>
    public int MessageConfirmationTimeoutInMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the conformance-ledger row key for this configuration,
    /// e.g. "Kafka / Classic" or "RMQ.Async / Classic" (FR-21 / ADR 0067).
    /// When null the ledger-driven Skip mechanism is skipped for this configuration.
    /// </summary>
    public string? LedgerKey { get; set; }

    /// <summary>
    /// Gets or sets the per-template Deferred Skip value computed from the conformance ledger.
    /// Set by <see cref="Generators.MessagingGatewayGenerator"/> before each canonical template
    /// render; empty when the ledger cell is Pass/Fixed (test runs without a Skip).
    /// </summary>
    /// <remarks>
    /// Render state, not configuration: it is never read from the JSON files, so it is not bound
    /// from them either. Defaults to empty rather than null because Liquid treats
    /// <c>nil != empty</c> as TRUE, so a null would render <c>[Fact(Skip = "")]</c> and silently
    /// skip the test.
    /// </remarks>
    [JsonIgnore]
    public string Skip { get; set; } = string.Empty;

    /// <summary>
    /// Returns a copy of this configuration carrying <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">The prefix the copy should carry.</param>
    /// <returns>A copy; this instance is unchanged.</returns>
    /// <remarks>
    /// <para>
    /// Templates read <see cref="Prefix"/> to build a namespace suffix, and the value they need is
    /// not always the one the configuration file declares. Handing each rendering its own copy keeps
    /// that difference out of the caller's object, so planning what would be generated can be asked
    /// as a question rather than performed as an edit.
    /// </para>
    /// <para>
    /// The copy is a <see cref="object.MemberwiseClone"/>, which is a deep copy only because every
    /// property here is a string or a value type. A property holding a list or a dictionary would be
    /// shared with the caller's object, and the purity this method exists for would be lost without
    /// anything failing to compile.
    /// </para>
    /// </remarks>
    internal MessagingGatewayConfiguration WithPrefix(string prefix)
    {
        var copy = (MessagingGatewayConfiguration)MemberwiseClone();
        copy.Prefix = prefix;
        return copy;
    }

    /// <summary>
    /// Returns a copy of this configuration with values it does not set taken from the root
    /// <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The root configuration to inherit unset values from.</param>
    /// <returns>A copy; this instance is unchanged.</returns>
    /// <remarks>
    /// Applied where the model a rendering reads is built, so that describing the work and
    /// performing it see the same model. Applying it on only one of those paths is how the
    /// generator's expected set and its output would come to disagree - which is the drift the
    /// generated-tree audit exists to catch, and so the last place it should be reintroduced.
    /// </remarks>
    internal MessagingGatewayConfiguration WithDefaultsFrom(TestConfiguration configuration)
    {
        var copy = (MessagingGatewayConfiguration)MemberwiseClone();

        if (string.IsNullOrEmpty(copy.MessageBuilder))
        {
            copy.MessageBuilder = configuration.MessageBuilder;
        }

        if (string.IsNullOrEmpty(copy.Namespace))
        {
            copy.Namespace = configuration.Namespace;
        }

        if (string.IsNullOrEmpty(copy.MessageAssertion))
        {
            copy.MessageAssertion = configuration.MessageAssertion;
        }

        return copy;
    }
}
