using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.JustSaying.JsonConverters;

internal static class RegisterConverters
{
    private static bool s_register;
    private static readonly object s_lock = new();

    public static void Register()
    {
        if (s_register)
        {
            return;
        }

        lock (s_lock)
        {
            // double-locking pattern
            if (s_register)
            {
                return;
            }
            
            if (!JsonSerialisationOptions.Options.Converters.IsReadOnly)
            {
                JsonSerialisationOptions.Options.Converters.Add(new TenantConverter());
                JsonSerialisationOptions.Options.Converters.Add(new IpAddressConverter());
            }

            s_register = true;
        }
    }
}
