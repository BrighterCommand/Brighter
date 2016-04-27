using System;
using paramore.brighter.commandprocessor.monitoring.Configuration;
using paramore.brighter.commandprocessor.monitoring.Handlers;

namespace paramore.brighter.commandprocessor.monitoring.Attributes
{
    /// <summary>
    /// Class MonitorAttribute.
    /// Using this attribute indicates that you intend to monitor the handler. Monitoring implies that diagnostic information will be sent over the ControlBus to any subscribers.
    /// A configuration setting acts as a 'master switch' to turn monitoring on and off. The <see cref="MonitoringConfigurationSection"/> provides that switch.
    ///
    /// </summary>
    public class MonitorAsyncAttribute : RequestHandlerAttribute
    {
        private readonly string _handlerName;
        private readonly bool _monitoringEnabled = false;
        private readonly string _instanceName;
        private readonly string _handlerFullAssemblyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorAsyncAttribute" /> class.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        /// <param name="handlerType">The type of the monitored handler (used to extract the assembly qualified type name for instrumentation purposes)</param>
        public MonitorAsyncAttribute(int step, HandlerTiming timing, Type handlerType)
            : base(step, timing)
        {
            _handlerName = handlerType.FullName;
            _handlerFullAssemblyName = handlerType.AssemblyQualifiedName;
            var monitoringSetting = MonitoringConfigurationSection.GetConfiguration();

            _monitoringEnabled = monitoringSetting.Monitor.IsMonitoringEnabled;
            _instanceName = monitoringSetting.Monitor.InstanceName;
        }

        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public override object[] InitializerParams()
        {
            return new object[] { _monitoringEnabled, _handlerName, _instanceName, _handlerFullAssemblyName };
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof(MonitorHandlerAsync<>);
        }
    }
}