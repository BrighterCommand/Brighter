using Nancy;

public class HelloModule : NancyModule
{
    public HelloModule()
    {
        Get["/"] = parameters => "Hello World";
    }
}