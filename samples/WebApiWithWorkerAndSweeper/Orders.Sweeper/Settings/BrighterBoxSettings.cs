namespace Orders.Sweeper.Settings;

public class BrighterBoxSettings
{
    public const string SettingsKey = "BrighterBox";

    public string OutboxTableName { get; set; } = "BrighterOutbox";

    public int OutboxSweeperInterval { get; set; } = 5;

    public bool UseMsi { get; set; } = true;
    
    public string ConnectionString { get; set; }

    public int MinimumMessageAge { get; set; } = 5000;
    /// <summary>
    /// The number of messages to take each run of the sweeper
    /// </summary>
    public int BatchSize { get; set; }
    /// <summary>
    /// Send using bulk
    /// </summary>
    public bool UseBulk { get; set; }
    /// <summary>
    /// The chunk size when bulk sending
    /// </summary>
    public int BatchChunkSize { get; set; }
}
