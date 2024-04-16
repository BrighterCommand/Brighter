namespace Orders.Sweeper.Settings;

public class BrighterBoxSettings
{
    public const string SettingsKey = "BrighterBox";

    public string OutboxTableName { get; init; } = "BrighterOutbox";

    public int OutboxSweeperInterval { get; init; } = 5;

    public bool UseMsi { get; init; } = true;
    
    public string ConnectionString { get; init; } = string.Empty;

    public int MinimumMessageAge { get; init; } = 5000;
    /// <summary>
    /// The number of messages to take each run of the sweeper
    /// </summary>
    public int BatchSize { get; init; }
    /// <summary>
    /// Send using bulk
    /// </summary>
    public bool UseBulk { get; init; }
    /// <summary>
    /// The chunk size when bulk sending
    /// </summary>
    public int BatchChunkSize { get; init; }
}
