using System;

namespace Paramore.Brighter.Observability;

public static class CommandProcessorSpanExtenstions
{
   public static string ToSpanName(this CommandProcessorSpan span) => span switch
   {
       CommandProcessorSpan.Create => "create",
       CommandProcessorSpan.Publish => "publish",
       CommandProcessorSpan.Send => "send",
       _ => throw new ArgumentOutOfRangeException(nameof(span), span, null)
   }; 
}
