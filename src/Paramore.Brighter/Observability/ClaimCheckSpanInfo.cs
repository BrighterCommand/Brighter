using System.Collections.Generic;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Represents contextual information for an OpenTelemetry span related to a claim check operation.
/// This record is used to capture key details that are relevant for tracing interactions
/// with various luggage (claim check) storage providers.
/// </summary>
/// <param name="Operation">
/// The type of claim check operation being performed (e.g., store, retrieve, delete, has claim).
/// See <see cref="ClaimCheckOperation"/> for possible values.
/// </param>
/// <param name="ProviderName">
/// The name of the claim check storage provider (e.g., "azure_blob", "mongodb_gridfs", "file_system").
/// </param>
/// <param name="BucketName">
/// The name of the storage container or bucket involved in the operation (e.g., S3 bucket name,
/// Azure Blob container name, MongoDB GridFS bucket name, or file system base directory path).
/// </param>
/// <param name="Id">
/// The unique identifier (claim check ID) of the payload being operated on.
/// </param>
/// <param name="Attributes">
/// An optional dictionary of additional attributes to be added to the OpenTelemetry span.
/// These can provide more specific details about the operation or the storage environment.
/// </param>
/// <param name="ContentLenght">
/// An optional value representing the content length (in bytes) of the payload involved in the operation.
/// This is particularly useful for 'Store' or 'Retrieve' operations.
/// </param>
public record ClaimCheckSpanInfo(
    ClaimCheckOperation Operation,
    string ProviderName, 
    string BucketName,
    string Id,
    Dictionary<string, string>? Attributes = null,
    long? ContentLenght = null);
