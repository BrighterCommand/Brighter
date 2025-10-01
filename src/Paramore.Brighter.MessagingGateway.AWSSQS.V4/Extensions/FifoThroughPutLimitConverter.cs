using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4.Extensions;

/// <summary>
/// This extenstion method class allows conversion of FifoThrougputLimit correctly
/// </summary>
public static class FifoThroughPutLimitConverter
{
    /// <summary>
    /// Convert the FifoThroughputLimit to a string
    /// </summary>
    /// <param name="limit">The <see cref="FifoThroughputLimit"/> to convert</param>
    /// <returns>A <see cref="string"/> that is either "perQueue" or "perMessageGroupId"</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string AsString(this FifoThroughputLimit limit)
    {
        return limit switch
        {
            FifoThroughputLimit.PerQueue => "perQueue",
            FifoThroughputLimit.PerMessageGroupId => "perMessageGroupId",
            _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, null)
        };
    }
}
