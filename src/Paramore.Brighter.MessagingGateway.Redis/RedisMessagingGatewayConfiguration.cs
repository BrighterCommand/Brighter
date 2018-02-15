using System;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagingGatewayConfiguration
    {
        /// <summary>
        /// The default RedisClient Socket ConnectTimeout (default -1, None)
        /// </summary>
        public int? DefaultConnectTimeout { get; set; }

        /// <summary>
        /// The default RedisClient Socket SendTimeout (default -1, None)
        /// </summary>
        public int? DefaultSendTimeout { get; set; }

        /// <summary>
        /// The default RedisClient Socket ReceiveTimeout (default -1, None)
        /// </summary>
        public int? DefaultReceiveTimeout { get; set; }

        /// <summary>
        /// Default Idle TimeOut before a connection is considered to be stale (default 240 secs)
        /// </summary>
        public int? DefaultIdleTimeOutSecs { get; set; }

        /// <summary>
        /// The default RetryTimeout for auto retry of failed operations (default 10,000ms)
        /// </summary>
        public int? DefaultRetryTimeout { get; set; }

      /// <summary>
        /// The Byte Buffer Size to combine Redis Operations within (default 1450 bytes)
        /// </summary>
        public static int BufferLength = 1450;

        /// <summary>
        /// The Byte Buffer Size for Operations to use a byte buffer pool (default 500kb)
        /// </summary>
        public static int BufferPoolMaxSize = 500000;

        /// <summary>
        /// Whether Connections to Master hosts should be verified they're still master instances (default true)
        /// </summary>
        public static bool VerifyMasterConnections = true;

        /// <summary>
        /// The ConnectTimeout on clients used to find the next available host (default 200ms)
        /// </summary>
        public static int HostLookupTimeoutMs = 200;

        /// <summary>
        /// Skip ServerVersion Checks by specifying Min Version number, e.g: 2.8.12 => 2812, 2.9.1 => 2910
        /// </summary>
        public static int? AssumeServerVersion;

        /// <summary>
        /// How long to hold deactivated clients for before disposing their connection (default 1 min)
        /// Dispose of deactivated Clients immediately with TimeSpan.Zero
        /// </summary>
        public static TimeSpan DeactivatedClientsExpiry = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Whether Debug Logging should log detailed Redis operations (default false)
        /// </summary>
        public static bool DisableVerboseLogging = false;
        
        /// <summary>
        /// Exponential backoff interval for retrying connections on socker failure, default 10ms
        /// </summary>
        /// <returns></returns>
        public int? BackoffMultiplier { get; set; }
        
        /// <summary>
        /// How big can the pool get (default None)?
        /// </summary>
        public int? MaxPoolSize { get; set; }
        
        /// <summary>
        /// How long does the message body persist in Redis before being reclaimed
        /// Once we reclaim attempts to retrieve will fail (and message will be rejected)
        /// </summary>
        public TimeSpan? MessageTimeToLive { get; set; }
        
        /// <summary>
        /// How do we connect to Redis
        /// </summary>
        public string RedisConnectionString { get; set; }
    }
}