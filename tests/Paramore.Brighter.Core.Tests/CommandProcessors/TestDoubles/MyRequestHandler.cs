namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyResponseHandler: RequestHandler<MyResponse>
    {
        private static MyResponse s_response;

        public MyResponseHandler()
        {
            s_response = null;
        }
        
        public override MyResponse Handle(MyResponse myResponse)
        {
            LogCommand(myResponse);
            return base.Handle(myResponse);
        }

        public static bool ShouldReceive(MyResponse expectedCommand)
        {
            return (s_response != null) && 
                (expectedCommand.Id == s_response.Id) &&
                (expectedCommand.SendersAddress.Topic == s_response.SendersAddress.Topic) &&
                (expectedCommand.SendersAddress.CorrelationId == s_response.SendersAddress.CorrelationId);
        }

        private void LogCommand(MyResponse request)
        {
            s_response = request;
        }
 

    }
}
