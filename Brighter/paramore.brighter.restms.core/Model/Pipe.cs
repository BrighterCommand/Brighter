using System;
using System.Transactions;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Model
{
    public enum PipeType
    {
        Default = 0
    }

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
