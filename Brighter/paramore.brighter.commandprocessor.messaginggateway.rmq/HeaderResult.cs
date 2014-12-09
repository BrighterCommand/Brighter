using System;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class HeaderResult<TResult>
    {
        public HeaderResult(TResult result, bool success)
        {
            Success = success;
            Result = result;
        }

        public HeaderResult<TNew> Map<TNew>( Func<TResult, HeaderResult<TNew>> map)
        {
            if(Success)
                return map(Result);
            return HeaderResult<TNew>.Empty();
        }

        public bool Success { get; private set;}
        public TResult Result { get; private set; }

        public static HeaderResult<TResult> Empty()
        {
            return new HeaderResult<TResult>(default(TResult), false);
        }
    }
}