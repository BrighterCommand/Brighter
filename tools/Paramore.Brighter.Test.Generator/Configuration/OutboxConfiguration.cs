namespace Paramore.Brighter.Test.Generator.Configuration;

public class OutboxConfiguration
{
    public string Prefix { get; set; } = string.Empty;
    public string Transaction { get; set; } = string.Empty;
    public string OutboxProvider { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string? MessageFactory { get; set; }
}
