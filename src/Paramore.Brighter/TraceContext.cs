namespace Paramore.Brighter
{
    /// <summary>
    /// Represents the W3C Trace Parent context for distributed tracing.
    /// </summary>
    /// <remarks>
    /// The traceparent field describes the position of the incoming request in its trace graph.
    /// Format: version-traceid-parentid-traceflags
    /// Example: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
    /// 
    /// Components:
    /// - version: 2 hex digits (e.g., "00")
    /// - trace-id: 32 hex digits (e.g., "4bf92f3577b34da6a3ce929d0e0e4736")
    /// - parent-id: 16 hex digits (e.g., "00f067aa0ba902b7")
    /// - trace-flags: 2 hex digits (e.g., "01")
    /// </remarks>
    public record TraceParent
    {
        /// <summary>
        /// Represents the traceparent value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Represents an empty TraceParent instance. The value is an empty string.
        /// </summary>
        public static TraceParent Empty { get; } = new TraceParent(string.Empty);

        /// <summary>
        /// Creates a new instance of the TraceParent class.
        /// </summary>
        /// <param name="value"></param>
        public TraceParent(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Converts the TraceParent instance to a string.
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static implicit operator string(TraceParent parent) => parent.Value;
        
        /// <summary>
        /// Converts a string to a TraceParent instance.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator TraceParent(string value) => new(value);

        /// <summary>
        /// Determines if the TraceParent is null or empty.
        /// </summary>
        /// <param name="traceParent">The traceparent to check</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(TraceParent? traceParent)
        {
           return traceParent == null || string.IsNullOrEmpty(traceParent.Value);
        }
    }

    /// <summary>
    /// Represents the W3C Trace State for OpenTelemetry vendor-specific context propagation.
    /// This field is managed by the OpenTelemetry implementation and should not be modified directly.
    /// </summary>
    /// <remarks>
    /// The tracestate field conveys vendor-specific trace identification data across systems.
    /// Format: vendor1=value1,vendor2=value2
    /// 
    /// Key-value pairs have the following restrictions:
    /// - Maximum 32 entries
    /// - Maximum 256 characters per entry
    /// - Keys can only contain [a-z0-9] and [-_*/@]
    /// - Values can only contain [a-zA-Z0-9] and [-_.*/@]
    /// 
    /// This implementation is specifically for OpenTelemetry use and should be treated as opaque.
    /// </remarks>
    public record TraceState
    {
        /// <summary>
        /// Gets the vendor-specific trace state value.
        /// This value is opaque and should only be modified by the OpenTelemetry implementation.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates an empty TraceState instance. The value is an empty string.
        /// </summary>
        public static TraceState Empty { get; } = new TraceState(string.Empty);

        /// <summary>
        /// Creates a new instance of the TraceState class.
        /// </summary>
        /// <param name="value">The OpenTelemetry trace state string</param>
        public TraceState(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Converts the TraceState instance to a string.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static implicit operator string(TraceState state) => state.Value;
        
        /// <summary>
        /// Converts a string to a TraceState instance.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator TraceState(string value) => new(value);

        /// <summary>
        /// Determines if the TraceState is null or empty.
        /// </summary>
        /// <param name="traceState">The traceState</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(TraceState? traceState)
        {
            return traceState == null || string.IsNullOrEmpty(traceState.Value);
        }
    }
}
