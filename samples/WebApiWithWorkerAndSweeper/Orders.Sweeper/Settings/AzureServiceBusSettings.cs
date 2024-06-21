namespace Orders.Sweeper.Settings;

public class AzureServiceBusSettings
{
    public static readonly string SettingsKey = "AzureServiceBusSettings";

    public string Endpoint { get; set; } = string.Empty;
}
