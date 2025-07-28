using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter
{
    /// <summary>
    /// Represents a partition key used to distribute messages across multiple partitions in a message-oriented system.
    /// Partition keys ensure that related messages are processed by the same consumer instance.
    /// </summary>
    /// <remarks>
    /// Partition keys are used to maintain message ordering within a partition while allowing parallel processing across partitions.
    /// Messages with the same partition key are guaranteed to be handled by the same consumer in the order they were sent.
    /// </remarks>
    public record PartitionKey
    {
        /// <summary>
        /// Gets the string representation of the partition key.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionKey"/> class.
        /// </summary>
        /// <param name="value">The string value to use as the partition key</param>
        public PartitionKey(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Implicitly converts a PartitionKey to its string representation.
        /// </summary>
        /// <param name="key">The PartitionKey to convert</param>
        public static implicit operator string(PartitionKey key) => key.Value;

        /// <summary>
        /// Implicitly converts a string to a PartitionKey.
        /// </summary>
        /// <param name="value">The string to convert</param>
        public static implicit operator PartitionKey(string value) => new(value);

        /// <summary>
        /// Returns the string representation of the partition key.
        /// </summary>
        /// <returns>The partition key string value</returns>
        public override string ToString() => Value;
        
        /// <summary>
        /// Gets an empty partition key.
        /// </summary>
        /// <remarks>
        /// An empty partition key indicates that no specific partitioning is required for the message.
        /// </remarks>
        public static PartitionKey Empty => new("");

        /// <summary>
        /// Determines whether the specified partition key is null or empty.
        /// </summary>
        /// <param name="partitionKey">The partition key</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)]PartitionKey? partitionKey)
        {
            return partitionKey == null || string.IsNullOrEmpty(partitionKey.Value);    
        }
    }
}
