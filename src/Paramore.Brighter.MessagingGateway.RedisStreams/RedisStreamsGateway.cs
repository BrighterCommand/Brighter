#region Licence
/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

/*
 * For more information on the reconnection strategy here, see: https://gist.github.com/JonCole/925630df72be1351b21440625ff2671f#file-redis-lazyreconnect-cs
 */

#endregion

using System;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsGateway
    {
        private static Lazy<ConnectionMultiplexer> s_redis;
        private readonly RedisStreamsConfiguration _configuration;
        private static DateTimeOffset s_lastReconnect = DateTimeOffset.MinValue;
        private static DateTimeOffset s_firstError = DateTimeOffset.MinValue;
        private static DateTimeOffset s_previousError = DateTimeOffset.MinValue;

        private static readonly object s_reconnectLock = new object();
        protected static ConnectionMultiplexer Redis { get { return s_redis.Value; } }

        protected RedisStreamsGateway(RedisStreamsConfiguration configuration)
        {
            _configuration = configuration;

            CreateRedisClient();
        }


       private void CreateRedisClient()
       {
           s_redis = new Lazy<ConnectionMultiplexer>(() =>
           {
               var options = ConfigurationOptions.Parse(_configuration.ConfigurationOptions);
               options.Proxy = _configuration.UseProxy ? Proxy.Twemproxy : Proxy.None;
               int intervalMs = _configuration.ReconnectIntervalMs;
               options.ReconnectRetryPolicy = _configuration.ReconnectStrategy == ReconnectStrategy.Linear
                   ? (IReconnectRetryPolicy) new LinearRetry(intervalMs)
                   : (IReconnectRetryPolicy) new ExponentialRetry(intervalMs);
               return ConnectionMultiplexer.Connect(options);
           });
       }
      
       protected void CloseRedisClient()
       {
           if (s_redis != null)
           {
               try
               {
                   Redis.Close();
               }
               catch (Exception)
               {
                   // Example error condition: if accessing old.Value causes a connection attempt and that fails.
               }
           }
       }
       protected void ForceReconnect()
       {
           var now = DateTimeOffset.UtcNow;
           var elapsedSinceLastReconnect = now - s_lastReconnect;
           var reconnectMinFrequency = TimeSpan.FromSeconds(_configuration.ReconnectMinFrequencyInSeconds);
           var reconnectWindow = TimeSpan.FromSeconds(_configuration.ReconnectErrorThresholdInSeconds);

           if (elapsedSinceLastReconnect > reconnectMinFrequency)
           {
               lock (s_reconnectLock)
               {
                   now = DateTimeOffset.UtcNow;
                   
                   //check that we were not blocked behind another thread, that has already reconnected
                   elapsedSinceLastReconnect = now - s_lastReconnect;
                   if (elapsedSinceLastReconnect < reconnectMinFrequency)
                       return; 
                   
                   if (s_firstError == DateTimeOffset.MinValue)
                   {
                       s_firstError = now;
                       s_previousError = now;
                       return;
                   }

                   var elapsedSinceFirstError = now - s_firstError;
                   var elapsedSinceMostRecentError = now - s_previousError;
                   s_previousError = now;

                   if (
                       elapsedSinceFirstError >= reconnectWindow   //Did ServiceStack fail to reconnect in the time window 
                       && elapsedSinceMostRecentError <= reconnectWindow //Did we likely retry this already?
                   )

                   {
                       s_firstError = DateTimeOffset.MinValue;
                       s_previousError = DateTimeOffset.MinValue;

                       CloseRedisClient();
                       CreateRedisClient();
                       s_lastReconnect = now;
                   }
               }
           }
       }
     }
}
