using System;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Model
{
    /*
         Pipe - a source of messages delivered to applications.
        Pipes follow these rules:

        A pipe is a read-only ordered stream of messages meant for a single reader.
        The order of messages in a pipe is stable only for a single feed.
        Pipes receive messages from joins, according to the joins defined between the pipe and the feed.
        Clients MUST create pipes for their own use: all pipes are private and dynamic.
        To create a new pipe the client POSTs a pipe document to the parent domain's URI.
        The server MAY do garbage collection on unused, or overflowing pipes.

     */

    public class Pipe : Resource, IAmAnAggregate
    {
        const string PIPE_URI_FORMAT = "http://{0}/restms/pipe/{1}";
        public Identity Id { get; private set; }
        public AggregateVersion Version { get; private set; }
        public Title Title { get; private set; }
        public PipeType Type { get; private set; }

        public Pipe(Identity identity, string pipeType, Title title= null)
        {
            Id = identity;
            Title = title;
            Name = new Name(Id.Value);
            Type = (PipeType) Enum.Parse(typeof (PipeType), pipeType);
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, identity.Value));
        }

        public Pipe(Identity identity, PipeType pipeType, Title title = null)
        {
            Id = identity;
            Title = title;
            Name = new Name(Id.Value);
            Type = pipeType;
            Href = new Uri(string.Format(PIPE_URI_FORMAT, Globals.HostName, identity.Value));
        }
    }
}
