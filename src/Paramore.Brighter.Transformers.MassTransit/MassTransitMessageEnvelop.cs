using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// Represents a message envelope used in MassTransit transport integration with Brighter.
/// Contains both the message payload and MassTransit-specific metadata.
/// </summary>
/// <typeparam name="T">The type of the message payload</typeparam>
public sealed class MassTransitMessageEnvelop<T>
{
    /// <summary>Unique identifier for this message</summary>
    public Id? MessageId { get; set; }

    /// <summary>Identifier for the request that initiated this message</summary>
    public Id? RequestId { get; set; }

    /// <summary>Identifier for correlating related messages</summary>
    public Id? CorrelationId { get; set; }

    /// <summary>Identifier for the conversation context</summary>
    public Id? ConversationId { get; set; }

    /// <summary>Identifier of the message initiator</summary>
    public Id? InitiatorId { get; set; }

    /// <summary>Source address where the message originated</summary>
    public Uri? SourceAddress { get; set; }

    /// <summary>Destination address for the message</summary>
    public Uri? DestinationAddress { get; set; }

    /// <summary>Address where responses should be sent</summary>
    public Uri? ResponseAddress { get; set; }

    /// <summary>Address where faults should be reported</summary>
    public Uri? FaultAddress { get; set; }

    /// <summary>Message type URIs supported by MassTransit</summary>
    public string[]? MessageType { get; set; }

    /// <summary>The actual message payload</summary>
    public T? Message { get; set; }

    /// <summary>Time when the message should expire</summary>
    public DateTime? ExpirationTime { get; set; }

    /// <summary>Time when the message was sent</summary>
    public DateTime? SentTime { get; set; }

    /// <summary>Additional message headers</summary>
    public Dictionary<string, object?>? Headers { get; set; }

    /// <summary>Information about the host that produced the message</summary>
    public HostInfo? Host { get; set; }
}

/// <summary>
/// Contains information about the host environment where a message was produced
/// </summary>
public sealed class HostInfo
{
    /// <summary>Name of the machine that sent the message</summary>
    public string? MachineName { get; set; }

    /// <summary>Name of the sending process</summary>
    public string? ProcessName { get; set; }

    /// <summary>Process ID of the sender</summary>
    public int ProcessId { get; set; }

    /// <summary>Name of the entry assembly</summary>
    public string? Assembly { get; set; }

    /// <summary>Version of the entry assembly</summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>.NET framework version</summary>
    public string? FrameworkVersion { get; set; }

    /// <summary>MassTransit package version</summary>
    public string? MassTransitVersion { get; set; }

    /// <summary>Operating system information</summary>
    public string? OperatingSystemVersion { get; set; }

    /// <summary>
    /// Creates a HostInfo instance populated with current process information
    /// </summary>
    /// <returns>Initialized HostInfo object</returns>
    public static HostInfo Create()
    {
        var process = Process.GetCurrentProcess();
        var assembly = System.Reflection.Assembly.GetEntryAssembly();

        return new HostInfo
        {
            MachineName = Environment.MachineName,
            ProcessName = process.ProcessName,
            ProcessId = process.Id,
            OperatingSystemVersion = Environment.OSVersion.VersionString,
            FrameworkVersion = RuntimeInformation.FrameworkDescription,
            MassTransitVersion = "8.4.1",
            Assembly = assembly?.GetName().Name,
            AssemblyVersion = assembly?.GetName().Version?.ToString()
        };
    }
}
