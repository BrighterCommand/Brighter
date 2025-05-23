using System.Collections.Generic;

namespace Paramore.Brighter.Observability;

public record ClaimCheckSpanInfo(
    ClaimCheckOperation Operation,
    string ProviderName, 
    string BucketName,
    string ClaimCheckKey,
    Dictionary<string, string>? Attributes = null,
    long? ContentLenght = null);
