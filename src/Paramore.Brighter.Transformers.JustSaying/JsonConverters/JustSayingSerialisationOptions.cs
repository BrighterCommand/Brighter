using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.JustSaying.JsonConverters;

/// <summary>
/// The serialisation options used to read and write JustSaying message bodies.
/// </summary>
/// <remarks>
/// JustSaying writes <see cref="Tenant"/> and <see cref="System.Net.IPAddress"/> as flat JSON strings,
/// which needs <see cref="TenantConverter"/> and <see cref="IpAddressConverter"/>. Those converters
/// cannot be added to <see cref="JsonSerialisationOptions.Options"/>, because System.Text.Json makes a
/// <see cref="JsonSerializerOptions"/> read-only the first time anything serialises through it, and
/// Brighter's outbox, inbox and scheduler all share that one instance. Adding to it is therefore a race
/// that either throws or silently does nothing, depending on who got there first.
/// <para>
/// Copying is not: constructing a <see cref="JsonSerializerOptions"/> from a read-only instance is always
/// legal. So we take a copy of Brighter's options, add our two converters to the copy, and leave the
/// shared instance alone. The copy is re-derived if <see cref="JsonSerialisationOptions.Options"/> is
/// replaced, so a host that swaps or reconfigures Brighter's options is still honoured.
/// </para>
/// </remarks>
internal static class JustSayingSerialisationOptions
{
    private static readonly object s_lock = new();
    private static JsonSerializerOptions? s_options;
    private static JsonSerializerOptions? s_derivedFrom;

    /// <summary>
    /// Gets Brighter's serialisation options, extended with the JustSaying converters.
    /// </summary>
    public static JsonSerializerOptions Options
    {
        get
        {
            var brighterOptions = JsonSerialisationOptions.Options;

            lock (s_lock)
            {
                if (s_options is null || !ReferenceEquals(s_derivedFrom, brighterOptions))
                {
                    var options = new JsonSerializerOptions(brighterOptions);
                    options.Converters.Add(new TenantConverter());
                    options.Converters.Add(new IpAddressConverter());

                    s_derivedFrom = brighterOptions;
                    s_options = options;
                }

                return s_options;
            }
        }
    }
}
