using System;
using Google.Api.Gax;

namespace Paramore.Brighter.Firestore.Extensions;

public static class ExpirationExtensions
{
    public static Expiration? ToExpiration(this int timeout)
    {
        if (timeout == -1)
        {
            return null;
        }
        
        return Expiration.FromTimeout(TimeSpan.FromMilliseconds(timeout));
    }
}
