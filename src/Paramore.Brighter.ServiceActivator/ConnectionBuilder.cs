using System;

namespace Paramore.Brighter.ServiceActivator
{
    public class ConnectionBuilder : 
        ConnectionBuilder.IConnectionBuilderName,
        ConnectionBuilder.IConnectionBuilderChannelFactory, 
        ConnectionBuilder.IConnectionBuilderChannelType,
        ConnectionBuilder.IConnectionBuilderChannelName,
        ConnectionBuilder.IConnectionBuilderRoutingKey,
        ConnectionBuilder.IConnectionBuilderOptionalBuild
    {
        private string _name;
        private IAmAChannelFactory _inputChannelFactory;
        private Type _type;
        private string _channelName;
        private int _milliseconds = 300;
        private string _routingKey;
        private ConnectionBuilder() {}

        public static IConnectionBuilderName With { get { return new ConnectionBuilder(); } }

        public IConnectionBuilderChannelFactory Name(string name)
        {
            _name = name;
            return this;
        }

        public IConnectionBuilderChannelType ChannelFactory(IAmAChannelFactory inputChannelFactory)
        {
            _inputChannelFactory = inputChannelFactory;
            return this;
        }

        public IConnectionBuilderChannelName Type(Type type)
        {
            _type = type;
            return this;
        }

        public IConnectionBuilderRoutingKey ChannelName(string channelName)
        {
            _channelName = channelName;
            return this;
        }

        public IConnectionBuilderOptionalBuild RoutingKey(string routingKey)
        {
            _routingKey = routingKey;
            return this;
        }

        public IConnectionBuilderOptionalBuild Timeout(int millisecondTimeout)
        {
            _milliseconds = millisecondTimeout;
            return this;
        }

        public Connection Build()
        {
            return new Connection(
                new ConnectionName(_name),
                _inputChannelFactory,
                _type,
                new ChannelName(_channelName),
                _routingKey,
                timeoutInMilliseconds: _milliseconds);
        }

        public interface IConnectionBuilderName
        {
            IConnectionBuilderChannelFactory Name(string name);
        }

        public interface IConnectionBuilderChannelFactory
        {
            IConnectionBuilderChannelType ChannelFactory(IAmAChannelFactory inputChannelFactory);
        }

        public interface IConnectionBuilderChannelType
        {
            IConnectionBuilderChannelName Type(Type type);
        }

        public interface IConnectionBuilderChannelName
        {
            IConnectionBuilderRoutingKey ChannelName(string channelName);
        }

        public interface IConnectionBuilderRoutingKey
        {
            IConnectionBuilderOptionalBuild RoutingKey(string routingKey);
        }

        public interface IConnectionBuilderOptionalBuild
        {
            Connection Build();
            IConnectionBuilderOptionalBuild Timeout(int millisecondTimeout);
        }

    }
}