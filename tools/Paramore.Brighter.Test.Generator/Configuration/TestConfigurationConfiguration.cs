using System.Collections.Generic;

namespace Paramore.Brighter.Test.Generator.Configuration;

public class TestConfigurationConfiguration
{
    public string Namespace { get; set; } = string.Empty;
    public string DestinyFolder { get; set; } = string.Empty;
    public string? MessageFactory { get; set; }
    public OutboxConfiguration? Outbox { get; set; }
    public Dictionary<string, OutboxConfiguration>? Outboxes { get; set; }
}
